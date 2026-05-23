using System.Text.RegularExpressions;
using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;
using Serilog;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Репозиторий для управления системными конфигурациями и мониторинга состояния сервисов.
/// </summary>
public class SystemRepository(DapperContext context) : ISystemRepository
{
    private readonly DapperContext _context = context;
    private readonly Serilog.ILogger _logger = Log.ForContext<SystemRepository>();

    /// <inheritdoc />
    public async Task<SystemConfig> GetConfigAsync()
    {
        const string sql = "SELECT * FROM system_config LIMIT 1";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<SystemConfig>(sql);
    }

    /// <inheritdoc />
    public async Task UpdateConfigAsync(SystemConfig config)
    {
        const string sql =
            @"
            UPDATE system_config SET
                raw_retention_days = @RawRetentionDays,
                agg_retention_days = @AggRetentionDays,
                polling_interval_ms = @PollingIntervalMs,
                config_reload_interval_sec = @ConfigReloadIntervalSec,
                health_check_interval_sec = @HealthCheckIntervalSec,
                ui_update_interval_ms = @UiUpdateIntervalMs,
                deadband_threshold = @DeadbandThreshold,
                data_heartbeat_sec = @DataHeartbeatSec,
                updated_at = NOW()
            WHERE id = @Id";

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, config);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SystemStatus>> GetStatusAsync()
    {
        const string sql = "SELECT * FROM system_status ORDER BY service_name";
        using var connection = _context.CreateConnection();
        try
        {
            return await connection.QueryAsync<SystemStatus>(sql);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "GetStatusAsync failed");
            return Enumerable.Empty<SystemStatus>();
        }
    }

    /// <inheritdoc />
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

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(
            sql,
            new
            {
                status.ServiceName,
                Status = Regex.Replace(status.Status.ToString(), "(?<!^)([A-Z])", "_$1").ToUpper(),
                status.UptimeSeconds,
                status.LastError,
                status.LastSync,
            }
        );
    }
}
