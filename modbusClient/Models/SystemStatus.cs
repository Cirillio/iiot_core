namespace ModbusClient.Models;

public record SystemStatus(bool IsConnected, string LastError, DateTime LastSync);
