using System;

namespace ModbusClient.Models;

/// <summary>
/// Глобальная конфигурация системы.
/// Управляет параметрами хранения данных, частотой опросов и системными тайм-аутами.
/// </summary>
public class SystemConfig
{
    /// <summary>
    /// Идентификатор конфигурации.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Срок хранения "сырых" (детальных) данных в днях.
    /// По умолчанию: 90 дней (3 месяца).
    /// </summary>
    public int RawRetentionDays { get; init; } = 90;

    /// <summary>
    /// Срок хранения агрегированных данных (усредненных) в днях.
    /// По умолчанию: 1825 дней (5 лет).
    /// </summary>
    public int AggRetentionDays { get; init; } = 1825;

    /// <summary>
    /// Интервал опроса датчиков в миллисекундах.
    /// Определяет частоту Modbus-запросов.
    /// По умолчанию: 1000 мс (1 секунда).
    /// </summary>
    public int PollingIntervalMs { get; init; } = 1000;

    /// <summary>
    /// Интервал проверки изменений в конфигурации устройств/датчиков (в секундах).
    /// По умолчанию: 60 секунд.
    /// </summary>
    public int ConfigReloadIntervalSec { get; init; } = 60;

    /// <summary>
    /// Интервал отправки "пульса" (heartbeat) сервиса для мониторинга его состояния (в секундах).
    /// По умолчанию: 30 секунд.
    /// </summary>
    public int HealthCheckIntervalSec { get; init; } = 30;

    /// <summary>
    /// Порог нечувствительности (Deadband).
    /// Изменение значения меньше этого порога может игнорироваться для экономии места.
    /// </summary>
    public double DeadbandThreshold { get; init; } = 0.01;

    /// <summary>
    /// Максимальное время (в секундах) без данных, после которого считается, что поток данных прерван.
    /// По умолчанию: 600 секунд (10 минут).
    /// </summary>
    public int DataHeartbeatSec { get; init; } = 600;

    /// <summary>
    /// Время последнего обновления конфигурации.
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
