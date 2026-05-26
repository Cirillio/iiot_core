using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIoT.Shared.Models;
using NModbus;

namespace IIoT.Collector.Interfaces;

/// <summary>
/// Сервис управления физическими соединениями Modbus TCP.
/// Пул сокетов индексируется по ConnectionId — несколько устройств (Slave ID) могут
/// делить одно соединение (RS-485→TCP шлюз). Доступ к сокету сериализуется семафором.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Получает активный Modbus Master для физического соединения.
    /// Если соединение уже установлено — возвращает его, иначе подключается.
    /// </summary>
    /// <param name="connection">Физическое соединение (ip:port).</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Экземпляр <see cref="IModbusMaster"/> или null, если подключение не удалось.</returns>
    Task<IModbusMaster?> GetConnectionAsync(ModbusConnection connection, CancellationToken ct);

    /// <summary>
    /// Помечает соединение как невалидное (при ошибке IO) — закрывает сокет.
    /// При следующем <see cref="GetConnectionAsync"/> будет переподключение.
    /// </summary>
    /// <param name="connectionId">ID соединения, которое нужно сбросить.</param>
    void InvalidateConnection(int connectionId);

    /// <summary>
    /// Возвращает семафор (1,1) для сериализации доступа к TCP-сессии соединения.
    /// Все опросы устройств за одним ConnectionId должны захватывать этот семафор,
    /// чтобы не перемешивать Modbus TCP-фреймы в рамках одной сессии.
    /// </summary>
    /// <param name="connectionId">ID соединения.</param>
    SemaphoreSlim GetLock(int connectionId);
}
