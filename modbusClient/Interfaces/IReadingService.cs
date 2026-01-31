using ModbusClient.Models;

namespace ModbusClient.Interfaces;

public interface IReadingService
{
    /// <summary>
    /// Преобразует сырые данные аналоговых портов в метрики.
    /// </summary>
    IEnumerable<Metric> ProcessAnalog(
        IEnumerable<(int Port, ushort Val)> rawData,
        IEnumerable<SensorSettings> sensors
    );

    /// <summary>
    /// Преобразует сырые данные цифровых портов в метрики.
    /// </summary>
    IEnumerable<Metric> ProcessDigital(
        IEnumerable<(int Port, bool Val)> rawData,
        IEnumerable<SensorSettings> sensors
    );
}
