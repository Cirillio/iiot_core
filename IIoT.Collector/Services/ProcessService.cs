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

            double finalRawValue;

            if (tag.RegisterCount == 2 && rawArray.Length >= 2)
            {
                finalRawValue = BitConverter.ToSingle(
                    BuildOrderedBytes(rawArray, 2, tag.Endianness),
                    0
                );
            }
            else if (tag.RegisterCount == 4 && rawArray.Length >= 4)
            {
                finalRawValue = BitConverter.ToDouble(
                    BuildOrderedBytes(rawArray, 4, tag.Endianness),
                    0
                );
            }
            else if (rawArray.Length > 0)
            {
                finalRawValue = rawArray[0];
            }
            else
            {
                continue;
            }

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
