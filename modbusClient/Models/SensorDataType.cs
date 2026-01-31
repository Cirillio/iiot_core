namespace ModbusClient.Models;

/// <summary>
/// Тип данных датчика (соответствует SQL ENUM sensor_data_type).
/// </summary>
public enum SensorDataType
{
    ANALOG,
    DIGITAL,
    VIRTUAL,
}
