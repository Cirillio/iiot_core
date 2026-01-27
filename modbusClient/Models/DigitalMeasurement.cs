namespace ModbusClient.Models;

/// <summary>
/// Represents the state of a digital input (DO/DI).
/// </summary>
public record DigitalMeasurement
{
    public long Id { get; init; }
    public int PortId { get; init; }
    public bool Value { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
