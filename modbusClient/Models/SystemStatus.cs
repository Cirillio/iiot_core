namespace ModbusClient.Models;

public record SystemStatus
{
    /// <summary>
    /// Уникальное имя сервиса (например, "ModbusCollector").
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;
    public ServiceStatus Status { get; init; } = ServiceStatus.ONLINE;
    public long UptimeSeconds { get; init; }
    public string? LastError { get; init; } = string.Empty;
    public DateTime LastSync { get; init; } = DateTime.UtcNow;
};
