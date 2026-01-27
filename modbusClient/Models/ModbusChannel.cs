namespace ModbusClient.Models;

/// <summary>
/// Internal representation of a Modbus channel before saving.
/// </summary>
public record ModbusChannel(int Index, ushort Value);
