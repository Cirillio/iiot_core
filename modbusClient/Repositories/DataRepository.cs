using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace ModbusClient.Repositories;

/// <summary>
/// Репозиторий для взаимодействия с базой данных (TimescaleDB + PostgreSQL).
/// Реализует методы для сохранения измерений и управления настройками датчиков.
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger = Log.ForContext<DataRepository>();

    public DataRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("ADAMDB")
            ?? throw new ArgumentNullException("Database connection string 'ADAMDB' is missing");

        // Позволяет Dapper автоматически маппить snake_case из БД в PascalCase свойства C#
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Высокопроизводительная вставка метрик через PostgreSQL Binary COPY.
    /// Идеально для TimescaleDB и больших объемов данных.
    /// </summary>
    public async Task SaveMetricsAsync(IEnumerable<Metric> metrics)
    {
        var metricsList = metrics.ToList();
        if (metricsList.Count == 0)
            return;

        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            // COPY protocol - самый быстрый способ вставки в Postgres
            using var writer = await conn.BeginBinaryImportAsync(
                "COPY metrics (time, sensor_id, raw_value, value) FROM STDIN (FORMAT BINARY)"
            );

            foreach (var m in metricsList)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(m.Time, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(m.SensorId, NpgsqlDbType.Integer);

                if (m.RawValue.HasValue)
                    await writer.WriteAsync(m.RawValue.Value, NpgsqlDbType.Double);
                else
                    await writer.WriteNullAsync();

                await writer.WriteAsync(m.Value, NpgsqlDbType.Double);
            }

            await writer.CompleteAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to bulk insert {Count} metrics into TimescaleDB",
                metricsList.Count
            );
            throw; // Пробрасываем исключение для обработки Retry Policy в вызывающем коде
        }
    }

    /// <summary>
    /// Обновляет статус сервиса (Heartbeat).
    /// </summary>
    public async Task UpdateSystemStatusAsync(SystemStatus status)
    {
        const string sql =
            @"
            INSERT INTO system_status (service_name, status, uptime_seconds, last_error, last_sync)
            VALUES (@ServiceName, @Status::system_service_status, @UptimeSeconds, @LastError, @LastSync)
            ON CONFLICT (service_name) DO UPDATE SET
                status = EXCLUDED.status,
                uptime_seconds = EXCLUDED.uptime_seconds,
                last_error = EXCLUDED.last_error,
                last_sync = EXCLUDED.last_sync;
        ";

        try
        {
            using var conn = CreateConnection();
            await conn.ExecuteAsync(
                sql,
                new
                {
                    status.ServiceName,
                    Status = status.Status.ToString(), // Enum -> String для корректного каста в SQL
                    status.UptimeSeconds,
                    status.LastError,
                    status.LastSync,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update heartbeat for {Service}", status.ServiceName);
        }
    }

    /// <summary>
    /// Получает список активных контроллеров для опроса.
    /// </summary>
    public async Task<IEnumerable<Device>> GetActiveDevicesAsync()
    {
        const string sql =
            @"
            SELECT id, name, ip_address, port, is_active, created_at
            FROM devices
            WHERE is_active = true";

        try
        {
            using var conn = CreateConnection();
            return await conn.QueryAsync<Device>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load active devices from DB");
            return [];
        }
    }

    /// <summary>
    /// Получает настройки всех датчиков для маппинга (Device+Port -> SensorID) и калибровки.
    /// </summary>
    public async Task<IEnumerable<SensorSettings>> GetSensorSettingsAsync()
    {
        const string sql =
            @"
            SELECT 
                sensor_id, 
                device_id, 
                port_number, 
                name, 
                slug, 
                unit, 
                input_min, input_max, 
                output_min, output_max, 
                offset_val, 
                formula, 
                ui_config as UiConfigJson,
                updated_at
                -- data_type пока пропускаем или требуем TypeHandler, если Dapper не сможет скастить
            FROM sensor_settings
            WHERE device_id IS NOT NULL";

        try
        {
            using var conn = CreateConnection();
            // ВАЖНО: Dapper по умолчанию не умеет маппить Postgres ENUM в C# ENUM без регистрации обработчика.
            // Для упрощения сейчас мы можем читать data_type отдельно или зарегистрировать маппер.
            // В данном коде мы полагаемся на то, что SensorDataType имеет дефолтное значение ANALOG,
            // либо нужно добавить NpgsqlDataSourceBuilder.MapEnum<SensorDataType>() при старте.

            var settings = await conn.QueryAsync<SensorSettings>(sql);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load sensor settings from DB");
            return [];
        }
    }

    public async Task<SystemConfig> GetSystemConfigAsync()
    {
        const string sql =
            @"
              SELECT 
                  id, 
                  raw_retention_days, 
                  agg_retention_days, 
                  polling_interval_ms,
                  config_reload_interval_sec,
                  health_check_interval_sec,
                  updated_at
              FROM system_config
              LIMIT 1";

        try
        {
            using var conn = CreateConnection();
            var config = await conn.QueryFirstOrDefaultAsync<SystemConfig>(sql);

            // Если конфига нет в БД, возвращаем дефолтный (чтобы сервис не падал)
            return config ?? new SystemConfig();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load System Config. Using defaults.");
            return new SystemConfig();
        }
    }
}
