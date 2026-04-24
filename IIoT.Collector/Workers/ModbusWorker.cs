using System.Collections.Concurrent;
using System.Diagnostics;
using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IIoT.Collector.Workers;

/// <summary>
/// Основной фоновый сервис (Worker), управляющий циклом опроса Modbus-устройств.
/// Реализует логику сбора данных, буферизации, health-check'ов и обновления конфигурации.
/// </summary>
public class ModbusWorker(
    IServiceScopeFactory scopeFactory,
    IDeviceService deviceService,
    IProcessService processService,
    IModbusService modbusDriver,
    IBufferRepository buffer,
    IHostApplicationLifetime hostLifetime,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<ModbusWorker> logger
) : BackgroundService
{
    // ... (existing fields)
    private volatile int _failedDevicesCount = 0;
    private volatile string? _lastGlobalError = null;

    // Локальный кэш конфигурации
    private List<Device> _devices = [];
    private Dictionary<int, List<SensorSettings>> _sensorCache = [];

    // Кэш последних сохраненных значений для реализации Deadband (пороговой записи)
    // Key: SensorId, Value: (Value, Timestamp)
    private readonly ConcurrentDictionary<int, (double Value, DateTime Time)> _lastSavedValues =
        new();

    private volatile SystemConfig _currentConfig = new();

    /// <summary>
    /// Точка входа в фоновый процесс.
    /// Запускает параллельные задачи (Loops) для опроса, конфига, мониторинга и буфера.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Modbus Worker v3.5 (Real-time Config) started");

        // 1. Инициализация локального SQLite буфера
        await buffer.InitializeAsync();

        // 2. Первоначальная загрузка конфигурации с повторными попытками
        // Если БД недоступна при старте, сервис не запустится.
        if (!await TryInitializeConfigAsync(stoppingToken))
        {
            logger.LogCritical("Failed to load initial configuration. Stopping service.");
            hostLifetime.StopApplication();
            return;
        }

        // 3. Запуск независимых циклов обработки
        var pollTask = RunDynamicPollingLoop(stoppingToken); // Основной опрос устройств
        var configTask = RunConfigLoop(stoppingToken); // Периодическое обновление настроек (резервное)
        var listenerTask = RunConfigListenerLoop(stoppingToken); // Мгновенное обновление по NOTIFY
        var healthTask = RunHealthLoop(stoppingToken); // Отправка Heartbeat статуса в БД
        var bufferTask = RunBufferFlusherLoop(stoppingToken); // Фоновая выгрузка из буфера

        // 4. Ожидание завершения всех задач
        await Task.WhenAll(pollTask, configTask, listenerTask, healthTask, bufferTask);
    }

    /// <summary>
    /// Слушает канал 'config_changed' в Postgres для мгновенного перечитывания настроек.
    /// Это позволяет избежать ожидания в 60 секунд при изменении интервала опроса.
    /// </summary>
    private async Task RunConfigListenerLoop(CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("ADAMDB");
        if (string.IsNullOrEmpty(connectionString)) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);

                // Подписка на уведомление
                conn.Notification += async (o, e) =>
                {
                    logger.LogInformation("Real-time configuration change detected (Channel: {Channel})", e.Channel);
                    await ReloadConfigurationAsync();
                };

                using (var cmd = new Npgsql.NpgsqlCommand("LISTEN config_changed", conn))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                logger.LogInformation("Listening for real-time config changes...");

                while (!ct.IsCancellationRequested)
                {
                    // Ожидаем уведомления без блокировки потока
                    await conn.WaitAsync(ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Config listener connection lost. Retrying in 5s... Error: {Msg}", ex.Message);
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Пытается загрузить конфигурацию при старте с экспоненциальной задержкой (Backoff).
    /// </summary>
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

                delay = Math.Min(delay * 2, 30000); // Max delay 30s
            }
        }
        return false;
    }

    /// <summary>
    /// Цикл динамического опроса.
    /// Поддерживает изменение интервала опроса на лету без перезапуска сервиса.
    /// </summary>
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

                // Проверяем, изменился ли интервал в конфиге
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

    /// <summary>
    /// Выполняет один полный цикл опроса всех активных устройств.
    /// </summary>
    private async Task ProcessPollingCycle(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

            var allMetrics = new List<Metric>();

            // Параллельный опрос всех устройств
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
                    // Попытка сохранить в основную БД (TimescaleDB)
                    await repository.SaveMetricsAsync(allMetrics);
                    if (_lastGlobalError?.StartsWith("DB Error") == true)
                        _lastGlobalError = null;
                }
                catch (Exception ex)
                {
                    // Если основная БД недоступна — пишем в локальный буфер
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

    /// <summary>
    /// Читает данные с одного конкретного устройства (Аналоговые + Дискретные входы).
    /// </summary>
    private async Task<(bool Success, IEnumerable<Metric> Metrics)> ReadDeviceAsync(
        Device device,
        CancellationToken ct
    )
    {
        try
        {
            // Получаем соединение из пула
            var master = await deviceService.GetConnectionAsync(device, ct);
            if (master == null)
                return (false, []);

            // Чтение регистров
            var analogRaw = (await modbusDriver.ReadAnalogAsync(master, (byte)device.SlaveId)).ToList();
            var digitalRaw = (await modbusDriver.ReadDigitalAsync(master, (byte)device.SlaveId)).ToList();

            if (!_sensorCache.TryGetValue(device.Id, out var sensors))
                return (true, []);

            // Логируем только те порты, которые есть в конфиге (убираем шум)
            foreach (var a in analogRaw)
            {
                if (sensors.Any(s => s.PortNumber == a.Port && s.DataType == SensorDataType.ANALOG))
                {
                    logger.LogTrace("Device {Name}: Port {Port} raw ANALOG = {Val}", device.Name, a.Port, a.Value);
                }
            }
            foreach (var d in digitalRaw)
            {
                if (sensors.Any(s => s.PortNumber == d.Port && s.DataType == SensorDataType.DIGITAL))
                {
                    logger.LogTrace("Device {Name}: Port {Port} raw DIGITAL = {Val}", device.Name, d.Port, d.Value);
                }
            }

            // Преобразование сырых данных в метрики
            var rawMetrics = new List<Metric>();
            rawMetrics.AddRange(processService.ProcessAnalog(analogRaw, sensors));
            rawMetrics.AddRange(processService.ProcessDigital(digitalRaw, sensors));

            // Фильтрация (Deadband)
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

            if (filteredMetrics.Count > 0)
            {
                logger.LogInformation(
                    "Device {Name}: {Count} metrics passed filter and will be saved",
                    device.Name,
                    filteredMetrics.Count
                );
            }

            return (true, filteredMetrics);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Device {Name}: {Msg}", device.Name, ex.Message);
            // Сбрасываем соединение при ошибке ввода-вывода
            deviceService.InvalidateConnection(device.Id);
            return (false, []);
        }
    }

    /// <summary>
    /// Фоновый цикл, который периодически проверяет буфер и пытается отправить накопленные данные в основную БД.
    /// Работает, когда связь с БД восстанавливается.
    /// </summary>
    private async Task RunBufferFlusherLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Пауза между попытками сброса буфера
                await Task.Delay(5000, ct);

                var count = await buffer.CountAsync();
                if (count == 0)
                    continue;

                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDataRepository>();

                // Берем пачку данных (Peek), пробуем сохранить, затем удаляем (Remove)
                // Это гарантирует сохранность данных при ошибке в момент сохранения
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
                    // БД всё ещё недоступна, пробуем позже
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

    /// <summary>
    /// Логика Deadband (Зоны нечувствительности).
    /// Определяет, нужно ли сохранять метрику или она не изменилась достаточно сильно.
    /// </summary>
    private bool ShouldSaveMetric(Metric m, SensorSettings s)
    {
        // Если первое значение - сохраняем всегда
        if (!_lastSavedValues.TryGetValue(m.SensorId, out var last))
        {
            logger.LogDebug("Sensor {Id}: First value, saving.", m.SensorId);
            return true;
        }

        // Если прошло много времени (Heartbeat данных) - сохраняем принудительно
        if ((m.Time - last.Time).TotalSeconds >= _currentConfig.DataHeartbeatSec)
        {
            logger.LogDebug("Sensor {Id}: Heartbeat timeout, forcing save.", m.SensorId);
            return true;
        }

        if (s.DataType == SensorDataType.ANALOG)
        {
            // Проверка изменения на % от диапазона датчика
            var delta = Math.Abs(m.Value - last.Value);
            var range = Math.Abs(s.OutputMax - s.OutputMin);
            if (range < 0.0001)
                range = 100.0; // Дефолтный диапазон, если не задан

            var threshold = range * _currentConfig.DeadbandThreshold;
            var shouldSave = delta > threshold;

            if (!shouldSave)
            {
                logger.LogTrace("Sensor {Id}: Delta {Delta} <= Threshold {Thr}, skipping.", m.SensorId, delta, threshold);
            }

            return shouldSave;
        }

        if (s.DataType == SensorDataType.DIGITAL)
        {
            // Для дискретных сохраняем только изменение состояния (0->1 или 1->0)
            var changed = Math.Abs(m.Value - last.Value) > 0.5;
            if (!changed)
            {
                logger.LogTrace("Sensor {Id}: State not changed, skipping.", m.SensorId);
            }
            return changed;
        }

        return true;
    }

    /// <summary>
    /// Периодически обновляет конфигурацию из БД (новые устройства, изменившиеся уставки датчиков).
    /// </summary>
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

    /// <summary>
    /// Периодически обновляет статус сервиса (Alive) в БД.
    /// Позволяет мониторингу понять, что сборщик жив, даже если нет данных.
    /// </summary>
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

    /// <summary>
    /// Формирует и отправляет статус системы.
    /// </summary>
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
        // Группируем датчики по DeviceId для быстрого доступа в цикле опроса
        _sensorCache = allSensors
            .Where(s => s.DeviceId.HasValue)
            .GroupBy(s => s.DeviceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var device in _devices)
        {
            if (_sensorCache.TryGetValue(device.Id, out var sensors))
            {
                logger.LogInformation("Loaded {Count} sensors for device {Name}", sensors.Count, device.Name);
                foreach (var s in sensors)
                {
                    logger.LogInformation(" - Sensor {Id}: Port {Port} ({Type})", s.SensorId, s.PortNumber, s.DataType);
                }
            }
        }
    }
}
