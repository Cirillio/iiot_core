using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

public interface IDeviceRepository
{
    Task<IEnumerable<Device>> GetDevicesWithSensorsAsync();

    Task<Device?> GetDeviceByIdWithSensorsAsync(int _deviceId);

    Task AddDeviceAsync(Device _device);

    Task UpdateDeviceAsync(Device _device);

    Task DeleteDeviceAsync(int _deviceId);
}
