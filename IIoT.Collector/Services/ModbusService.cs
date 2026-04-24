using System.Net.Sockets;
using IIoT.Collector.Interfaces;
using NModbus;
using Serilog;

namespace IIoT.Collector.Services;

/// <summary>
/// Реализация драйвера Modbus TCP для устройств Advantech ADAM-6017 (и совместимых).
/// </summary>
public class ModbusService : IModbusService
{
    private readonly ILogger _logger = Log.ForContext<ModbusService>();

    // Константы для ADAM-6017
    private const ushort StartAddress = 0;
    private const ushort AnalogCount = 8;
    private const ushort DigitalCount = 2;
    private const ushort ChannelConfig = 0x0008;

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

            // Инициализацию каналов (WriteMultipleRegisters) здесь убираем, 
            // так как она требует конкретный SlaveId, а при коннекте мы его не знаем для всех случаев.
            // ADAM-6017 обычно настроен статически. Если нужно - перенесем в Read.

            return (master, tcpClient);
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to connect to {IP}: {Msg}", ip, ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int Port, ushort Value)>> ReadAnalogAsync(IModbusMaster master, byte slaveId)
    {
        // Function 0x04: Read Input Registers (3xxxx)
        var data = await master.ReadInputRegistersAsync(slaveId, StartAddress, AnalogCount);

        // Превращаем массив ushort[] в список пар (Порт, Значение)
        return data.Select((val, index) => (index, val));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(int Port, bool Value)>> ReadDigitalAsync(IModbusMaster master, byte slaveId)
    {
        // Function 0x02: Read Discrete Inputs (1xxxx)
        var data = await master.ReadInputsAsync(slaveId, StartAddress, DigitalCount);

        return data.Select((val, index) => (index, val));
    }
}
