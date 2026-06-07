using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Интерфейс репозитория для управления настройками датчиков (сенсоров).
/// </summary>
public interface ITagRepository
{
    /// <summary>
    /// Получить все датчики из системы.
    /// </summary>
    /// <returns>Коллекция всех настроек датчиков.</returns>
    Task<IEnumerable<TagSettings>> GetAllAsync();

    /// <summary>
    /// Получить настройки конкретного датчика по его идентификатору.
    /// </summary>
    /// <param name="tagId">Уникальный ID датчика.</param>
    /// <returns>Объект настроек или null, если не найден.</returns>
    Task<TagSettings?> GetByIdAsync(int tagId);

    /// <summary>
    /// Получить список датчиков, привязанных к конкретному устройству.
    /// </summary>
    /// <param name="deviceId">ID устройства.</param>
    /// <returns>Коллекция датчиков устройства.</returns>
    Task<IEnumerable<TagSettings>> GetByDeviceIdAsync(int deviceId);

    /// <summary>
    /// Добавить новый датчик в базу данных.
    /// </summary>
    /// <param name="tag">Объект настроек датчика.</param>
    /// <returns>ID созданного датчика.</returns>
    Task<int> AddAsync(TagSettings tag);

    /// <summary>
    /// Обновить существующие настройки датчика.
    /// </summary>
    /// <param name="tag">Объект с обновленными данными.</param>
    Task UpdateAsync(TagSettings tag);

    /// <summary>
    /// Удалить датчик из системы.
    /// </summary>
    /// <param name="tagId">ID датчика.</param>
    Task DeleteAsync(int tagId);
}
