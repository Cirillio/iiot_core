using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;

namespace IIoT.Collector.Services;

/// <summary>
/// Сервис для обработки "сырых" значений с датчиков.
/// </summary>
public class ProcessService : IProcessService
{
    /// <inheritdoc />
    public IEnumerable<Metric> Process(
        IEnumerable<(int SensorId, ushort[] RawValues)> rawData,
        IEnumerable<SensorSettings> sensors
    )
    {
        var timestamp = DateTime.UtcNow;
        var sensorMap = sensors.ToDictionary(s => s.SensorId);

        foreach (var (sensorId, rawArray) in rawData)
        {
            if (!sensorMap.TryGetValue(sensorId, out var sensor))
                continue;

            double finalRawValue = 0;

            if (sensor.RegisterCount == 1 && rawArray.Length >= 1)
            {
                finalRawValue = rawArray[0];
            }
            else if (sensor.RegisterCount == 2 && rawArray.Length >= 2)
            {
                // По умолчанию предполагаем Big-Endian Float32 (самый частый случай в Modbus)
                // Можем добавить выбор байтового порядка в будущем
                byte[] bytes = new byte[4];
                BitConverter.TryWriteBytes(bytes.AsSpan(0, 2), rawArray[1]); // Low 16 bits
                BitConverter.TryWriteBytes(bytes.AsSpan(2, 2), rawArray[0]); // High 16 bits
                
                // Modbus Float обычно передается как High-word first, Low-word second
                // Но внутри слов байты тоже могут быть переставлены.
                // Самый стандартный: CD AB (или AB CD в зависимости от того как смотреть)
                
                finalRawValue = BitConverter.ToSingle(bytes, 0);
            }
            else if (rawArray.Length > 0)
            {
                finalRawValue = rawArray[0];
            }

            yield return new Metric
            {
                Time = timestamp,
                SensorId = sensor.SensorId,
                RawValue = finalRawValue,
                Value = SensorExtensions.Calculate(finalRawValue, sensor),
            };
        }
    }
}
