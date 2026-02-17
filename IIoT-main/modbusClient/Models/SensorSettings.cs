namespace ModbusClient.Models;

/// <summary>
/// Полная конфигурация датчика (сенсора).
/// Содержит все необходимые метаданные для опроса, калибровки, расчетов и отображения в UI.
/// </summary>
public record SensorSettings
{
    // --- Идентификаторы ---

    /// <summary>
    /// Уникальный идентификатор датчика в БД (Primary Key).
    /// </summary>
    public int SensorId { get; init; }

    /// <summary>
    /// Идентификатор устройства, к которому подключен датчик (Foreign Key).
    /// Может быть null для виртуальных датчиков.
    /// </summary>
    public int? DeviceId { get; init; }

    /// <summary>
    /// Номер регистра или канала на устройстве.
    /// Nullable, так как может отсутствовать у виртуальных датчиков.
    /// </summary>
    public int? PortNumber { get; init; }

    // --- Описательные данные ---

    /// <summary>
    /// Человекочитаемое название датчика.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Текстовый идентификатор (slug) для использования в формулах, URL API и системных ссылках.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// Тип данных датчика (Аналоговый, Дискретный, Виртуальный).
    /// </summary>
    public SensorDataType DataType { get; init; } = SensorDataType.ANALOG;

    /// <summary>
    /// Единица измерения физической величины (например, "°C", "Bar", "V", "%").
    /// </summary>
    public string? Unit { get; init; }

    // --- Границы сигнала (Линейная интерполяция) ---

    /// <summary>
    /// Минимальное "сырое" значение, ожидаемое с устройства (обычно 0).
    /// </summary>
    public double InputMin { get; init; }

    /// <summary>
    /// Максимальное "сырое" значение, ожидаемое с устройства (например, 65535 для 16-бит).
    /// </summary>
    public double InputMax { get; init; }

    /// <summary>
    /// Физическое значение, соответствующее InputMin (например, 0 вольт = -50 градусов).
    /// </summary>
    public double OutputMin { get; init; }

    /// <summary>
    /// Физическое значение, соответствующее InputMax (например, 10 вольт = 150 градусов).
    /// </summary>
    public double OutputMax { get; init; }

    // --- Математика и калибровка ---

    /// <summary>
    /// Значение смещения, добавляемое к результату после масштабирования.
    /// Используется для точной калибровки "нуля".
    /// </summary>
    public double OffsetVal { get; init; }

    /// <summary>
    /// Математическая формула для расчета значения виртуального датчика.
    /// Может содержать slug-и других датчиков.
    /// </summary>
    public string? Formula { get; init; }

    // --- Дополнительные метаданные ---

    /// <summary>
    /// Конфигурация отображения в UI (цвет графиков, иконки, границы предупреждений) в формате JSON.
    /// </summary>
    public string UiConfigJson { get; init; } = "{}";

    /// <summary>
    /// Дата последнего обновления настроек. Используется для кэширования конфигурации.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
