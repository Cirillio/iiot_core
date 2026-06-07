namespace IIoT.Shared.Models;

/// <summary>
/// Порядок байт и слов при десериализации многорегистровых значений (32/64-бит).
/// Соответствует ENUM 'modbus_endianness' в PostgreSQL.
/// </summary>
/// <remarks>
/// Регистры Modbus всегда 16-битные. 32-битное число занимает 2 регистра, и разные
/// вендоры раскладывают слова/байты по-разному. Обозначения ABCD — порядок байт "по проводу",
/// где A — старший байт старшего слова.
/// </remarks>
public enum ModbusEndianness
{
    /// <summary>
    /// ABCD — старшее слово первым, big-endian внутри слов. Стандарт Modbus по умолчанию.
    /// </summary>
    BigEndian,

    /// <summary>
    /// DCBA — полный реверс байт. Little-endian слова и байты.
    /// </summary>
    LittleEndian,

    /// <summary>
    /// CDAB — младшее слово первым, big-endian внутри слов. Частый случай (напр. Меркурий-230).
    /// </summary>
    WordSwap,

    /// <summary>
    /// BADC — старшее слово первым, но байты внутри слов переставлены.
    /// </summary>
    ByteWordSwap,
}
