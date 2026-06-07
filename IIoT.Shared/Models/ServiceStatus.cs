namespace IIoT.Shared.Models;

/// <summary>
/// Статусы работоспособности сервиса или компонента системы.
/// </summary>
public enum ServiceStatus
{
    Online,
    Offline,
    Degraded,
    CriticalError,
    Maintenance,
}
