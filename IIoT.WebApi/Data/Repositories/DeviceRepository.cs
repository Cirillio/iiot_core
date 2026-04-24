using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Реализация репозитория для управления устройствами через Dapper.
/// </summary>
public class DeviceRepository(DapperContext context) : IDeviceRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<DashboardDeviceDTO>> GetDevicesWithSensorsAsync(int? sensorLimit = null)
    {
        // Используем CTE с оконной функцией ROW_NUMBER() для фильтрации топ-N датчиков по mainPagePosition
        // TotalSensors считаем отдельно, чтобы limit не обнулял счетчик
        var sql =
            @"
            WITH RankedSensors AS (
                SELECT 
                    s.*,
                    ROW_NUMBER() OVER (
                        PARTITION BY s.device_id 
                        ORDER BY 
                            COALESCE((s.ui_config->>'mainPagePosition')::int, 9999) ASC,
                            s.sensor_id ASC
                    ) as rn
                FROM sensor_settings s
            ),
            DeviceTotals AS (
                SELECT device_id, COUNT(*) as total_count
                FROM sensor_settings
                GROUP BY device_id
            )
            SELECT 
                d.id, d.name, d.ip_address, d.port, d.slave_id, d.is_active, d.created_at,
                COALESCE(dt.total_count, 0) as TotalSensors,
                rs.sensor_id, 
                rs.device_id, 
                rs.port_number, 
                rs.name, 
                rs.slug, 
                rs.data_type AS SensorDataType, 
                rs.unit, 
                rs.ui_config AS UiConfigJson, 
                rs.updated_at
            FROM devices d
            LEFT JOIN DeviceTotals dt ON d.id = dt.device_id
            LEFT JOIN RankedSensors rs ON d.id = rs.device_id AND (@Limit IS NULL OR rs.rn <= @Limit)
            ORDER BY d.id, rs.rn";

        var devices = new Dictionary<int, DashboardDeviceDTO>();

        using var connection = _context.CreateConnection();

        await connection.QueryAsync<DashboardDeviceDTO, DashboardSensorDTO, DashboardDeviceDTO>(
            sql,
            (device, sensor) =>
            {
                if (!devices.TryGetValue(device.Id, out var currentDevice))
                {
                    currentDevice = device;
                    devices.Add(currentDevice.Id, currentDevice);
                }

                // Dapper маппит TotalSensors из первого объекта (device), поэтому дополнительно присваивать не нужно,
                // но важно убедиться, что свойство заполнилось.
                // При MultiMapping Dapper клонирует объект device для каждой строки, 
                // но так как мы используем Dictionary, мы берем только первый экземпляр.

                if (sensor != null && sensor.SensorId != 0)
                {
                    currentDevice.Sensors.Add(sensor);
                }

                return currentDevice;
            },
            new { Limit = sensorLimit },
            splitOn: "sensor_id"
        );

        return devices.Values;
    }

    /// <inheritdoc />
    public async Task<Device?> GetDeviceByIdWithSensorsAsync(int _deviceId)
    {
        var sql =
            @"
            SELECT d.*, s.* 
            FROM devices d
            LEFT JOIN sensor_settings s ON d.id = s.device_id
            WHERE d.id = @Id";

        Device? device = null;

        using var connection = _context.CreateConnection();

        await connection.QueryAsync<Device, SensorSettings, Device>(
            sql,
            (d, s) =>
            {
                device ??= d;
                if (s != null && s.SensorId != 0)
                {
                    device.Sensors.Add(s);
                }
                return d;
            },
            new { Id = _deviceId },
            splitOn: "sensor_id"
        );

        return device;
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(Device _device)
    {
        var sql =
            @"
            INSERT INTO devices (name, ip_address, port, slave_id, is_active, created_at)
            VALUES (@Name, @IpAddress, @Port, @SlaveId, @IsActive, @CreatedAt)
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
                ip_address = @IpAddress,
                port = @Port,
                slave_id = @SlaveId,
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
