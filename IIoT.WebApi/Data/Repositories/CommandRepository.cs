using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория команд управления через Dapper.
/// </summary>
public class CommandRepository(DapperContext context) : ICommandRepository
{
    private readonly DapperContext _context = context;

    private const string Columns =
        "id, tag_id, value, operator_id, status, error_message, created_at, updated_at";

    /// <inheritdoc />
    public async Task<DeviceCommand> CreateAsync(int tagId, double value, string operatorId)
    {
        const string sql =
            @"
            INSERT INTO device_commands (tag_id, value, operator_id)
            VALUES (@TagId, @Value, @OperatorId)
            RETURNING id, tag_id, value, operator_id, status, error_message, created_at, updated_at";

        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<DeviceCommand>(
            sql,
            new
            {
                TagId = tagId,
                Value = value,
                OperatorId = operatorId,
            }
        );
    }

    /// <inheritdoc />
    public async Task<DeviceCommand?> GetByIdAsync(Guid id)
    {
        var sql = $"SELECT {Columns} FROM device_commands WHERE id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DeviceCommand>(sql, new { Id = id });
    }
}
