namespace ModbusClient.Models;

/// <summary>
/// Calibration settings and metadata for a port.
/// </summary>
public record SensorConfig
{
    public int PortId { get; init; }
    public string SensorName { get; init; } = string.Empty;
    public string Unit { get; init; } = "V"; // Единица измерения (напр. Бар, °C)

    // Границы входного сигнала (напр. -10 до 10 для вольтажа)
    public double InputMin { get; init; } = -10.0;
    public double InputMax { get; init; } = 10.0;

    // Границы физической величины (напр. 0 до 100 Бар)
    public double OutputMin { get; init; } = 0.0;
    public double OutputMax { get; init; } = 100.0;

    // Дополнительное смещение (калибровка нуля)
    public double OffsetVal { get; init; } = 0.0;
}
