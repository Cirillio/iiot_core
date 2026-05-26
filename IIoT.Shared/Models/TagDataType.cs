namespace IIoT.Shared.Models;

/// <summary>
/// Тип данных тега (точки опроса).
/// Соответствует ENUM 'tag_data_type' в PostgreSQL.
/// Определяет способ обработки "сырых" данных.
/// </summary>
public enum TagDataType
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
    /// Виртуальный тег (вычисляется по формуле из других тегов).
    /// </summary>
    Virtual,
}
