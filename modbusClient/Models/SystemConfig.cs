using System;

namespace ModbusClient.Models;

public class SystemConfig
{
    public int Id { get; init; }
    public int RawRetentionDays { get; init; } = 90; // 3 месяца
    public int AggRetentionDays { get; init; } = 1825; // 5 Лет
    public int PollingIntervalMs { get; init; } = 1000; // Опрос датчиков
    public int ConfigReloadIntervalSec { get; init; } = 60; // Проверка новых устройств
    public int HealthCheckIntervalSec { get; init; } = 30; // Отправка пульса

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
