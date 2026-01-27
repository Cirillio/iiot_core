using System.Net.Sockets;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using NModbus;
using Serilog;

namespace ModbusClient.Services;

public class ModbusService : IModbusService
{
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private readonly ILogger _logger = Log.ForContext<ModbusService>();

    private const byte UnitId = 1; // Modbus slave ID
    private const ushort StartAddress = 0;
    private const ushort AnalogCount = 8;
    private const ushort DigitalCount = 2;
    private const ushort ChannelConfig = 0x0008; // ADC mode

    public async Task<bool> InitializeAsync(string ipAddress, int port, CancellationToken ct)
    {
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(ipAddress, port, ct);

            var factory = new ModbusFactory();
            _master = factory.CreateMaster(_tcpClient);
            _master.Transport.ReadTimeout = 1500;

            _logger.Information("Connected to ADAM at {IP}", ipAddress);

            // Initialize ports (write configuration 0x0008)
            ushort[] config = [.. Enumerable.Repeat(ChannelConfig, (int)AnalogCount)];
            await _master.WriteMultipleRegistersAsync(UnitId, StartAddress, config);

            _logger.Debug("Channels configured with 0x0008 (ADC mode)");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Modbus connection to {IP}", ipAddress);
            return false;
        }
    }

    public async Task<IEnumerable<RawMeasurement>> ReadAllAnalogAsync(CancellationToken ct)
    {
        if (_master == null)
            return [];

        // Function 0x04 - Read Input Registers
        ushort[] inputs = await _master.ReadInputRegistersAsync(UnitId, StartAddress, AnalogCount);

        return inputs.Select(
            (val, index) =>
                new RawMeasurement
                {
                    PortId = index,
                    RawValue = val,
                    CreatedAt = DateTime.UtcNow,
                }
        );
    }

    public async Task<IEnumerable<DigitalMeasurement>> ReadAllDigitalAsync(CancellationToken ct)
    {
        if (_master == null)
            return [];

        // Function 0x02 - Read Discrete Inputs
        bool[] inputs = await _master.ReadInputsAsync(UnitId, StartAddress, DigitalCount);

        return inputs.Select(
            (val, index) =>
                new DigitalMeasurement
                {
                    PortId = index,
                    Value = val,
                    CreatedAt = DateTime.UtcNow,
                }
        );
    }

    public async Task DisconnectAsync()
    {
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _master?.Dispose();
        _logger.Information("Modbus connection closed");
        await Task.CompletedTask;
    }
}
