namespace ModbusClient.Models;

/// <summary>
/// Represents a raw measurement record from a Modbus register.
/// </summary>
public record RawMeasurement
{
    // BIGSERIAL в Postgres мапится на long в C#
    public long Id { get; init; }

    // Номер порта (0-7 для ADAM-6017)
    public int PortId { get; init; }

    // Значение 0-65535 (16 бит)
    public int RawValue { get; init; }

    // Время создания записи (всегда UTC для БД)
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
