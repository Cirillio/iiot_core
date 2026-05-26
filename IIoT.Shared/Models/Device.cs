using IIoT.Shared.Models;

namespace IIoT.Shared.Models;

/// <summary>
/// Представляет устройство (Modbus TCP контроллер или шлюз), хранящееся в таблице базы данных 'devices'.
/// Используется для установления сетевого соединения и опроса регистров.
/// </summary>
public record Device
{
    /// <summary>
    /// Уникальный идентификатор устройства в базе данных (Primary Key).
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Человекочитаемое имя устройства для отображения в интерфейсе и логах.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    public List<TagSettings> Tags { get; init; } = [];

    /// <summary>
    /// Общее количество тегов, привязанных к устройству.
    /// </summary>
    public int TotalTags { get; init; }

    /// <summary>
    /// Идентификатор физического соединения (modbus_connections.id), через которое
    /// опрашивается устройство. Несколько устройств могут делить одно соединение (шлюз).
    /// </summary>
    public int ConnectionId { get; init; }

    /// <summary>
    /// Modbus Unit ID (Slave Address). Адресует устройство на шине / через шлюз (1-247).
    /// </summary>
    public int SlaveId { get; init; } = 1;

    /// <summary>
    /// Включать ли групповой (пакетный) опрос смежных регистров.
    /// false — каждый тег читается отдельным запросом (для "капризного" оборудования).
    /// </summary>
    public bool UseGroupPolling { get; init; } = true;

    /// <summary>
    /// Максимальный диапазон адресов в одном групповом запросе.
    /// Ограничивает ширину чанка при группировке.
    /// </summary>
    public int MaxRegisterSpan { get; init; } = 120;

    /// <summary>
    /// Флаг активности устройства.
    /// Если false, устройство исключается из цикла опроса.
    /// Значение по умолчанию: true.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Дата и время создания записи об устройстве в UTC.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
