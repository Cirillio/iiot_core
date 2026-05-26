namespace IIoT.Shared.Models;

/// <summary>
/// Настройки визуализации тега в пользовательском интерфейсе (Web/Mobile).
/// Хранится в БД как JSONB.
/// </summary>
public record TagUiConfig
{
    /// <summary>
    /// HEX-код цвета для графиков и индикаторов (напр. "#FF5733").
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Нижний критический порог (авария).
    /// </summary>
    public double? MinCritical { get; init; }

    /// <summary>
    /// Нижний предупредительный порог (внимание).
    /// </summary>
    public double? MinWarning { get; init; }

    /// <summary>
    /// Верхний предупредительный порог (внимание).
    /// </summary>
    public double? MaxWarning { get; init; }

    /// <summary>
    /// Верхний критический порог (авария).
    /// </summary>
    public double? MaxCritical { get; init; }

    /// <summary>
    /// Предупредительный порог для дискретных тегов.
    /// </summary>
    public double? DigitalWarning { get; init; }

    /// <summary>
    /// Критический порог для дискретных тегов.
    /// </summary>
    public double? DigitalCritical { get; init; }

    /// <summary>
    /// Метка для состояния 0.
    /// </summary>
    public string? LabelZero { get; init; }

    /// <summary>
    /// Метка для состояния 1.
    /// </summary>
    public string? LabelOne { get; init; }
}
