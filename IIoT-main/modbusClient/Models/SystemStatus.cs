namespace ModbusClient.Models;

/// <summary>
/// DTO для передачи текущего статуса системы (Health Check).
/// </summary>
public record SystemStatus
{
    /// <summary>
    /// Уникальное имя сервиса (например, "ModbusCollector").
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Текущее состояние сервиса (Enum).
    /// </summary>
    public ServiceStatus Status { get; init; } = ServiceStatus.ONLINE;

    /// <summary>
    /// Время непрерывной работы сервиса в секундах (Uptime).
    /// </summary>
    public long UptimeSeconds { get; init; }

    /// <summary>
    /// Текст последней ошибки (если есть), иначе пустая строка.
    /// </summary>
    public string? LastError { get; init; } = string.Empty;

    /// <summary>
    /// Временная метка последней синхронизации или проверки статуса (UTC).
    /// </summary>
    public DateTime LastSync { get; init; } = DateTime.UtcNow;
};
