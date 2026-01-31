using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModbusClient.Interfaces;
using ModbusClient.Models;

namespace ModbusClient.Workers;

public class ModbusWorker(
    IServiceScopeFactory scopeFactory,
    IDeviceService deviceService,
    IReadingService readingService,
    IModbusService modbusDriver,
    IBufferRepository buffer,
    IHostApplicationLifetime hostLifetime,
    ILogger<ModbusWorker> logger
) : BackgroundService
{
    private volatile int _failedDevicesCount = 0;
    private volatile string? _lastGlobalError = null;

    private List<Device> _devices = [];
    private Dictionary<int, List<SensorSettings>> _sensorCache = [];
    private readonly ConcurrentDictionary<int, (double Value, DateTime Time)> _lastSavedValues =
        new();

    private volatile SystemConfig _currentConfig = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Modbus Worker v3.4 (Offline Buffering) started");

        // 1. Initialize local buffer
        await buffer.InitializeAsync();

        // 2. Initial Load with Retry Policy
        if (!await TryInitializeConfigAsync(stoppingToken))
        {
            logger.LogCritical("Failed to load initial configuration. Stopping service.");
            hostLifetime.StopApplication();
            return;
        }

        // 3. Start Loops
        var pollTask = RunDynamicPollingLoop(stoppingToken);
        var configTask = RunConfigLoop(stoppingToken);
        var healthTask = RunHealthLoop(stoppingToken);
        var bufferTask = RunBufferFlusherLoop(stoppingToken);

        await Task.WhenAll(pollTask, configTask, healthTask, bufferTask);
    }

    private async Task<bool> TryInitializeConfigAsync(CancellationToken ct)
    {
        const int maxRetries = 10;
        int delay = 2000;

        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                await ReloadConfigurationAsync();
                logger.LogInformation("Initial configuration loaded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Attempt {N}/{Max}: Failed to load config from DB. Retrying in {S}s...",
                    i,
                    maxRetries,
                    delay / 1000
                );
                if (i == maxRetries)
                    break;

                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                delay = Math.Min(delay * 2, 30000);
            }
        }
        return false;
    }

    private async Task RunDynamicPollingLoop(CancellationToken ct)
    {
        var currentInterval = _currentConfig.PollingIntervalMs;
        if (currentInterval <= 0)
            currentInterval = 1000;

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(currentInterval));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(ct);

                if (
                    _currentConfig.PollingIntervalMs != currentInterval
                    && _currentConfig.PollingIntervalMs > 0
                )
                {
                    currentInterval = _currentConfig.PollingIntervalMs;
                    timer.Dispose();
                    timer = new PeriodicTimer(TimeSpan.FromMilliseconds(currentInterval));
                    logger.LogInformation("Polling interval updated to {Ms}ms", currentInterval);
                }

                if (_devices.Count == 0)
                    continue;

                await ProcessPollingCycle(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ProcessPollingCycle(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

            var allMetrics = new List<Metric>();
            var results = await Task.WhenAll(_devices.Select(d => ReadDeviceAsync(d, ct)));

            foreach (var r in results)
            {
                if (r.Success)
                    allMetrics.AddRange(r.Metrics);
            }

            _failedDevicesCount = results.Count(r => !r.Success);

            if (allMetrics.Count > 0)
            {
                try
                {
                    await repository.SaveMetricsAsync(allMetrics);
                    if (_lastGlobalError?.StartsWith("DB Error") == true)
                        _lastGlobalError = null;
                }
                catch (Exception ex)
                {
                    _lastGlobalError = $"DB Error: {ex.Message}";
                    logger.LogWarning(
                        "Postgres unreachable. Buffering {Count} metrics to SQLite.",
                        allMetrics.Count
                    );
                    await buffer.AddRangeAsync(allMetrics);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastGlobalError = $"Polling critical: {ex.Message}";
            logger.LogError(ex, "Critical error in polling loop");
        }
    }

    private async Task<(bool Success, IEnumerable<Metric> Metrics)> ReadDeviceAsync(
        Device device,
        CancellationToken ct
    )
    {
        try
        {
            var master = await deviceService.GetConnectionAsync(device, ct);
            if (master == null)
                return (false, []);

            var analogRaw = await modbusDriver.ReadAnalogAsync(master);
            var digitalRaw = await modbusDriver.ReadDigitalAsync(master);

            if (!_sensorCache.TryGetValue(device.Id, out var sensors))
                return (true, []);

            var rawMetrics = new List<Metric>();
            rawMetrics.AddRange(readingService.ProcessAnalog(analogRaw, sensors));
            rawMetrics.AddRange(readingService.ProcessDigital(digitalRaw, sensors));

            var filteredMetrics = new List<Metric>();
            foreach (var m in rawMetrics)
            {
                var setting = sensors.FirstOrDefault(s => s.SensorId == m.SensorId);
                if (setting != null && ShouldSaveMetric(m, setting))
                {
                    filteredMetrics.Add(m);
                    _lastSavedValues[m.SensorId] = (m.Value, m.Time);
                }
            }

            return (true, filteredMetrics);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Device {Name}: {Msg}", device.Name, ex.Message);
            deviceService.InvalidateConnection(device.Id);
            return (false, []);
        }
    }

    private async Task RunBufferFlusherLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Wait between flush attempts
                await Task.Delay(5000, ct);

                var count = await buffer.CountAsync();
                if (count == 0)
                    continue;

                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

                var metrics = await buffer.PeekAsync(1000);
                var metricsList = metrics.ToList();
                if (metricsList.Count == 0)
                    continue;

                try
                {
                    logger.LogInformation(
                        "Flushing {Count} metrics from buffer to main DB...",
                        metricsList.Count
                    );
                    await repository.SaveMetricsAsync(metricsList);
                    await buffer.RemoveOldestAsync(metricsList.Count);
                }
                catch
                {
                    // Ignore, DB still down. Log rarely if needed.
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Buffer flusher error");
            }
        }
    }

    private bool ShouldSaveMetric(Metric m, SensorSettings s)
    {
        if (!_lastSavedValues.TryGetValue(m.SensorId, out var last))
            return true;
        if ((m.Time - last.Time).TotalSeconds >= _currentConfig.DataHeartbeatSec)
            return true;

        if (s.DataType == SensorDataType.ANALOG)
        {
            var delta = Math.Abs(m.Value - last.Value);
            var range = Math.Abs(s.OutputMax - s.OutputMin);
            if (range < 0.0001)
                range = 100.0;
            return delta > range * _currentConfig.DeadbandThreshold;
        }

        if (s.DataType == SensorDataType.DIGITAL)
        {
            return Math.Abs(m.Value - last.Value) > 0.5;
        }

        return true;
    }

    private async Task RunConfigLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var delay =
                    _currentConfig.ConfigReloadIntervalSec > 0
                        ? _currentConfig.ConfigReloadIntervalSec
                        : 60;
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                await ReloadConfigurationAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Config reload failed");
            }
        }
    }

    private async Task RunHealthLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync();
            }
            catch { }
            try
            {
                var delay =
                    _currentConfig.HealthCheckIntervalSec > 0
                        ? _currentConfig.HealthCheckIntervalSec
                        : 30;
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendHeartbeatAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

        var status = ServiceStatus.ONLINE;
        var errorMsg = string.Empty;

        if (!string.IsNullOrEmpty(_lastGlobalError))
        {
            status = ServiceStatus.CRITICAL_ERROR;
            errorMsg = _lastGlobalError;
        }
        else if (_devices.Count > 0 && _failedDevicesCount == _devices.Count)
        {
            status = ServiceStatus.CRITICAL_ERROR;
            errorMsg = "ALL devices unreachable";
        }
        else if (_failedDevicesCount > 0)
        {
            status = ServiceStatus.DEGRADED;
            errorMsg = $"Unreachable: {_failedDevicesCount}/{_devices.Count}";
        }

        await repository.UpdateSystemStatusAsync(
            new SystemStatus
            {
                ServiceName = "ModbusCollector",
                Status = status,
                LastError = errorMsg,
                UptimeSeconds = (long)
                    (
                        DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                    ).TotalSeconds,
                LastSync = DateTime.UtcNow,
            }
        );
    }

    private async Task ReloadConfigurationAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

        _currentConfig = await repository.GetSystemConfigAsync();
        _devices = [.. await repository.GetActiveDevicesAsync()];

        var allSensors = await repository.GetSensorSettingsAsync();
        _sensorCache = allSensors
            .Where(s => s.DeviceId.HasValue)
            .GroupBy(s => s.DeviceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
