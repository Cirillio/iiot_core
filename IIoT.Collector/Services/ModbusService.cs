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
    public async Task<IEnumerable<(int SensorId, ushort[] RawValues)>> ReadRegistersAsync(
        IModbusMaster master,
        byte slaveId,
        IEnumerable<SensorSettings> sensors,
        ModbusRegisterType registerType,
        CancellationToken ct
    )
    {
        var allSensors = sensors
            .Where(s => s.RegisterType == registerType)
            .OrderBy(s => s.RegisterAddress)
            .ToList();

        if (allSensors.Count == 0) return [];

        var result = new List<(int SensorId, ushort[] RawValues)>();
        var chunks = CreateChunks(allSensors, 120);

        foreach (var chunk in chunks)
        {
            var minAddr = (ushort)chunk[0].RegisterAddress;
            var maxSensor = chunk.MaxBy(s => s.RegisterAddress + s.RegisterCount - 1);
            var maxAddr = (ushort)(maxSensor!.RegisterAddress + maxSensor.RegisterCount - 1);
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
                        throw new NotSupportedException($"Register type {registerType} not supported");
                }

                foreach (var s in chunk)
                {
                    var offset = s.RegisterAddress - minAddr;
                    var values = new ushort[s.RegisterCount];
                    Array.Copy(data, offset, values, 0, s.RegisterCount);
                    result.Add((s.SensorId, values));
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to read chunk {Min}-{Max} ({Type}) from slave {Id}: {Msg}", 
                    minAddr, maxAddr, registerType, slaveId, ex.Message);
                // Пропускаем этот чанк, но продолжаем опрос других
            }
        }

        return result;
    }

    /// <summary>
    /// Группирует сенсоры в чанки, где расстояние между регистрами не превышает maxSpan.
    /// </summary>
    private static List<List<SensorSettings>> CreateChunks(List<SensorSettings> sensors, int maxSpan)
    {
        var chunks = new List<List<SensorSettings>>();
        if (sensors.Count == 0) return chunks;

        var currentChunk = new List<SensorSettings> { sensors[0] };
        chunks.Add(currentChunk);

        for (int i = 1; i < sensors.Count; i++)
        {
            var s = sensors[i];
            var firstInChunk = currentChunk[0];
            
            // Расстояние от начала чанка до конца текущего сенсора
            if (s.RegisterAddress + s.RegisterCount - firstInChunk.RegisterAddress <= maxSpan)
            {
                currentChunk.Add(s);
            }
            else
            {
                currentChunk = new List<SensorSettings> { s };
                chunks.Add(currentChunk);
            }
        }

        return chunks;
    }
}
