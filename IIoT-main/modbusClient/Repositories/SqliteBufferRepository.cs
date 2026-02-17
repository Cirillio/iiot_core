using Dapper;
using Microsoft.Data.Sqlite;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using Serilog;

namespace ModbusClient.Repositories;

/// <summary>
/// Реализация локального буфера на основе SQLite.
/// Обеспечивает надежное хранение данных при потере связи с основной БД.
/// </summary>
public class SqliteBufferRepository : IBufferRepository
{
    private const string ConnectionString = "Data Source=buffer.db";
    private readonly ILogger _logger = Log.ForContext<SqliteBufferRepository>();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();

            // Создаем таблицу, если её нет.
            // time_ticks: храним DateTime.Ticks (long), так как SQLite не имеет нативного типа DateTime с высокой точностью.
            // WAL mode: Write-Ahead Logging для повышения производительности записи и конкурентности.
            const string sql =
                @"
                CREATE TABLE IF NOT EXISTS buffered_metrics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    time_ticks INTEGER NOT NULL,
                    sensor_id INTEGER NOT NULL,
                    raw_value REAL,
                    value REAL NOT NULL
                );
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
            ";
            await conn.ExecuteAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to initialize SQLite buffer!");
        }
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<Metric> metrics)
    {
        var list = metrics.ToList();
        if (list.Count == 0)
            return;

        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            const string sql =
                @"
                INSERT INTO buffered_metrics (time_ticks, sensor_id, raw_value, value)
                VALUES (@TimeTicks, @SensorId, @RawValue, @Value)";

            // Используем транзакцию для массовой вставки (значительно быстрее)
            await conn.ExecuteAsync(
                sql,
                list.Select(m => new
                {
                    TimeTicks = m.Time.Ticks,
                    m.SensorId,
                    m.RawValue,
                    m.Value,
                }),
                transaction
            );

            await transaction.CommitAsync();
            _logger.Warning(
                "Buffered {Count} metrics to local SQLite due to DB failure",
                list.Count
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CRITICAL: Failed to write to local buffer!");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Metric>> PeekAsync(int count)
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            // Выбираем N самых старых записей
            const string sql =
                @"
                SELECT time_ticks, sensor_id, raw_value, value
                FROM buffered_metrics
                ORDER BY id ASC
                LIMIT @Count";

            var raw = await conn.QueryAsync(sql, new { Count = count });

            // Маппим обратно в модель Metric
            return raw.Select(r => new Metric
            {
                Time = new DateTime((long)r.time_ticks, DateTimeKind.Utc),
                SensorId = (int)r.sensor_id,
                RawValue = (double?)r.raw_value,
                Value = (double)r.value,
            });
        }
        catch
        {
            return Enumerable.Empty<Metric>();
        }
    }

    /// <inheritdoc />
    public async Task RemoveOldestAsync(int count)
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            // Удаляем те же N записей (используем подзапрос с LIMIT)
            const string sql =
                @"
                DELETE FROM buffered_metrics 
                WHERE id IN (
                    SELECT id FROM buffered_metrics ORDER BY id ASC LIMIT @Count
                )";
            await conn.ExecuteAsync(sql, new { Count = count });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to clear buffer");
        }
    }

    /// <inheritdoc />
    public async Task<long> CountAsync()
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            return await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM buffered_metrics");
        }
        catch
        {
            return 0;
        }
    }
}
