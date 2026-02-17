using Dapper;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

public class DeviceRepository(DapperContext context) : IDeviceRepository
{
    private readonly DapperContext _context = context;

    public async Task<IEnumerable<Device>> GetDevicesWithSensorsAsync()
    {
        var sql =
            @"select d.*, s.* from devices d
            left join sensor_settings s on d.id = s.device_id
        ";

        var devices = new Dictionary<int, Device>();

        using var connection = _context.CreateConnection();

        var result = await connection.QueryAsync<Device, SensorSettings, Device>(
            sql,
            (device, sensor) =>
            {
                // Проверяем, не добавляли ли мы это устройство ранее
                if (!devices.TryGetValue(device.Id, out var currentDevice))
                {
                    currentDevice = device;
                    // Инициализируем список сенсоров (нужно добавить это поле в record Device)
                    // currentDevice.Sensors = new List<SensorSettings>();
                    devices.Add(currentDevice.Id, currentDevice);
                }

                // Если у устройства есть датчик (JOIN нашел запись), добавляем его в список
                if (sensor != null)
                {
                    currentDevice.Sensors.Add(sensor);
                }

                return currentDevice;
            },
            splitOn: "sensor_id" // Dapper должен знать, где в строке заканчивается Device и начинается Sensor
        );

        return devices.Values;
    }

    public async Task<Device?> GetDeviceByIdWithSensorsAsync(int _deviceId)
    {
        var sql =
            @"select d.*, s.* from devices d
            left join sensor_settings s on d.id = s.device_id
            where d.id = @Id";

        Device? device = null;

        using var connection = _context.CreateConnection();

        await connection.QueryAsync<Device, SensorSettings, Device>(
            sql,
            (d, s) =>
            {
                device ??= d;
                if (s != null)
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

    public async Task AddDeviceAsync(Device _device) { }

    public async Task UpdateDeviceAsync(Device _device) { }

    public async Task DeleteDeviceAsync(int _deviceId) { }
}
