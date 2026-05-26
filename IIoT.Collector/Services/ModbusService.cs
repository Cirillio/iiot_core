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
        int maxRegisterSpan,
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
        // биты — 2000. device.MaxRegisterSpan может лишь СУЗИТЬ шаг, но не превысить физический
        // максимум протокола, иначе устройство вернёт 0x03 Illegal Data Value.
        int hardwareLimit =
            registerType is ModbusRegisterType.HoldingRegister or ModbusRegisterType.InputRegister
                ? 125
                : 2000;
        int effectiveSpan = Math.Min(maxRegisterSpan, hardwareLimit);

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
            catch (Exception ex)
            {
                _logger.Warning(
                    "Failed to read chunk {Min}-{Max} ({Type}) from slave {Id}: {Msg}",
                    minAddr,
                    maxAddr,
                    registerType,
                    slaveId,
                    ex.Message
                );
                // Пропускаем этот чанк, но продолжаем опрос других
            }
        }

        return result;
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
