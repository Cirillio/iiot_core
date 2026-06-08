using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория для управления устройствами через Dapper.
/// Сетевой сокет вынесен в modbus_connections, поэтому ip:port подтягивается через JOIN.
/// </summary>
public class DeviceRepository(DapperContext context) : IDeviceRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<DashboardDeviceDTO>> GetDevicesWithTagsAsync(int? tagLimit = null)
    {
        // CTE с ROW_NUMBER() для фильтрации топ-N тегов по mainPagePosition.
        // TotalTags считаем отдельно, чтобы limit не обнулял счётчик.
        var sql =
            @"
            WITH RankedTags AS (
                SELECT
                    s.*,
                    ROW_NUMBER() OVER (
                        PARTITION BY s.device_id
                        ORDER BY
                            COALESCE((s.ui_config->>'mainPagePosition')::int, 9999) ASC,
                            s.tag_id ASC
                    ) as rn
                FROM tags s
            ),
            DeviceTotals AS (
                SELECT device_id, COUNT(*) as total_count
                FROM tags
                GROUP BY device_id
            )
            SELECT
                d.id, d.name, d.connection_id, d.slave_id, d.use_group_polling, d.max_register_span, d.is_active,
                d.is_online, d.last_seen, d.created_at,
                mc.ip_address, mc.port,
                COALESCE(dt.total_count, 0) as TotalTags,
                rs.tag_id,
                rs.device_id,
                rs.port_number,
                rs.name,
                rs.slug,
                rs.data_type AS DataType,
                rs.unit,
                rs.ui_config AS UiConfigJson,
                rs.updated_at
            FROM devices d
            JOIN modbus_connections mc ON d.connection_id = mc.id
            LEFT JOIN DeviceTotals dt ON d.id = dt.device_id
            LEFT JOIN RankedTags rs ON d.id = rs.device_id AND (@Limit IS NULL OR rs.rn <= @Limit)
            ORDER BY d.id, rs.rn";

        var devices = new Dictionary<int, DashboardDeviceDTO>();

        using var connection = _context.CreateConnection();

        await connection.QueryAsync<DashboardDeviceDTO, DashboardTagDTO, DashboardDeviceDTO>(
            sql,
            (device, tag) =>
            {
                if (!devices.TryGetValue(device.Id, out var currentDevice))
                {
                    currentDevice = device;
                    devices.Add(currentDevice.Id, currentDevice);
                }

                if (tag != null && tag.TagId != 0)
                {
                    currentDevice.Tags.Add(tag);
                }

                return currentDevice;
            },
            new { Limit = tagLimit },
            splitOn: "tag_id"
        );

        return devices.Values;
    }

    /// <inheritdoc />
    public async Task<Device?> GetDeviceByIdWithTagsAsync(int _deviceId)
    {
        var sql =
            @"
            SELECT d.*, s.*, s.ui_config AS UiConfigJson
            FROM devices d
            LEFT JOIN tags s ON d.id = s.device_id
            WHERE d.id = @Id";

        Device? device = null;

        using var connection = _context.CreateConnection();

        await connection.QueryAsync<Device, TagSettings, Device>(
            sql,
            (d, s) =>
            {
                device ??= d;
                if (s != null && s.TagId != 0)
                {
                    device.Tags.Add(s);
                }
                return d;
            },
            new { Id = _deviceId },
            splitOn: "tag_id"
        );

        return device;
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(Device _device)
    {
        var sql =
            @"
            INSERT INTO devices (name, connection_id, slave_id, use_group_polling, max_register_span, max_bit_span, is_active, created_at)
            VALUES (@Name, @ConnectionId, @SlaveId, @UseGroupPolling, @MaxRegisterSpan, @MaxBitSpan, @IsActive, @CreatedAt)
            RETURNING id;";

        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, _device);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Device _device)
    {
        var sql =
            @"
            UPDATE devices
            SET name = @Name,
                connection_id = @ConnectionId,
                slave_id = @SlaveId,
                use_group_polling = @UseGroupPolling,
                max_register_span = @MaxRegisterSpan,
                max_bit_span = @MaxBitSpan,
                is_active = @IsActive
            WHERE id = @Id;";

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, _device);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int _deviceId)
    {
        var sql = "DELETE FROM devices WHERE id = @Id;";

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = _deviceId });
    }
}
