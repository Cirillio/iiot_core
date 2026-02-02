using ModbusClient.Models;

namespace ModbusClient.Interfaces;

/// <summary>
/// Сервис бизнес-логики обработки "сырых" данных.
/// Отвечает за маппинг данных с портов на конкретные сенсоры и применение калибровок.
/// </summary>
public interface IReadingService
{
    /// <summary>
    /// Обрабатывает сырые данные с аналоговых портов.
    /// Сопоставляет порт устройства с настройками сенсора и вычисляет физическое значение.
    /// </summary>
    /// <param name="rawData">Коллекция сырых данных (Порт, Значение).</param>
    /// <param name="sensors">Список настроек сенсоров, относящихся к опрашиваемому устройству.</param>
    /// <returns>Коллекция готовых метрик для сохранения.</returns>
    IEnumerable<Metric> ProcessAnalog(
        IEnumerable<(int Port, ushort Val)> rawData,
        IEnumerable<SensorSettings> sensors
    );

    /// <summary>
    /// Обрабатывает сырые данные с цифровых (дискретных) портов.
    /// Сопоставляет порт устройства с настройками сенсора и формирует метрику (0 или 1).
    /// </summary>
    /// <param name="rawData">Коллекция сырых данных (Порт, Значение bool).</param>
    /// <param name="sensors">Список настроек сенсоров, относящихся к опрашиваемому устройству.</param>
    /// <returns>Коллекция готовых метрик для сохранения.</returns>
    IEnumerable<Metric> ProcessDigital(
        IEnumerable<(int Port, bool Val)> rawData,
        IEnumerable<SensorSettings> sensors
    );
}
