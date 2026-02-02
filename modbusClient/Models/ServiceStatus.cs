namespace ModbusClient.Models;

/// <summary>
/// Статусы работоспособности сервиса или компонента системы.
/// </summary>
public enum ServiceStatus
{
    /// <summary>
    /// Сервис работает штатно, ошибок нет.
    /// </summary>
    ONLINE,

    /// <summary>
    /// Сервис недоступен или остановлен.
    /// </summary>
    OFFLINE,

    /// <summary>
    /// Сервис работает, но с ограниченной функциональностью или производительностью.
    /// </summary>
    DEGRADED,

    /// <summary>
    /// Произошла критическая ошибка, требующая вмешательства.
    /// </summary>
    CRITICAL_ERROR,

    /// <summary>
    /// Сервис находится в режиме обслуживания.
    /// </summary>
    MAINTENANCE,
}
