using IIoT.Shared.Models;

namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// DTO тега для отображения на дашборде. Содержит метаданные, необходимые для визуализации.
/// </summary>
public record DashboardTagDTO
{
    /// <summary>
    /// Уникальный ID тега.
    /// </summary>
    public int TagId { get; init; }

    /// <summary>
    /// ID устройства, к которому привязан тег.
    /// </summary>
    public int DeviceId { get; init; }

    /// <summary>
    /// Номер порта или регистра Modbus.
    /// </summary>
    public int PortNumber { get; init; }

    /// <summary>
    /// Название тега (напр. "Температура котла").
    /// </summary>
    public string Name { get; init; } = "Unnamed tag";

    /// <summary>
    /// Уникальный строковый код (для системных ссылок).
    /// </summary>
    public string Slug { get; init; } = "unnamed-tag";

    /// <summary>
    /// Тип сигнала (ANALOG_RAW, ANALOG_PHYSICAL, DIGITAL).
    /// </summary>
    public TagDataType DataType { get; init; }

    /// <summary>
    /// Единица измерения (напр. "°C").
    /// </summary>
    public string Unit { get; init; } = "UnknownUnit";

    /// <summary>
    /// Конфигурация интерфейса (цвета, иконки, границы).
    /// </summary>
    public TagUiConfig UiConfigJson { get; init; } = new();

    /// <summary>
    /// Время последнего изменения настроек.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
