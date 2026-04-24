namespace IIoT.WebApi.Core.Interfaces;

public interface IMonitoringClient
{
    /// <summary>
    /// Передача новых метрик в JSON-формате.
    /// </summary>
    Task ReceiveMetrics(string json);

    /// <summary>
    /// Уведомление об обновлении конфигурации устройства или датчика.
    /// </summary>
    /// <param name="entityType">Тип ("DEVICE", "SENSOR")</param>
    /// <param name="entityId">ID измененной сущности</param>
    Task ConfigUpdated(string entityType, int entityId);

    /// <summary>
    /// Системные алерты (ошибки, предупреждения).
    /// </summary>
    /// <param name="message">Текст уведомления</param>
    /// <param name="level">Уровень ("INFO", "WARNING", "CRITICAL")</param>
    Task SystemAlert(string message, string level);
}
