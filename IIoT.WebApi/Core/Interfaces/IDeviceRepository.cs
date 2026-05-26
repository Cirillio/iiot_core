using IIoT.Shared.Models;
using IIoT.WebApi.Data.DTO;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Интерфейс репозитория для управления устройствами (контроллерами).
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// Получить список всех устройств вместе с их датчиками в формате DTO для дашборда.
    /// </summary>
    /// <param name="tagLimit">Максимальное количество датчиков для каждого устройства (для превью).</param>
    /// <returns>Коллекция DTO устройств.</returns>
    Task<IEnumerable<DashboardDeviceDTO>> GetDevicesWithTagsAsync(int? tagLimit = null);

    /// <summary>
    /// Получить полную информацию об устройстве и его датчиках по ID.
    /// </summary>
    /// <param name="_deviceId">Идентификатор устройства.</param>
    /// <returns>Объект Device или null, если не найден.</returns>
    Task<Device?> GetDeviceByIdWithTagsAsync(int _deviceId);

    /// <summary>
    /// Добавить новое устройство в систему.
    /// </summary>
    /// <param name="_device">Объект устройства.</param>
    /// <returns>ID созданного устройства.</returns>
    Task<int> AddAsync(Device _device);

    /// <summary>
    /// Обновить существующее устройство.
    /// </summary>
    /// <param name="_device">Объект устройства с обновленными данными.</param>
    Task UpdateAsync(Device _device);

    /// <summary>
    /// Удалить устройство из системы.
    /// </summary>
    /// <param name="_deviceId">Идентификатор устройства.</param>
    Task DeleteAsync(int _deviceId);
}
