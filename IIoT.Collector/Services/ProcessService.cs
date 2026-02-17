using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;

namespace IIoT.Collector.Services;

/// <summary>
/// Сервис для обработки "сырых" значений с датчиков.
/// </summary>
public class ProcessService : IProcessService
{
    /// <inheritdoc />
    public IEnumerable<Metric> ProcessAnalog(
        IEnumerable<(int Port, ushort Val)> rawData,
        IEnumerable<SensorSettings> sensors
    )
    {
        var timestamp = DateTime.UtcNow;

        foreach (var (port, raw) in rawData)
        {
            var sensor = sensors.FirstOrDefault(s =>
                s.PortNumber == port && s.DataType == SensorDataType.ANALOG
            );

            if (sensor == null)
                continue;

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = raw,
                Value = SensorExtensions.Calculate(raw, sensor),
            };
        }
    }

    /// <inheritdoc />
    public IEnumerable<Metric> ProcessDigital(
        IEnumerable<(int Port, bool Val)> rawData,
        IEnumerable<SensorSettings> sensors
    )
    {
        var timestamp = DateTime.UtcNow;

        foreach (var (port, val) in rawData)
        {
            var sensor = sensors.FirstOrDefault(s =>
                s.PortNumber == port && s.DataType == SensorDataType.DIGITAL
            );

            if (sensor == null)
                continue;

            double rawValue = val ? 1.0 : 0.0;

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = rawValue,
                Value = SensorExtensions.Calculate(rawValue, sensor),
            };
        }
    }
}
