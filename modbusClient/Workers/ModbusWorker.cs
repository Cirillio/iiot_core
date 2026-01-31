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
    ILogger<ModbusWorker> logger
) : BackgroundService
{
    // ... (existing fields)

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Modbus Worker v3.2 (Resilient Startup) started");

        // 1. Initial Load with Retry Policy
        // Если БД недоступна (например, при старте docker-compose), мы ждем.
        // Если недоступна долго — падаем, чтобы Docker перезапустил нас.
        if (!await TryInitializeConfigAsync(stoppingToken))
        {
            logger.LogCritical(
                "Failed to load initial configuration after multiple attempts. Stopping service."
            );
            Environment.Exit(1); // Exit with a non-zero code to indicate failure
            return;
        }

        // 2. Start Loops
        var pollTask = RunDynamicPollingLoop(stoppingToken);
        var configTask = RunConfigLoop(stoppingToken);
        var healthTask = RunHealthLoop(stoppingToken);

        await Task.WhenAll(pollTask, configTask, healthTask);
    }

    private async Task<bool> TryInitializeConfigAsync(CancellationToken ct)
    {
        const int maxRetries = 10;
        int delay = 2000; // Начинаем с 2 секунд

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

                delay = Math.Min(delay * 2, 30000); // Экспоненциальный Backoff до 30 сек
            }
        }
        return false;
    }

    // State
    private volatile int _failedDevicesCount = 0;
    private volatile string? _lastGlobalError = null;

    // Caches
    private List<Device> _devices = [];
    private Dictionary<int, List<SensorSettings>> _sensorCache = [];

    // Active Config (Thread-safe)
    private volatile SystemConfig _currentConfig = new();

    private async Task RunDynamicPollingLoop(CancellationToken ct)
    {
        var currentInterval = _currentConfig.PollingIntervalMs;
        // Protection against zero or negative interval
        if (currentInterval <= 0)
            currentInterval = 1000;

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(currentInterval));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(ct);

                // Check for config change
                if (
                    _currentConfig.PollingIntervalMs != currentInterval
                    && _currentConfig.PollingIntervalMs > 0
                )
                {
                    currentInterval = _currentConfig.PollingIntervalMs;
                    timer.Dispose();
                    timer = new PeriodicTimer(TimeSpan.FromMilliseconds(currentInterval));
                    logger.LogInformation("Polling interval changed to {Ms}ms", currentInterval);
                }

                if (_devices.Count == 0)
                    continue;

                // Execute Cycle
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
            // We need a scope here to access DataRepository for SAVING metrics
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

            var tasks = _devices.Select(d => ProcessDeviceCycleAsync(d, repository, ct));
            var results = await Task.WhenAll(tasks);

            _failedDevicesCount = results.Count(success => !success);

            if (_lastGlobalError?.StartsWith("Polling loop") == true)
                _lastGlobalError = null;
        }
        catch (Exception ex)
        {
            _lastGlobalError = $"Polling loop critical: {ex.Message}";
            logger.LogError(ex, "Critical error in global polling loop");
        }
    }

    private async Task<bool> ProcessDeviceCycleAsync(
        Device device,
        IDataRepository repository,
        CancellationToken ct
    )
    {
        try
        {
            var master = await deviceService.GetConnectionAsync(device, ct);
            if (master == null)
                return false;

            // Reading (using shared Modbus Driver)
            var analogRaw = await modbusDriver.ReadAnalogAsync(master);
            var digitalRaw = await modbusDriver.ReadDigitalAsync(master);

            if (!_sensorCache.TryGetValue(device.Id, out var sensors))
                return true;

            // Processing
            var metrics = new List<Metric>();
            metrics.AddRange(readingService.ProcessAnalog(analogRaw, sensors));
            metrics.AddRange(readingService.ProcessDigital(digitalRaw, sensors));

            // Saving
            if (metrics.Count > 0)
            {
                await repository.SaveMetricsAsync(metrics);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error polling device {Name}: {Msg}", device.Name, ex.Message);
            deviceService.InvalidateConnection(device.Id);
            return false;
        }
    }

    private async Task RunConfigLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var delaySec = _currentConfig.ConfigReloadIntervalSec;
                if (delaySec <= 0)
                    delaySec = 60;

                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                await ReloadConfigurationAsync();
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
                var delaySec = _currentConfig.HealthCheckIntervalSec;
                if (delaySec <= 0)
                    delaySec = 30;

                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);

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

                var uptime =
                    DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

                await repository.UpdateSystemStatusAsync(
                    new SystemStatus
                    {
                        ServiceName = "ModbusCollector",
                        Status = status,
                        LastError = errorMsg,
                        UptimeSeconds = (long)uptime.TotalSeconds,
                        LastSync = DateTime.UtcNow,
                    }
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send heartbeat");
            }
        }
    }

    private async Task ReloadConfigurationAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

        // 1. Load System Config
        var sysConfig = await repository.GetSystemConfigAsync();
        _currentConfig = sysConfig;

        // 2. Load Devices & Sensors
        var newDevices = await repository.GetActiveDevicesAsync();
        _devices = [.. newDevices];

        var allSensors = await repository.GetSensorSettingsAsync();
        _sensorCache = allSensors
            .Where(s => s.DeviceId.HasValue)
            .GroupBy(s => s.DeviceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        logger.LogDebug("Configuration reloaded. Interval: {Int}ms", sysConfig.PollingIntervalMs);
    }
}
