namespace IIoT.Shared.Models;

/// <summary>
/// Бинарная раскладка сырого значения тега в регистрах Modbus.
/// Ортогональна <see cref="TagDataType"/>: RawDataType определяет КАК распаковать байты,
/// TagDataType — что с распакованным числом делать (масштабировать / отдать как есть).
/// Соответствует ENUM 'modbus_raw_data_type' в PostgreSQL.
/// </summary>
/// <remarks>
/// Раскладка по словам/байтам для многорегистровых типов управляется <see cref="ModbusEndianness"/>.
/// Int16/UInt16 занимают 1 регистр; Int32/UInt32/Float32 — 2; Float64 — 4.
/// </remarks>
public enum RawDataType
{
    /// <summary>Знаковое 16-битное целое (1 регистр). Напр. температура -15 °C = 0xFFF1.</summary>
    Int16,

    /// <summary>Беззнаковое 16-битное целое (1 регистр). Дефолт для сырого АЦП.</summary>
    UInt16,

    /// <summary>Знаковое 32-битное целое (2 регистра).</summary>
    Int32,

    /// <summary>Беззнаковое 32-битное целое (2 регистра). Накопительные счётчики (кВт·ч, м³).</summary>
    UInt32,

    /// <summary>32-битное число с плавающей точкой IEEE-754 (2 регистра).</summary>
    Float32,

    /// <summary>64-битное число с плавающей точкой IEEE-754 (4 регистра).</summary>
    Float64,
}
