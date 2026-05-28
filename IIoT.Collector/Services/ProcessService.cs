using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;

namespace IIoT.Collector.Services;

/// <summary>
/// Сервис для обработки "сырых" значений с датчиков.
/// </summary>
public class ProcessService : IProcessService
{
    /// <inheritdoc />
    public IEnumerable<Metric> Process(
        IEnumerable<(int TagId, ushort[] RawValues)> rawData,
        IEnumerable<TagSettings> tags
    )
    {
        var timestamp = DateTime.UtcNow;
        var tagMap = tags.ToDictionary(s => s.TagId);

        foreach (var (tagId, rawArray) in rawData)
        {
            if (!tagMap.TryGetValue(tagId, out var tag))
                continue;

            // Сколько регистров требует тип; если данных меньше — тег пропускаем.
            int needed = tag.RawDataType switch
            {
                RawDataType.Int16 or RawDataType.UInt16 => 1,
                RawDataType.Int32 or RawDataType.UInt32 or RawDataType.Float32 => 2,
                RawDataType.Float64 => 4,
                _ => 1,
            };

            if (rawArray.Length < needed)
                continue;

            double finalRawValue = tag.RawDataType switch
            {
                RawDataType.Int16 => (short)rawArray[0],
                RawDataType.UInt16 => rawArray[0],
                RawDataType.Int32 => BitConverter.ToInt32(
                    BuildOrderedBytes(rawArray, 2, tag.Endianness),
                    0
                ),
                RawDataType.UInt32 => BitConverter.ToUInt32(
                    BuildOrderedBytes(rawArray, 2, tag.Endianness),
                    0
                ),
                RawDataType.Float32 => BitConverter.ToSingle(
                    BuildOrderedBytes(rawArray, 2, tag.Endianness),
                    0
                ),
                RawDataType.Float64 => BitConverter.ToDouble(
                    BuildOrderedBytes(rawArray, 4, tag.Endianness),
                    0
                ),
                _ => rawArray[0],
            };

            yield return new Metric
            {
                Time = timestamp,
                TagId = tag.TagId,
                RawValue = finalRawValue,
                Value = TagExtensions.Calculate(finalRawValue, tag),
            };
        }
    }

    /// <summary>
    /// Раскладывает N 16-битных регистров в массив байт согласно порядку слов/байт тега.
    /// Работает для 32-бит (count=2, Float) и 64-бит (count=4, Double).
    /// Возвращает байты в порядке LSB-first — готовые для BitConverter на little-endian хосте (x86/x64).
    /// </summary>
    /// <remarks>
    /// Два независимых преобразования относительно полученного порядка регистров (reg0 — младший адрес):
    /// слово-реверс (старшее слово первым) и реверс байт внутри слова. Их комбинации дают 4 режима:
    /// BigEndian (ABCD), WordSwap (CDAB), ByteWordSwap (BADC), LittleEndian (DCBA).
    /// </remarks>
    private static byte[] BuildOrderedBytes(ushort[] regs, int count, ModbusEndianness endianness)
    {
        bool wordReversed =
            endianness is ModbusEndianness.BigEndian or ModbusEndianness.ByteWordSwap;
        bool byteBigInWord =
            endianness is ModbusEndianness.ByteWordSwap or ModbusEndianness.LittleEndian;

        var bytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            int srcWord = wordReversed ? count - 1 - i : i;
            ushort w = regs[srcWord];
            byte hi = (byte)(w >> 8),
                lo = (byte)(w & 0xFF);
            bytes[i * 2] = byteBigInWord ? hi : lo;
            bytes[i * 2 + 1] = byteBigInWord ? lo : hi;
        }

        return bytes;
    }
}
