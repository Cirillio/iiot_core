using System.Data;
using Dapper;
using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace IIoT.Collector.Repositories;

/// <summary>
/// Репозиторий для взаимодействия с основной базой данных (PostgreSQL + TimescaleDB).
/// Отвечает за сохранение метрик и загрузку конфигурации.
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger = Log.ForContext<DataRepository>();

    /// <summary>
    /// Инициализирует новый экземпляр репозитория.
    /// </summary>
    /// <param name="configuration">Конфигурация приложения для доступа к строке подключения 'ADAMDB'.</param>
    /// <exception cref="ArgumentNullException">Если строка подключения не найдена.</exception>
    public DataRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("ADAMDB")
            ?? throw new ArgumentNullException("Database connection string 'ADAMDB' is missing");

        // Глобальная настройка Dapper: маппинг snake_case (в БД) в PascalCase (в C#)
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    /// <summary>
    /// Создает новое подключение к PostgreSQL.
    /// </summary>
    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <inheritdoc />
    /// <remarks>
    /// Использует протокол PostgreSQL Binary COPY для максимальной производительности вставки.
    /// Это значительно быстрее обычных INSERT запросов, особенно для TimescaleDB.
    /// </remarks>
    public async Task SaveMetricsAsync(IEnumerable<Metric> metrics)
    {
        var metricsList = metrics.ToList();
        if (metricsList.Count == 0)
            return;

        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            // COPY protocol - прямой поток бинарных данных в таблицу
            using var writer = await conn.BeginBinaryImportAsync(
                "COPY metrics (time, sensor_id, raw_value, value) FROM STDIN (FORMAT BINARY)"
            );

            foreach (var m in metricsList)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(m.Time, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(m.SensorId, NpgsqlDbType.Integer);

                // RawValue может быть NULL
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
            // Пробрасываем исключение, чтобы вызывающий код (Worker) мог задействовать буфер
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateSystemStatusAsync(SystemStatus status)
    {
        // Upsert (INSERT ON CONFLICT UPDATE) логика для обновления статуса
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
                    Status = status.Status.ToString(), // Enum -> String для корректного каста в Postgres enum type
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
                data_type::text as DataType,
                unit, 
                input_min, input_max, 
                output_min, output_max, 
                offset_val, 
                formula, 
                ui_config as UiConfigJson,
                updated_at
            FROM sensor_settings
            WHERE device_id IS NOT NULL";

        try
        {
            using var conn = CreateConnection();
            var settings = await conn.QueryAsync<SensorSettings>(sql);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load sensor settings from DB");
            return [];
        }
    }

    /// <inheritdoc />
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
                  COALESCE(deadband_threshold, 0.01) as DeadbandThreshold,
                  COALESCE(data_heartbeat_sec, 600) as DataHeartbearSec,
                  updated_at
              FROM system_config
              LIMIT 1";

        try
        {
            using var conn = CreateConnection();
            var config = await conn.QueryFirstOrDefaultAsync<SystemConfig>(sql);

            // Если конфига нет в БД (таблица пуста), возвращаем дефолтный объект с настройками по умолчанию
            return config ?? new SystemConfig();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load System Config. Using defaults.");
            return new SystemConfig();
        }
    }
}
