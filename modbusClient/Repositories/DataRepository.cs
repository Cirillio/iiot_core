using Dapper;
using Microsoft.Extensions.Configuration;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using Npgsql;
using Serilog;

namespace ModbusClient.Repositories;

/// <summary>
/// Репозиторий для взаимодействия с базой данных.
/// Реализует методы для сохранения измерений и управления настройками датчиков.
/// </summary>
/// <param name="configuration">Конфигурация приложения для доступа к строке подключения.</param>
public class DataRepository(IConfiguration configuration) : IDataRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("ADAMDB")
        ?? throw new ArgumentNullException("Database connection string is missing");
    private readonly ILogger _logger = Log.ForContext<DataRepository>();

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Асинхронно сохраняет пакет сырых (аналоговых) измерений в базу данных.
    /// </summary>
    /// <param name="measurements">Коллекция измерений <see cref="RawMeasurement"/> для вставки.</param>
    public async Task SaveRawMeasurementsAsync(IEnumerable<RawMeasurement> measurements)
    {
        const string sql =
            @"
            INSERT INTO raw_data_logs (port_id, raw_value, created_at)
            VALUES (@PortId, @RawValue, @CreatedAt)";

        try
        {
            using var connection = CreateConnection();
            // Dapper автоматически выполнит запрос для каждого элемента коллекции
            await connection.ExecuteAsync(sql, measurements);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error bulk inserting analog measurements to DB");
        }
    }

    /// <summary>
    /// Асинхронно сохраняет пакет цифровых (дискретных) измерений в базу данных.
    /// </summary>
    /// <param name="measurements">Коллекция измерений <see cref="DigitalMeasurement"/> для вставки.</param>
    public async Task SaveDigitalMeasurementsAsync(IEnumerable<DigitalMeasurement> measurements)
    {
        const string sql =
            @"
            INSERT INTO digital_data_logs (port_id, value, created_at)
            VALUES (@PortId, @Value, @CreatedAt)";

        try
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, measurements);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting digital measurements to DB");
        }
    }

    /// <summary>
    /// Получает список всех конфигураций датчиков из базы данных.
    /// </summary>
    /// <returns>Коллекция <see cref="SensorConfig"/>, содержащая настройки для каждого порта.</returns>
    public async Task<IEnumerable<SensorConfig>> GetSensorConfigsAsync()
    {
        const string sql =
            @"
            SELECT 
                port_id as PortId, 
                sensor_name as SensorName, 
                unit as Unit, 
                input_min as InputMin, 
                input_max as InputMax, 
                output_min as OutputMin, 
                output_max as OutputMax, 
                offset_val as OffsetVal 
            FROM sensor_settings 
            ORDER BY port_id";

        try
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<SensorConfig>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch sensor configurations");
            return [];
        }
    }

    /// <summary>
    /// Обновляет или вставляет конфигурацию для конкретного датчика.
    /// Если запись для указанного port_id уже существует, она будет обновлена.
    /// В противном случае будет создана новая запись.
    /// </summary>
    /// <param name="config">Объект конфигурации <see cref="SensorConfig"/> с обновленными данными.</param>
    public async Task UpdateSensorConfigAsync(SensorConfig config)
    {
        const string sql =
            @"
            INSERT INTO sensor_settings (port_id, sensor_name, unit, input_min, input_max, output_min, output_max, offset_val, updated_at)
            VALUES (@PortId, @SensorName, @Unit, @InputMin, @InputMax, @OutputMin, @OutputMax, @OffsetVal, CURRENT_TIMESTAMP)
            ON CONFLICT (port_id) DO UPDATE SET
                sensor_name = EXCLUDED.sensor_name,
                unit = EXCLUDED.unit,
                input_min = EXCLUDED.input_min,
                input_max = EXCLUDED.input_max,
                output_min = EXCLUDED.output_min,
                output_max = EXCLUDED.output_max,
                offset_val = EXCLUDED.offset_val,
                updated_at = CURRENT_TIMESTAMP";

        try
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, config);
            _logger.Information("Sensor configuration updated for Port {PortId}", config.PortId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update sensor config for Port {PortId}", config.PortId);
        }
    }

    /// <summary>
    /// Обновляет статус системы в логах БД.
    /// </summary>
    /// <param name="status">Статус системы.</param>
    public async Task UpdateSystemStatusAsync(SystemStatus status)
    {
        const string sql =
            @"
            INSERT INTO system_status_logs (is_connected, last_error, last_sync)
            VALUES (@IsConnected, @LastError, @LastSync)";

        try
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, status);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update system status");
        }
    }
}
