using ModbusClient.Interfaces;
using ModbusClient.Models;

namespace ModbusClient.Services;

/// <summary>
/// Сервис для обработки "сырых" значений с датчиков.
/// </summary>
public class ReadingService : IReadingService
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
            // Ищем настройку для конкретного порта на этом устройстве
            var sensor = sensors.FirstOrDefault(s =>
                s.PortNumber == port && s.DataType == SensorDataType.ANALOG
            );
            if (sensor == null)
                continue; // Если датчик не настроен в БД, пропускаем данные

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = raw,
                Value = CalculateAnalog(raw, sensor),
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

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = val ? 1.0 : 0.0,
                Value = val ? 1.0 : 0.0,
            };
        }
    }

    /// <summary>
    /// Вычисляет физическое значение аналогового сигнала.
    /// Использует линейную интерполяцию.
    /// </summary>
    /// <param name="raw">Сырое значение (0-65535).</param>
    /// <param name="s">Настройки сенсора.</param>
    /// <returns>Откалиброванное значение.</returns>
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
