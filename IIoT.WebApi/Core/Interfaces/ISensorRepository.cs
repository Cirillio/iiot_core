using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Интерфейс репозитория для управления настройками датчиков (сенсоров).
/// </summary>
public interface ISensorRepository
{
    /// <summary>
    /// Получить все датчики из системы.
    /// </summary>
    /// <returns>Коллекция всех настроек датчиков.</returns>
    Task<IEnumerable<SensorSettings>> GetAllAsync();

    /// <summary>
    /// Получить настройки конкретного датчика по его идентификатору.
    /// </summary>
    /// <param name="sensorId">Уникальный ID датчика.</param>
    /// <returns>Объект настроек или null, если не найден.</returns>
    Task<SensorSettings?> GetByIdAsync(int sensorId);

    /// <summary>
    /// Получить список датчиков, привязанных к конкретному устройству.
    /// </summary>
    /// <param name="deviceId">ID устройства.</param>
    /// <returns>Коллекция датчиков устройства.</returns>
    Task<IEnumerable<SensorSettings>> GetByDeviceIdAsync(int deviceId);

    /// <summary>
    /// Добавить новый датчик в базу данных.
    /// </summary>
    /// <param name="sensor">Объект настроек датчика.</param>
    /// <returns>ID созданного датчика.</returns>
    Task<int> AddAsync(SensorSettings sensor);

    /// <summary>
    /// Обновить существующие настройки датчика.
    /// </summary>
    /// <param name="sensor">Объект с обновленными данными.</param>
    Task UpdateAsync(SensorSettings sensor);

    /// <summary>
    /// Удалить датчик из системы.
    /// </summary>
    /// <param name="sensorId">ID датчика.</param>
    Task DeleteAsync(int sensorId);
}
