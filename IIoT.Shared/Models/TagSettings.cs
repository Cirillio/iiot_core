namespace IIoT.Shared.Models;

/// <summary>
/// Полная конфигурация тега (точки опроса / датчика).
/// Содержит метаданные для опроса, десериализации, калибровки, расчётов и отображения в UI.
/// Соответствует таблице 'tags'.
/// </summary>
public record TagSettings
{
    // --- Идентификаторы ---

    /// <summary>
    /// Уникальный идентификатор тега в БД (Primary Key).
    /// </summary>
    public int TagId { get; init; }

    /// <summary>
    /// Идентификатор устройства, к которому привязан тег (Foreign Key).
    /// Может быть null для виртуальных тегов.
    /// </summary>
    public int? DeviceId { get; init; }

    /// <summary>
    /// Номер канала/порта на устройстве. Nullable — может отсутствовать у виртуальных тегов.
    /// </summary>
    public int? PortNumber { get; init; }

    /// <summary>
    /// Адрес регистра Modbus (0-65535, физический 0-based).
    /// </summary>
    public int RegisterAddress { get; init; }

    /// <summary>
    /// Тип регистра Modbus — определяет Function Code при чтении.
    /// </summary>
    public ModbusRegisterType RegisterType { get; init; } = ModbusRegisterType.InputRegister;

    /// <summary>
    /// Количество последовательных регистров (1 для 16-bit, 2 для 32-bit).
    /// </summary>
    public int RegisterCount { get; init; } = 1;

    /// <summary>
    /// Бинарный тип сырого значения — определяет распаковку байт из регистров.
    /// </summary>
    public RawDataType RawDataType { get; init; } = RawDataType.UInt16;

    /// <summary>
    /// Порядок байт/слов для десериализации многорегистровых значений.
    /// </summary>
    public ModbusEndianness Endianness { get; init; } = ModbusEndianness.BigEndian;

    // --- Описательные данные ---

    /// <summary>
    /// Человекочитаемое название тега.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Текстовый идентификатор (slug) для формул, URL API и системных ссылок.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// Тип данных тега (сырой АЦП, физическая величина, дискретный, виртуальный).
    /// </summary>
    public TagDataType DataType { get; init; } = TagDataType.AnalogRaw;

    /// <summary>
    /// Единица измерения физической величины ("°C", "Bar", "V", "%").
    /// </summary>
    public string? Unit { get; init; }

    // --- Границы сигнала (линейная интерполяция) ---

    /// <summary>
    /// Минимальное "сырое" значение с устройства (обычно 0).
    /// </summary>
    public double InputMin { get; init; }

    /// <summary>
    /// Максимальное "сырое" значение с устройства (например, 65535 для 16-бит).
    /// </summary>
    public double InputMax { get; init; }

    /// <summary>
    /// Физическое значение, соответствующее InputMin.
    /// </summary>
    public double OutputMin { get; init; }

    /// <summary>
    /// Физическое значение, соответствующее InputMax.
    /// </summary>
    public double OutputMax { get; init; }

    // --- Математика и калибровка ---

    /// <summary>
    /// Смещение, добавляемое к результату после масштабирования (калибровка нуля).
    /// </summary>
    public double OffsetVal { get; init; }

    /// <summary>
    /// Индивидуальный порог нечувствительности (Deadband) для этого тега.
    /// null → используется глобальный SystemConfig.DeadbandThreshold.
    /// </summary>
    public double? DeadbandThreshold { get; init; }

    /// <summary>
    /// Формула для расчёта виртуального тега. Может содержать slug-и других тегов.
    /// </summary>
    public string? Formula { get; init; }

    // --- Дополнительные метаданные ---

    /// <summary>
    /// Конфигурация отображения в UI (JSON).
    /// </summary>
    public TagUiConfig? UiConfigJson { get; init; }

    /// <summary>
    /// Дата последнего обновления настроек. Используется для инвалидации кэша конфигурации.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
