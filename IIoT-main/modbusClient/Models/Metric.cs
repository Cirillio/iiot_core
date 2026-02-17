namespace ModbusClient.Models;

/// <summary>
/// Представляет единичное измерение (метрику), полученное с датчика.
/// Подготовлена для записи в гипертаблицу TimescaleDB 'metrics'.
/// </summary>
public record Metric
{
    /// <summary>
    /// Временная метка измерения (UTC).
    /// Является частью составного первичного ключа в TimescaleDB.
    /// </summary>
    public DateTime Time { get; init; }

    /// <summary>
    /// Идентификатор датчика, к которому относится измерение.
    /// Внешний ключ на таблицу 'sensors'.
    /// </summary>
    public int SensorId { get; init; }

    /// <summary>
    /// "Сырое" значение, полученное напрямую с устройства (до калибровки).
    /// Может быть null, если измерение отсутствует или некорректно.
    /// Тип соответствует DOUBLE PRECISION в базе данных.
    /// </summary>
    public double? RawValue { get; init; }

    /// <summary>
    /// Финальное (откалиброванное) физическое значение.
    /// Рассчитывается на основе RawValue с применением коэффициентов масштабирования и смещения.
    /// </summary>
    public double Value { get; init; }
}
