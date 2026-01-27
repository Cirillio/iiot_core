using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModbusClient.Interfaces;
using Serilog;

namespace ModbusClient.Workers;

/// <summary>
/// Background service for cyclic Modbus polling and data persistence.
/// </summary>
public class ModbusWorker(
    IModbusService modbus,
    IDataRepository repository,
    IConfiguration configuration
) : BackgroundService
{
    private readonly IModbusService _modbus = modbus;
    private readonly IDataRepository _repository = repository;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger _logger = Log.ForContext<ModbusWorker>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string adamIp = _configuration["ModbusSettings:IpAddress"] ?? "192.168.1.50";
        int adamPort = int.TryParse(_configuration["ModbusSettings:Port"], out var p) ? p : 502;

        _logger.Information("Worker started. Target ADAM: {IP}:{Port}", adamIp, adamPort);

        // Попытка первичной инициализации
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _modbus.InitializeAsync(adamIp, adamPort, stoppingToken))
                break;

            _logger.Warning("Retrying connection in 5 seconds...");
            await Task.Delay(5000, stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.Debug("Polling tick started...");

                // 1. Сбор данных
                var analogData = await _modbus.ReadAllAnalogAsync(stoppingToken);
                var digitalData = await _modbus.ReadAllDigitalAsync(stoppingToken);

                // 2. Сохранение (запускаем параллельно для скорости)
                var saveTasks = new List<Task>
                {
                    _repository.SaveRawMeasurementsAsync(analogData),
                    _repository.SaveDigitalMeasurementsAsync(digitalData),
                };

                await Task.WhenAll(saveTasks);

                _logger.Debug("Data successfully saved to DB");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during polling cycle. Checking connection...");

                // Если соединение разорвано, пробуем переподключиться
                await _modbus.DisconnectAsync();
                await _modbus.InitializeAsync(adamIp, adamPort, stoppingToken);
            }
        }

        await _modbus.DisconnectAsync();
        _logger.Information("Worker stopped gracefully");
    }
}
