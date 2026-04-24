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
    /// IP-адрес контроллера.
    /// </summary>
    public string IpAddress { get; init; } = "127.0.0.1";

    /// <summary>
    /// Порт Modbus TCP.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Modbus Unit ID.
    /// </summary>
    public int SlaveId { get; init; }

    /// <summary>
    /// Состояние активности опроса.
    /// </summary>
    public bool IsActive { get; init; } = false;

    /// <summary>
    /// Дата регистрации устройства.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Список датчиков, подключенных к данному устройству.
    /// </summary>
    public List<DashboardSensorDTO> Sensors { get; init; } = [];

    /// <summary>
    /// Общее количество датчиков (включая скрытые фильтром).
    /// </summary>
    public int TotalSensors { get; init; }
}
