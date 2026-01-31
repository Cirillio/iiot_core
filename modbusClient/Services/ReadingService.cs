using ModbusClient.Interfaces;
using ModbusClient.Models;

namespace ModbusClient.Services;

public class ReadingService : IReadingService
{
    public IEnumerable<Metric> ProcessAnalog(
        IEnumerable<(int Port, ushort Val)> rawData,
        IEnumerable<SensorSettings> sensors
    )
    {
        var timestamp = DateTime.UtcNow;

        foreach (var (port, raw) in rawData)
        {
            // Ищем настройку для конкретного порта на этом устройстве
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
                Value = CalculateAnalog(raw, sensor),
            };
        }
    }

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

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = val ? 1.0 : 0.0,
                Value = val ? 1.0 : 0.0,
            };
        }
    }

    private static double CalculateAnalog(double raw, SensorSettings s)
    {
        // Защита от деления на ноль
        if (Math.Abs(s.InputMax - s.InputMin) < 0.0001)
            return s.OutputMin;

        // Линейная интерполяция: Y = (X - Xmin) * (Ymax - Ymin) / (Xmax - Xmin) + Ymin
        double normalized =
            (raw - s.InputMin) * (s.OutputMax - s.OutputMin) / (s.InputMax - s.InputMin)
            + s.OutputMin;

        return normalized + s.OffsetVal;
    }
}
