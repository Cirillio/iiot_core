using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория физических соединений Modbus через Dapper.
/// </summary>
public class ConnectionRepository(DapperContext context) : IConnectionRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<ModbusConnection>> GetAllAsync()
    {
        const string sql = "SELECT id, ip_address, port, description FROM modbus_connections ORDER BY id";
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<ModbusConnection>(sql);
    }

    /// <inheritdoc />
    public async Task<ModbusConnection?> GetByIdAsync(int id)
    {
        const string sql = "SELECT id, ip_address, port, description FROM modbus_connections WHERE id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ModbusConnection>(sql, new { Id = id });
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(ModbusConnection conn)
    {
        const string sql =
            @"
            INSERT INTO modbus_connections (ip_address, port, description)
            VALUES (@IpAddress, @Port, @Description)
            RETURNING id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, conn);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ModbusConnection conn)
    {
        const string sql =
            @"
            UPDATE modbus_connections
            SET ip_address = @IpAddress,
                port = @Port,
                description = @Description
            WHERE id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, conn);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM modbus_connections WHERE id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
