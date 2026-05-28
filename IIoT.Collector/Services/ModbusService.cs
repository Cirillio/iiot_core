using System.Net.Sockets;
using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;
using NModbus;
using Serilog;

namespace IIoT.Collector.Services;

/// <summary>
/// Универсальная реализация драйвера Modbus TCP.
/// Поддерживает пакетное чтение различных типов регистров.
/// </summary>
public class ModbusService : IModbusService
{
    private readonly ILogger _logger = Log.ForContext<ModbusService>();

    /// <inheritdoc />
    public async Task<(IModbusMaster Master, TcpClient Client)?> ConnectAsync(
        string ip,
        int port,
        CancellationToken ct
    )
    {
        try
        {
            var tcpClient = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(3));

            await tcpClient.ConnectAsync(ip, port, connectCts.Token);

            var factory = new ModbusFactory();
            var master = factory.CreateMaster(tcpClient);
            master.Transport.ReadTimeout = 2000;
            master.Transport.Retries = 2;

            _logger.Debug("Connected to {IP}:{Port}", ip, port);

            return (master, tcpClient);
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to connect to {IP}: {Msg}", ip, ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int TagId, ushort[] RawValues)>> ReadRegistersAsync(
        IModbusMaster master,
        byte slaveId,
        IEnumerable<TagSettings> tags,
        ModbusRegisterType registerType,
        int maxSpan,
        bool useGroupPolling,
        CancellationToken ct
    )
    {
        var allTags = tags.Where(s => s.RegisterType == registerType)
            .OrderBy(s => s.RegisterAddress)
            .ToList();

        if (allTags.Count == 0)
            return [];

        // Жёсткий лимит PDU зависит от типа таблицы памяти: 16-битные слова — 125 регистров,
        // биты — 2000. maxSpan (заданный на устройстве) может лишь СУЗИТЬ шаг, но не превысить
        // физический максимум протокола, иначе устройство вернёт 0x03 Illegal Data Value.
        // Вызывающий код передаёт лимит, соответствующий типу таблицы (MaxRegisterSpan / MaxBitSpan).
        int hardwareLimit =
            registerType is ModbusRegisterType.HoldingRegister or ModbusRegisterType.InputRegister
                ? 125
                : 2000;
        int effectiveSpan = Math.Min(maxSpan, hardwareLimit);

        var result = new List<(int TagId, ushort[] RawValues)>();
        var chunks = CreateChunks(allTags, effectiveSpan, useGroupPolling);

        foreach (var chunk in chunks)
        {
            var minAddr = (ushort)chunk[0].RegisterAddress;
            var maxTag = chunk.MaxBy(s => s.RegisterAddress + s.RegisterCount - 1);
            var maxAddr = (ushort)(maxTag!.RegisterAddress + maxTag.RegisterCount - 1);
            var count = (ushort)(maxAddr - minAddr + 1);

            try
            {
                ushort[] data;
                switch (registerType)
                {
                    case ModbusRegisterType.InputRegister:
                        data = await master.ReadInputRegistersAsync(slaveId, minAddr, count);
                        break;
                    case ModbusRegisterType.HoldingRegister:
                        data = await master.ReadHoldingRegistersAsync(slaveId, minAddr, count);
                        break;
                    case ModbusRegisterType.DiscreteInput:
                        var di = await master.ReadInputsAsync(slaveId, minAddr, count);
                        data = di.Select(b => (ushort)(b ? 1 : 0)).ToArray();
                        break;
                    case ModbusRegisterType.Coil:
                        var coils = await master.ReadCoilsAsync(slaveId, minAddr, count);
                        data = coils.Select(b => (ushort)(b ? 1 : 0)).ToArray();
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Register type {registerType} not supported"
                        );
                }

                foreach (var s in chunk)
                {
                    var offset = s.RegisterAddress - minAddr;
                    var values = new ushort[s.RegisterCount];
                    Array.Copy(data, offset, values, 0, s.RegisterCount);
                    result.Add((s.TagId, values));
                }
            }
            catch (Exception ex) when (ModbusErrorPolicy.IsTransport(ex))
            {
                // Транспортный сбой — сокет мёртв. Пробрасываем наверх, чтобы воркер
                // инвалидировал соединение, а не долбил следующие чанки по дохлому сокету.
                throw;
            }
            catch (SlaveException ex)
            {
                // Устройство живо, но вернуло код ошибки (illegal address/value) — пропускаем чанк.
                _logger.Warning(
                    "Slave error on chunk {Min}-{Max} ({Type}) from slave {Id}: {Msg}",
                    minAddr,
                    maxAddr,
                    registerType,
                    slaveId,
                    ex.Message
                );
            }
            catch (Exception ex)
            {
                // Неизвестная ошибка — трактуем как сбой соединения (fail-safe) и сбрасываем сокет.
                _logger.Error(
                    ex,
                    "Unexpected error on chunk {Min}-{Max} ({Type}) from slave {Id}",
                    minAddr,
                    maxAddr,
                    registerType,
                    slaveId
                );
                throw;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task WriteTagAsync(
        IModbusMaster master,
        byte slaveId,
        TagSettings tag,
        double value,
        CancellationToken ct
    )
    {
        var address = (ushort)tag.RegisterAddress;

        switch (tag.RegisterType)
        {
            case ModbusRegisterType.Coil:
                // FC05 — реле/клапаны. Любое ненулевое значение трактуем как ON.
                await master.WriteSingleCoilAsync(slaveId, address, value != 0.0);
                break;

            case ModbusRegisterType.HoldingRegister:
                var regs = EncodeValue(value, tag.RawDataType, tag.Endianness);
                if (regs.Length == 1)
                {
                    // FC06 — одиночный 16-битный регистр.
                    await master.WriteSingleRegisterAsync(slaveId, address, regs[0]);
                }
                else
                {
                    // FC16 — группа регистров (32/64-бит) с уже учтённым порядком слов/байт.
                    await master.WriteMultipleRegistersAsync(slaveId, address, regs);
                }
                break;

            default:
                // Защита ядра: запись в Discrete Input / Input Register физически невозможна.
                throw new InvalidOperationException(
                    $"Write is prohibited for register type {tag.RegisterType} (Read-Only)."
                );
        }
    }

    /// <summary>
    /// Сериализует значение в массив регистров согласно бинарному типу и порядку байт/слов тега.
    /// Инверсия распаковки из <c>ProcessService.BuildOrderedBytes</c>.
    /// 16-битные типы пишутся напрямую (порядок байт берёт на себя протокол), многорегистровые —
    /// раскладываются через <see cref="PackOrderedBytes"/>.
    /// </summary>
    private static ushort[] EncodeValue(double value, RawDataType type, ModbusEndianness endianness)
    {
        switch (type)
        {
            case RawDataType.Int16:
                return [unchecked((ushort)(short)Math.Round(value))];
            case RawDataType.UInt16:
                return [unchecked((ushort)(int)Math.Round(value))];
            case RawDataType.Int32:
                return PackOrderedBytes(BitConverter.GetBytes((int)Math.Round(value)), 2, endianness);
            case RawDataType.UInt32:
                return PackOrderedBytes(BitConverter.GetBytes((uint)Math.Round(value)), 2, endianness);
            case RawDataType.Float32:
                return PackOrderedBytes(BitConverter.GetBytes((float)value), 2, endianness);
            case RawDataType.Float64:
                return PackOrderedBytes(BitConverter.GetBytes(value), 4, endianness);
            default:
                return [unchecked((ushort)(int)Math.Round(value))];
        }
    }

    /// <summary>
    /// Раскладывает little-endian байты значения (с x64-хоста) в N регистров согласно порядку слов/байт.
    /// Точная инверсия <c>BuildOrderedBytes</c>.
    /// </summary>
    private static ushort[] PackOrderedBytes(byte[] bytes, int count, ModbusEndianness endianness)
    {
        bool wordReversed =
            endianness is ModbusEndianness.BigEndian or ModbusEndianness.ByteWordSwap;
        bool byteBigInWord =
            endianness is ModbusEndianness.ByteWordSwap or ModbusEndianness.LittleEndian;

        var regs = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            byte b0 = bytes[i * 2],
                b1 = bytes[i * 2 + 1];
            byte hi = byteBigInWord ? b0 : b1;
            byte lo = byteBigInWord ? b1 : b0;
            ushort w = (ushort)((hi << 8) | lo);
            int dstWord = wordReversed ? count - 1 - i : i;
            regs[dstWord] = w;
        }

        return regs;
    }

    /// <summary>
    /// Группирует теги в чанки для чтения.
    /// useGroupPolling=false — каждый тег в собственном чанке (точечные индивидуальные запросы).
    /// useGroupPolling=true — смежные теги объединяются, пока ширина чанка (от первого адреса
    /// до конца текущего тега) не превышает maxRegisterSpan.
    /// </summary>
    private static List<List<TagSettings>> CreateChunks(
        List<TagSettings> tags,
        int maxRegisterSpan,
        bool useGroupPolling
    )
    {
        var chunks = new List<List<TagSettings>>();
        if (tags.Count == 0)
            return chunks;

        // Точечный опрос: один запрос на тег
        if (!useGroupPolling)
        {
            foreach (var t in tags)
                chunks.Add([t]);
            return chunks;
        }

        var currentChunk = new List<TagSettings> { tags[0] };
        chunks.Add(currentChunk);

        for (int i = 1; i < tags.Count; i++)
        {
            var s = tags[i];
            var firstInChunk = currentChunk[0];

            // Ширина чанка от начала до конца текущего тега
            if (
                s.RegisterAddress + s.RegisterCount - firstInChunk.RegisterAddress
                <= maxRegisterSpan
            )
            {
                currentChunk.Add(s);
            }
            else
            {
                currentChunk = new List<TagSettings> { s };
                chunks.Add(currentChunk);
            }
        }

        return chunks;
    }
}
