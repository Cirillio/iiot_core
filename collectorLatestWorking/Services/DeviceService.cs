using System.Collections.Concurrent;
using System.Net.Sockets;
using ModbusClient.Interfaces;
using ModbusClient.Models;
using NModbus;
using Serilog;

namespace ModbusClient.Services;

/// <summary>
/// Реализация сервиса управления соединениями.
/// Использует пул соединений для переиспользования открытых TCP-сокетов.
/// </summary>
public class DeviceService(IModbusService modbusDriver) : IDeviceService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<DeviceService>();

    // Храним пару (Master, Client)
    // Key: DeviceId, Value: (ModbusMaster, TcpClient)
    private readonly ConcurrentDictionary<
        int,
        (IModbusMaster Master, TcpClient Client)
    > _connections = new();

    /// <inheritdoc />
    public async Task<IModbusMaster?> GetConnectionAsync(Device device, CancellationToken ct)
    {
        // 1. Если соединение есть и клиент подключен — возвращаем мастера
        if (_connections.TryGetValue(device.Id, out var conn))
        {
            if (conn.Client.Connected)
                return conn.Master;

            // Если сокет мертв — чистим, чтобы попробовать переподключиться
            InvalidateConnection(device.Id);
        }

        // 2. Создаем новое подключение через драйвер
        _logger.Debug("Establishing connection to {Name} ({IP})...", device.Name, device.IpAddress);
        var newConn = await modbusDriver.ConnectAsync(device.IpAddress, device.Port, ct);

        if (newConn == null)
            return null;

        // 3. Сохраняем в пул
        _connections[device.Id] = newConn.Value;
        return newConn.Value.Master;
    }

    /// <inheritdoc />
    public void InvalidateConnection(int deviceId)
    {
        if (_connections.TryRemove(deviceId, out var conn))
        {
            try
            {
                // Корректно закрываем TCP соединение
                conn.Client.Dispose();
            }
            catch
            {
                // Игнорируем ошибки при закрытии уже закрытого сокета
            }
            _logger.Debug("Connection for Device {Id} invalidated", deviceId);
        }
    }

    /// <summary>
    /// Освобождает все ресурсы сервиса, закрывая все открытые соединения.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var conn in _connections.Values)
            {
                try
                {
                    conn.Client.Dispose();
                }
                catch
                {
                    // Игнорируем ошибки при Dispose
                }
            }
            _connections.Clear();
            _logger.Debug("Disposing DeviceService and connections.");
        }
    }
}
