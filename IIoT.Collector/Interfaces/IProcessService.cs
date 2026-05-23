using IIoT.Shared.Models;

namespace IIoT.Collector.Interfaces;

/// <summary>
/// Сервис бизнес-логики обработки "сырых" данных.
/// Отвечает за маппинг данных с портов на конкретные сенсоры и применение калибровок.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Обрабатывает сырые данные с Modbus-устройств.
    /// Применяет масштабирование, формулы и калибровки согласно настройкам сенсора.
    /// Поддерживает многорегистровые значения (32-bit).
    /// </summary>
    /// <param name="rawData">Коллекция сырых данных (SensorId, RawValues массив).</param>
    /// <param name="sensors">Список всех настроек сенсоров опрашиваемого устройства.</param>
    /// <returns>Коллекция готовых метрик для сохранения.</returns>
    IEnumerable<Metric> Process(
        IEnumerable<(int SensorId, ushort[] RawValues)> rawData,
        IEnumerable<SensorSettings> sensors
    );
}
