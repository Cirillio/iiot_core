namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// DTO устройства для отображения на главном экране (Dashboard).
/// Содержит базовую информацию и список датчиков.
/// </summary>
public record DashboardDeviceDTO
{
    /// <summary>
    /// ID устройства в БД.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Человекочитаемое имя устройства.
    /// </summary>
    public string Name { get; init; } = "Unnamed Device";

    /// <summary>
    /// ID физического соединения (modbus_connections).
    /// </summary>
    public int ConnectionId { get; init; }

    /// <summary>
    /// IP-адрес шлюза/контроллера (из modbus_connections).
    /// </summary>
    public string IpAddress { get; init; } = "127.0.0.1";

    /// <summary>
    /// Порт Modbus TCP (из modbus_connections).
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Modbus Unit ID.
    /// </summary>
    public int SlaveId { get; init; }

    /// <summary>
    /// Включён ли групповой опрос смежных регистров.
    /// </summary>
    public bool UseGroupPolling { get; init; } = true;

    /// <summary>
    /// Максимальная ширина чанка при групповом опросе.
    /// </summary>
    public int MaxRegisterSpan { get; init; } = 120;

    /// <summary>
    /// Состояние активности опроса (намерение оператора).
    /// </summary>
    public bool IsActive { get; init; } = false;

    /// <summary>
    /// Рантайм-доступность: смог ли коллектор связаться с устройством (пишет коллектор).
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Время последнего успешного контакта (UTC).
    /// </summary>
    public DateTime? LastSeen { get; init; }

    /// <summary>
    /// Дата регистрации устройства.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Список тегов, привязанных к данному устройству.
    /// </summary>
    public List<DashboardTagDTO> Tags { get; init; } = [];

    /// <summary>
    /// Общее количество тегов (включая скрытые фильтром).
    /// </summary>
    public int TotalTags { get; init; }
}
