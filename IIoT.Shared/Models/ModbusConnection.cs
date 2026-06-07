namespace IIoT.Shared.Models;

/// <summary>
/// Физическое сетевое соединение Modbus TCP (сокет ip_address:port).
/// Выделено в отдельную сущность, чтобы несколько устройств (разные Slave ID),
/// висящих за одним RS-485→TCP шлюзом, делили одну TCP-сессию.
/// Соответствует таблице 'modbus_connections'.
/// </summary>
public record ModbusConnection
{
    /// <summary>
    /// Уникальный идентификатор соединения (Primary Key).
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// IP-адрес шлюза или устройства (IPv4).
    /// </summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// TCP-порт. По умолчанию 502 (стандартный Modbus TCP).
    /// </summary>
    public int Port { get; init; } = 502;

    /// <summary>
    /// Человекочитаемое описание (например, "Шлюз цеха №2").
    /// </summary>
    public string? Description { get; init; }
}
