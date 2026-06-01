using IIoT.Shared.Models;

namespace IIoT.Collector.Interfaces;

/// <summary>
/// Рантайм-статус доступности одного устройства за цикл опроса.
/// </summary>
/// <param name="DeviceId">ID устройства.</param>
/// <param name="IsOnline">Доступно ли (с учётом гистерезиса).</param>
/// <param name="Seen">Был ли успешный контакт в этом цикле (для обновления last_seen).</param>
/// <param name="Error">Текст ошибки связи (null при успехе).</param>
public record DeviceStatusUpdate(int DeviceId, bool IsOnline, bool Seen, string? Error);

/// <summary>
/// Интерфейс основного репозитория данных (PostgreSQL/TimescaleDB).
/// Отвечает за сохранение временных рядов (метрик), получение конфигураций и обновление статусов.
/// </summary>
public interface IDataRepository
{
    /// <summary>
    /// Сохраняет пакет измерений (метрик) в гипертаблицу 'metrics'.
    /// </summary>
    /// <param name="metrics">Коллекция метрик для вставки.</param>
    Task SaveMetricsAsync(IEnumerable<Metric> metrics);

    /// <summary>
    /// Обновляет информацию о текущем состоянии сервиса (Heartbeat) в таблице системных статусов.
    /// </summary>
    /// <param name="status">Объект статуса системы.</param>
    Task UpdateSystemStatusAsync(SystemStatus status);

    /// <summary>
    /// Получает список всех активных устройств (контроллеров), которые необходимо опрашивать.
    /// Учитывает флаг IsActive = true.
    /// </summary>
    /// <returns>Коллекция устройств.</returns>
    Task<IEnumerable<Device>> GetActiveDevicesAsync();

    /// <summary>
    /// Получает все физические соединения (сокеты) Modbus.
    /// Используется коллектором для резолва ip:port по ConnectionId устройства.
    /// </summary>
    /// <returns>Коллекция соединений.</returns>
    Task<IEnumerable<ModbusConnection>> GetConnectionsAsync();

    /// <summary>
    /// Получает полные настройки всех тегов.
    /// Используется для маппинга данных (Device + Port -> TagId) и калибровки значений.
    /// </summary>
    /// <returns>Коллекция настроек тегов.</returns>
    Task<IEnumerable<TagSettings>> GetTagSettingsAsync();

    /// <summary>
    /// Получает глобальную конфигурацию системы (интервалы опроса, политики хранения данных и т.д.).
    /// </summary>
    /// <returns>Объект конфигурации системы.</returns>
    Task<SystemConfig> GetSystemConfigAsync();

    /// <summary>
    /// Батч-обновление рантайм-статуса доступности устройств (is_online / last_seen / last_conn_error).
    /// Не трогает is_active.
    /// </summary>
    Task UpdateDeviceStatusesAsync(IReadOnlyList<DeviceStatusUpdate> updates);
}
