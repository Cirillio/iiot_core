using System.Collections.Concurrent;
using System.Net.Sockets;
using IIoT.Collector.Interfaces;
using IIoT.Shared.Models;
using NModbus;
using Serilog;

namespace IIoT.Collector.Services;

/// <summary>
/// Реализация сервиса управления соединениями.
/// Использует пул соединений для переиспользования открытых TCP-сокетов.
/// </summary>
public class DeviceService(IModbusService modbusDriver) : IDeviceService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<DeviceService>();

    // Пул сокетов. Key: ConnectionId, Value: (ModbusMaster, TcpClient)
    private readonly ConcurrentDictionary<
        int,
        (IModbusMaster Master, TcpClient Client)
    > _connections = new();

    // Семафоры сериализации доступа к TCP-сессии. Key: ConnectionId
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    /// <inheritdoc />
    public SemaphoreSlim GetLock(int connectionId) =>
        _locks.GetOrAdd(connectionId, _ => new SemaphoreSlim(1, 1));

    /// <inheritdoc />
    public async Task<IModbusMaster?> GetConnectionAsync(
        ModbusConnection connection,
        CancellationToken ct
    )
    {
        // 1. Если соединение есть и клиент подключен — возвращаем мастера
        if (_connections.TryGetValue(connection.Id, out var conn))
        {
            if (conn.Client.Connected)
                return conn.Master;

            // Если сокет мертв — чистим, чтобы попробовать переподключиться
            InvalidateConnection(connection.Id);
        }

        // 2. Создаем новое подключение через драйвер
        _logger.Debug(
            "Establishing connection {Id} ({IP}:{Port})...",
            connection.Id,
            connection.IpAddress,
            connection.Port
        );
        var newConn = await modbusDriver.ConnectAsync(connection.IpAddress, connection.Port, ct);

        if (newConn == null)
            return null;

        // 3. Сохраняем в пул
        _connections[connection.Id] = newConn.Value;
        return newConn.Value.Master;
    }

    /// <inheritdoc />
    public void InvalidateConnection(int connectionId)
    {
        if (_connections.TryRemove(connectionId, out var conn))
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
            _logger.Debug("Connection {Id} invalidated", connectionId);
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

            foreach (var sem in _locks.Values)
                sem.Dispose();
            _locks.Clear();

            _logger.Debug("Disposing DeviceService and connections.");
        }
    }
}
