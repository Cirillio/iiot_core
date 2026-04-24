using IIoT.Shared.Models;

namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// DTO датчика для отображения на дашборде. Содержит метаданные, необходимые для визуализации.
/// </summary>
public record DashboardSensorDTO
{
    /// <summary>
    /// Уникальный ID датчика.
    /// </summary>
    public int SensorId { get; init; }

    /// <summary>
    /// ID устройства, к которому подключен датчик.
    /// </summary>
    public int DeviceId { get; init; }

    /// <summary>
    /// Номер порта или регистра Modbus.
    /// </summary>
    public int PortNumber { get; init; }

    /// <summary>
    /// Название датчика (напр. "Температура котла").
    /// </summary>
    public string Name { get; init; } = "Unnamed sensor";

    /// <summary>
    /// Уникальный строковый код (для системных ссылок).
    /// </summary>
    public string Slug { get; init; } = "unnamed-sensor";

    /// <summary>
    /// Тип сигнала (ANALOG, DIGITAL, VIRTUAL).
    /// </summary>
    public SensorDataType SensorDataType { get; init; }

    /// <summary>
    /// Единица измерения (напр. "°C").
    /// </summary>
    public string Unit { get; init; } = "UnknownUnit";

    /// <summary>
    /// Конфигурация интерфейса (цвета, иконки, границы).
    /// </summary>
    public SensorUiConfig UiConfigJson { get; init; } = new();

    /// <summary>
    /// Время последнего изменения настроек.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
