using Dapper;
using Microsoft.Data.Sqlite;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using Serilog;

namespace ModbusClient.Repositories;

public class SqliteBufferRepository : IBufferRepository
{
    private const string ConnectionString = "Data Source=buffer.db";
    private readonly ILogger _logger = Log.ForContext<SqliteBufferRepository>();

    public async Task InitializeAsync()
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();

            // Создаем таблицу. Храним time как Ticks (long) для точности
            const string sql = @"
                CREATE TABLE IF NOT EXISTS buffered_metrics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    time_ticks INTEGER NOT NULL,
                    sensor_id INTEGER NOT NULL,
                    raw_value REAL,
                    value REAL NOT NULL
                );
                PRAGMA journal_mode = WAL; -- Ускоряет запись и конкурентный доступ
                PRAGMA synchronous = NORMAL;
            ";
            await conn.ExecuteAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to initialize SQLite buffer!");
        }
    }

    public async Task AddRangeAsync(IEnumerable<Metric> metrics)
    {
        var list = metrics.ToList();
        if (list.Count == 0) return;

        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            const string sql = @"
                INSERT INTO buffered_metrics (time_ticks, sensor_id, raw_value, value)
                VALUES (@TimeTicks, @SensorId, @RawValue, @Value)";

            await conn.ExecuteAsync(sql, list.Select(m => new
            {
                TimeTicks = m.Time.Ticks,
                m.SensorId,
                m.RawValue,
                m.Value
            }), transaction);

            await transaction.CommitAsync();
            _logger.Warning("Buffered {Count} metrics to local SQLite due to DB failure", list.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CRITICAL: Failed to write to local buffer!");
        }
    }

    public async Task<IEnumerable<Metric>> PeekAsync(int count)
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            const string sql = @"
                SELECT time_ticks, sensor_id, raw_value, value
                FROM buffered_metrics
                ORDER BY id ASC
                LIMIT @Count";

            var raw = await conn.QueryAsync(sql, new { Count = count });

            return raw.Select(r => new Metric
            {
                Time = new DateTime((long)r.time_ticks, DateTimeKind.Utc),
                SensorId = (int)r.sensor_id,
                RawValue = (double?)r.raw_value,
                Value = (double)r.value
            });
        }
        catch
        {
            return Enumerable.Empty<Metric>();
        }
    }

    public async Task RemoveOldestAsync(int count)
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            const string sql = @"
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
