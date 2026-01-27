using ModbusClient.Models;

namespace ModbusClient.Interfaces;

/// <summary>
/// Interface for interacting with the database.
/// </summary>
public interface IDataRepository
{
    // Saving a batch of analog (RAW) data
    // Using IEnumerable for Bulk Insert (high speed)
    Task SaveRawMeasurementsAsync(IEnumerable<RawMeasurement> measurements);

    // Saving digital input states
    Task SaveDigitalMeasurementsAsync(IEnumerable<DigitalMeasurement> measurements);

    // Getting port settings (so the worker knows what to poll)
    Task<IEnumerable<SensorConfig>> GetSensorConfigsAsync();

    // Updating settings (called by API server or desktop client)
    Task UpdateSensorConfigAsync(SensorConfig config);

    // Updating system status in DB logs
    Task UpdateSystemStatusAsync(SystemStatus status);
}
