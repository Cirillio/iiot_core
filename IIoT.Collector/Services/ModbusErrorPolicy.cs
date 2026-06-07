using System.Net.Sockets;

namespace IIoT.Collector.Services;

/// <summary>
/// Классификатор исключений Modbus: отделяет транспортные сбои (мёртвый сокет)
/// от протокольных ошибок устройства (SlaveException — устройство живо, но вернуло код ошибки).
/// </summary>
public static class ModbusErrorPolicy
{
    /// <summary>
    /// Транспортный сбой — сокет физически мёртв или недоступен.
    /// Такие исключения нужно пробрасывать наверх, чтобы воркер инвалидировал соединение,
    /// а не опрашивал следующие чанки по уже мёртвому сокету (каскад таймаутов).
    /// </summary>
    public static bool IsTransport(Exception ex) =>
        ex is IOException or SocketException or ObjectDisposedException or TimeoutException;
}
