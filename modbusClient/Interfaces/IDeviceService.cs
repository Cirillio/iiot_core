using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModbusClient.Models;
using NModbus;

namespace ModbusClient.Interfaces;

/// <summary>
/// Сервис управления соединениями с Modbus-устройствами.
/// Отвечает за создание, кэширование и восстановление подключений.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Получает активный экземпляр Modbus Master для указанного устройства.
    /// Если соединение уже установлено и активно, возвращает его.
    /// Если соединения нет, пытается подключиться.
    /// </summary>
    /// <param name="device">Устройство, к которому нужно подключиться.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>
    /// Экземпляр <see cref="IModbusMaster"/> или null, если подключение не удалось.
    /// </returns>
    Task<IModbusMaster?> GetConnectionAsync(Device device, CancellationToken ct);

    /// <summary>
    /// Принудительно помечает соединение с устройством как невалидное (например, при ошибке IO).
    /// При следующем запросе <see cref="GetConnectionAsync"/> будет предпринята попытка переподключения.
    /// </summary>
    /// <param name="deviceId">ID устройства, соединение с которым нужно сбросить.</param>
    void InvalidateConnection(int deviceId);
}
