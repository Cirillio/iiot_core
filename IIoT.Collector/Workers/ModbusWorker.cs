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
    private Dictionary<int, List<TagSettings>> _tagCache = [];
    private Dictionary<int, ModbusConnection> _connectionsById = [];
    private Dictionary<int, TagSettings> _tagsById = [];

    // Кэш последних сохраненных значений для реализации Deadband (пороговой записи)
    // Key: TagId, Value: (Value, Timestamp)
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
        var commandTask = RunCommandListenerLoop(stoppingToken); // Приём команд управления (Supervisory Control)
        var healthTask = RunHealthLoop(stoppingToken); // Отправка Heartbeat статуса в БД
        var bufferTask = RunBufferFlusherLoop(stoppingToken); // Фоновая выгрузка из буфера

        // 4. Ожидание завершения всех задач
        await Task.WhenAll(pollTask, configTask, listenerTask, commandTask, healthTask, bufferTask);
    }

    /// <summary>
    /// Слушает канал 'config_changed' в Postgres для мгновенного перечитывания настроек.
    /// Это позволяет избежать ожидания в 60 секунд при изменении интервала опроса.
    /// </summary>
    private async Task RunConfigListenerLoop(CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("ADAMDB");
        if (string.IsNullOrEmpty(connectionString))
            return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);

                // Подписка на уведомление
                conn.Notification += async (o, e) =>
                {
                    logger.LogInformation(
                        "Real-time configuration change detected (Channel: {Channel})",
                        e.Channel
                    );
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Config listener connection lost. Retrying in 5s... Error: {Msg}",
                    ex.Message
                );
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Слушает канал 'control_commands' в Postgres. Заменяет polling: при INSERT новой команды
    /// БД присылает UUID, коллектор вычитывает команду и исполняет её приоритетно.
    /// </summary>
    private async Task RunCommandListenerLoop(CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("ADAMDB");
        if (string.IsNullOrEmpty(connectionString))
            return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var conn = new Npgsql.NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);

                conn.Notification += async (o, e) =>
                {
                    if (!Guid.TryParse(e.Payload, out var commandId))
                    {
                        logger.LogWarning("Received malformed command id: {Payload}", e.Payload);
                        return;
                    }

                    logger.LogInformation("Control command received: {CommandId}", commandId);
                    await ExecuteCommandAsync(commandId, ct);
                };

                using (var cmd = new Npgsql.NpgsqlCommand("LISTEN control_commands", conn))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                logger.LogInformation("Listening for control commands...");

                while (!ct.IsCancellationRequested)
                {
                    await conn.WaitAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Command listener connection lost. Retrying in 5s... Error: {Msg}",
                    ex.Message
                );
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Исполняет команду управления: захватывает шину, пишет в Modbus, квитирует статус
    /// и публикует фиктивную метрику с новым значением (мгновенный фидбэк телеметрии).
    /// </summary>
    private async Task ExecuteCommandAsync(Guid commandId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var commandRepo = scope.ServiceProvider.GetRequiredService<ICommandRepository>();
        var dataRepo = scope.ServiceProvider.GetRequiredService<IDataRepository>();

        var command = await commandRepo.GetByIdAsync(commandId);
        if (command == null)
        {
            logger.LogWarning("Command {Id} not found in DB", commandId);
            return;
        }

        // Резолв тега и устройства из локального кэша конфигурации.
        if (!_tagsById.TryGetValue(command.TagId, out var tag) || !tag.DeviceId.HasValue)
        {
            await commandRepo.UpdateStatusAsync(
                commandId,
                CommandStatus.Failed,
                $"Tag {command.TagId} not found or has no device binding."
            );
            return;
        }

        // Защита ядра (defense-in-depth): запись только в Coil/Holding, даже если API пропустил.
        if (
            tag.RegisterType
            is ModbusRegisterType.DiscreteInput
                or ModbusRegisterType.InputRegister
        )
        {
            await commandRepo.UpdateStatusAsync(
                commandId,
                CommandStatus.Failed,
                $"Register type {tag.RegisterType} is Read-Only."
            );
            return;
        }

        var device = _devices.FirstOrDefault(d => d.Id == tag.DeviceId.Value);
        if (device == null || !_connectionsById.TryGetValue(device.ConnectionId, out var connection))
        {
            await commandRepo.UpdateStatusAsync(
                commandId,
                CommandStatus.Failed,
                "Target device or connection is not in active configuration."
            );
            return;
        }

        // Сигнализируем старт обработки (UPDATE триггерит уведомление в АРМ).
        await commandRepo.UpdateStatusAsync(commandId, CommandStatus.Processing);

        // Захват физического канала: дожидаемся завершения текущего цикла опроса этого шлюза.
        var gate = deviceService.GetLock(device.ConnectionId);
        await gate.WaitAsync(ct);
        try
        {
            var master = await deviceService.GetConnectionAsync(connection, ct);
            if (master == null)
            {
                await commandRepo.UpdateStatusAsync(
                    commandId,
                    CommandStatus.Failed,
                    $"Unable to (re)connect to gateway {connection.IpAddress}:{connection.Port}."
                );
                return;
            }

            // Физическая транзакция записи (FC05 / FC06 / FC16).
            await modbusDriver.WriteTagAsync(master, (byte)device.SlaveId, tag, command.Value, ct);

            await commandRepo.UpdateStatusAsync(commandId, CommandStatus.Success);

            // Мгновенный фидбэк: фиктивная метрика с новым значением → телеметрия без ожидания опроса.
            var metric = new Metric
            {
                Time = DateTime.UtcNow,
                TagId = tag.TagId,
                RawValue = command.Value,
                Value = TagExtensions.Calculate(command.Value, tag),
            };
            try
            {
                await dataRepo.SaveMetricsAsync([metric]);
                _lastSavedValues[tag.TagId] = (metric.Value, metric.Time);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Command {Id} written, but synthetic metric failed: {Msg}", commandId, ex.Message);
            }

            logger.LogInformation(
                "Command {Id} executed: tag {Tag} <= {Value}",
                commandId,
                tag.TagId,
                command.Value
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Command {Id} failed ({Type}): {Msg}",
                commandId,
                ex.GetType().Name,
                ex.Message
            );

            await commandRepo.UpdateStatusAsync(
                commandId,
                CommandStatus.Failed,
                $"{ex.GetType().Name}: {ex.Message}"
            );

            // Транспортный сбой — сбрасываем сокет, чтобы опрос переподключился.
            if (Services.ModbusErrorPolicy.IsTransport(ex))
                deviceService.InvalidateConnection(device.ConnectionId);
        }
        finally
        {
            gate.Release();
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
    /// Читает данные с одного конкретного устройства по всем типам регистров.
    /// </summary>
    private async Task<(bool Success, IEnumerable<Metric> Metrics)> ReadDeviceAsync(
        Device device,
        CancellationToken ct
    )
    {
        // Резолвим физическое соединение устройства
        if (!_connectionsById.TryGetValue(device.ConnectionId, out var connection))
        {
            logger.LogWarning(
                "Device {Name}: connection {ConnId} not found in config",
                device.Name,
                device.ConnectionId
            );
            return (false, []);
        }

        if (!_tagCache.TryGetValue(device.Id, out var tags))
            return (true, []);

        // Сериализуем доступ к TCP-сессии: устройства за одним шлюзом опрашиваются по очереди
        var gate = deviceService.GetLock(device.ConnectionId);
        await gate.WaitAsync(ct);
        try
        {
            // Получаем соединение из пула (по ConnectionId)
            var master = await deviceService.GetConnectionAsync(connection, ct);
            if (master == null)
                return (false, []);

            var allMetrics = new List<Metric>();

            // Опрашиваем каждую группу регистров
            foreach (var registerType in Enum.GetValues<ModbusRegisterType>())
            {
                var tagsOfType = tags.Where(s => s.RegisterType == registerType).ToList();
                if (tagsOfType.Count == 0)
                    continue;

                // Битовые таблицы (Coil/DiscreteInput) ограничиваем отдельным MaxBitSpan,
                // иначе MaxRegisterSpan (настроенный под регистры) дробит чтение битов на мелкие пакеты.
                bool isBitTable =
                    registerType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput;
                int maxSpan = isBitTable ? device.MaxBitSpan : device.MaxRegisterSpan;

                try
                {
                    var rawData = await modbusDriver.ReadRegistersAsync(
                        master,
                        (byte)device.SlaveId,
                        tagsOfType,
                        registerType,
                        maxSpan,
                        device.UseGroupPolling,
                        ct
                    );

                    allMetrics.AddRange(processService.Process(rawData, tags));
                }
                catch (Exception ex) when (Services.ModbusErrorPolicy.IsTransport(ex))
                {
                    // Транспортный сбой — пробрасываем во внешний catch для сброса соединения.
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        "Device {Name}: Failed to read {Type}: {Msg}",
                        device.Name,
                        registerType,
                        ex.Message
                    );
                }
            }

            // Фильтрация (Deadband)
            var filteredMetrics = new List<Metric>();
            foreach (var m in allMetrics)
            {
                var setting = tags.FirstOrDefault(s => s.TagId == m.TagId);
                if (setting != null && ShouldSaveMetric(m, setting))
                {
                    filteredMetrics.Add(m);
                    _lastSavedValues[m.TagId] = (m.Value, m.Time);
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
            deviceService.InvalidateConnection(device.ConnectionId);
            return (false, []);
        }
        finally
        {
            gate.Release();
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
    private bool ShouldSaveMetric(Metric m, TagSettings s)
    {
        // Если первое значение - сохраняем всегда
        if (!_lastSavedValues.TryGetValue(m.TagId, out var last))
        {
            logger.LogDebug("Tag {Id}: First value, saving.", m.TagId);
            return true;
        }

        // Если прошло много времени (Heartbeat данных) - сохраняем принудительно
        if ((m.Time - last.Time).TotalSeconds >= _currentConfig.DataHeartbeatSec)
        {
            logger.LogDebug("Tag {Id}: Heartbeat timeout, forcing save.", m.TagId);
            return true;
        }

        if (s.DataType is TagDataType.AnalogRaw or TagDataType.AnalogPhysical)
        {
            // Проверка изменения на % от диапазона тега
            var delta = Math.Abs(m.Value - last.Value);
            var range = Math.Abs(s.OutputMax - s.OutputMin);
            if (range < 0.0001)
                range = 100.0; // Дефолтный диапазон, если не задан

            // Индивидуальный порог тега имеет приоритет над глобальным
            var deadband = s.DeadbandThreshold ?? _currentConfig.DeadbandThreshold;
            var threshold = range * deadband;
            var shouldSave = delta > threshold;

            if (!shouldSave)
            {
                logger.LogTrace(
                    "Tag {Id}: Delta {Delta} <= Threshold {Thr}, skipping.",
                    m.TagId,
                    delta,
                    threshold
                );
            }

            return shouldSave;
        }

        if (s.DataType == TagDataType.Digital)
        {
            // Для дискретных сохраняем только изменение состояния (0->1 или 1->0)
            var changed = Math.Abs(m.Value - last.Value) > 0.5;
            if (!changed)
            {
                logger.LogTrace("Tag {Id}: State not changed, skipping.", m.TagId);
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

        var status = ServiceStatus.Online;
        var errorMsg = string.Empty;

        if (!string.IsNullOrEmpty(_lastGlobalError))
        {
            status = ServiceStatus.CriticalError;
            errorMsg = _lastGlobalError;
        }
        else if (_devices.Count > 0 && _failedDevicesCount == _devices.Count)
        {
            status = ServiceStatus.CriticalError;
            errorMsg = "ALL devices unreachable";
        }
        else if (_failedDevicesCount > 0)
        {
            status = ServiceStatus.Degraded;
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

        var connections = await repository.GetConnectionsAsync();
        _connectionsById = connections.ToDictionary(c => c.Id);

        var allTags = await repository.GetTagSettingsAsync();
        // Группируем теги по DeviceId для быстрого доступа в цикле опроса
        _tagCache = allTags
            .Where(s => s.DeviceId.HasValue)
            .GroupBy(s => s.DeviceId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Плоский индекс по TagId — нужен обработчику команд для резолва тега за O(1).
        _tagsById = allTags.ToDictionary(s => s.TagId);

        foreach (var device in _devices)
        {
            if (_tagCache.TryGetValue(device.Id, out var tags))
            {
                logger.LogInformation(
                    "Loaded {Count} tags for device {Name}",
                    tags.Count,
                    device.Name
                );
                foreach (var s in tags)
                {
                    logger.LogInformation(
                        " - Tag {Id}: Port {Port} ({Type})",
                        s.TagId,
                        s.PortNumber,
                        s.DataType
                    );
                }
            }
        }
    }
}
