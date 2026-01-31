using System.Net.Sockets;
using NModbus;

namespace ModbusClient.Interfaces;

public interface IModbusService
{
    /// <summary>
    /// Создает новое физическое подключение и настраивает контроллер (0x0008).
    /// </summary>
    Task<(IModbusMaster Master, TcpClient Client)?> ConnectAsync(
        string ip,
        int port,
        CancellationToken ct
    );

    /// <summary>
    /// Читает сырые данные аналоговых входов (0-65535).
    /// </summary>
    Task<IEnumerable<(int Port, ushort Value)>> ReadAnalogAsync(IModbusMaster master);

    /// <summary>
    /// Читает сырые данные цифровых входов (True/False).
    /// </summary>
    Task<IEnumerable<(int Port, bool Value)>> ReadDigitalAsync(IModbusMaster master);
}
