namespace ModbusClient.Models;

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

    /// <summary>
    /// IP-адрес устройства в сети (IPv4).
    /// Например: "192.168.1.50".
    /// </summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// Порт TCP для подключения по протоколу Modbus.
    /// Значение по умолчанию: 502 (стандартный порт Modbus TCP).
    /// </summary>
    public int Port { get; init; } = 502;

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
