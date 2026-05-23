namespace IIoT.Shared.Models;

/// <summary>
/// Перечисление типов данных датчиков.
/// Соответствует пользовательскому типу данных (ENUM) 'sensor_data_type' в базе данных PostgreSQL.
/// Определяет способ обработки "сырых" данных.
/// </summary>
public enum SensorDataType
{
    /// <summary>
    /// Аналоговый сигнал (например, температура, давление).
    /// </summary>
    Analog,

    /// <summary>
    /// Дискретный сигнал (0 или 1).
    /// </summary>
    Digital,

    /// <summary>
    /// Виртуальный датчик.
    /// </summary>
    Virtual,
}
