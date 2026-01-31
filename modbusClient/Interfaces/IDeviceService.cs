using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModbusClient.Models;
using NModbus;

namespace ModbusClient.Interfaces;

public interface IDeviceService
{
    /// <summary>
    /// Возвращает готового мастера для устройства.
    /// Если соединения нет — пытается подключиться.
    /// </summary>
    Task<IModbusMaster?> GetConnectionAsync(Device device, CancellationToken ct);

    /// <summary>
    /// Принудительно разрывает соединение (например, при ошибке ввода-вывода).
    /// </summary>
    void InvalidateConnection(int deviceId);
}
