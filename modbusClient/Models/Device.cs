namespace ModbusClient.Models;

/// <summary>
/// Модель устройства (контроллера) из таблицы devices.
/// </summary>
public record Device
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty; // Например, "192.168.1.50"
    public int Port { get; init; } = 502;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
