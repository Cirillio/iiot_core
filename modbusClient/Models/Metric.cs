namespace ModbusClient.Models;

/// <summary>
/// Модель для записи в гипертаблицу metrics.
/// </summary>
public record Metric
{
    public DateTime Time { get; init; }
    public int SensorId { get; init; }
    public double? RawValue { get; init; } // Nullable, так как в БД DOUBLE PRECISION (может быть NULL)
    public double Value { get; init; } // Откалиброванное значение
}
