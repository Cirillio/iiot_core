using IIoT.Shared.Models;

namespace IIoT.Collector.Interfaces;

/// <summary>
/// Сервис бизнес-логики обработки "сырых" данных.
/// Отвечает за маппинг данных с портов на конкретные теги и применение калибровок.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Обрабатывает сырые данные с Modbus-устройств.
    /// Применяет десериализацию (endianness), масштабирование и калибровки согласно настройкам тега.
    /// Поддерживает многорегистровые значения (32-bit).
    /// </summary>
    /// <param name="rawData">Коллекция сырых данных (TagId, RawValues массив).</param>
    /// <param name="tags">Список всех настроек тегов опрашиваемого устройства.</param>
    /// <returns>Коллекция готовых метрик для сохранения.</returns>
    IEnumerable<Metric> Process(
        IEnumerable<(int TagId, ushort[] RawValues)> rawData,
        IEnumerable<TagSettings> tags
    );
}
