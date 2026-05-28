using Dapper;
using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;
using Npgsql;
using Serilog;

namespace IIoT.Collector.Repositories;

/// <summary>
/// Репозиторий команд управления (таблица 'device_commands') для коллектора.
/// </summary>
public class CommandRepository(NpgsqlDataSource dataSource) : ICommandRepository
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly ILogger _logger = Log.ForContext<CommandRepository>();

    /// <inheritdoc />
    public async Task<DeviceCommand?> GetByIdAsync(Guid id)
    {
        const string sql =
            @"
            SELECT id, tag_id, value, operator_id, status, error_message, created_at, updated_at
            FROM device_commands
            WHERE id = @Id";

        await using var conn = await _dataSource.OpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<DeviceCommand>(sql, new { Id = id });
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid id, CommandStatus status, string? errorMessage = null)
    {
        const string sql =
            @"
            UPDATE device_commands
            SET status = @Status::command_status,
                error_message = @ErrorMessage
            WHERE id = @Id";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            await conn.ExecuteAsync(
                sql,
                new
                {
                    Id = id,
                    Status = status.ToString().ToUpperInvariant(),
                    ErrorMessage = errorMessage,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update status of command {Id} to {Status}", id, status);
        }
    }
}
