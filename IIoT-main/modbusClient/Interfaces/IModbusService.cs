using System.Net.Sockets;
using NModbus;

namespace ModbusClient.Interfaces;

/// <summary>
/// Низкоуровневый сервис для работы с протоколом Modbus TCP.
/// Обертка над библиотекой NModbus для выполнения конкретных операций чтения/записи.
/// </summary>
public interface IModbusService
{
    /// <summary>
    /// Устанавливает новое TCP-соединение с устройством и инициализирует Modbus Master.
    /// </summary>
    /// <param name="ip">IP-адрес устройства.</param>
    /// <param name="port">TCP порт (обычно 502).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>
    /// Кортеж из <see cref="IModbusMaster"/> и <see cref="TcpClient"/>, или null в случае ошибки.
    /// TcpClient возвращается для возможности корректного закрытия сокета (Dispose).
    /// </returns>
    Task<(IModbusMaster Master, TcpClient Client)?> ConnectAsync(
        string ip,
        int port,
        CancellationToken ct
    );

    /// <summary>
    /// Читает значения аналоговых входов (Input Registers) с устройства.
    /// Обычно используется функция Modbus 0x04.
    /// </summary>
    /// <param name="master">Активный Modbus Master.</param>
    /// <returns>
    /// Коллекция кортежей (Номер порта/регистра, Сырое значение 0-65535).
    /// </returns>
    Task<IEnumerable<(int Port, ushort Value)>> ReadAnalogAsync(IModbusMaster master);

    /// <summary>
    /// Читает значения дискретных входов (Discrete Inputs) с устройства.
    /// Обычно используется функция Modbus 0x02.
    /// </summary>
    /// <param name="master">Активный Modbus Master.</param>
    /// <returns>
    /// Коллекция кортежей (Номер порта/входа, Значение True/False).
    /// </returns>
    Task<IEnumerable<(int Port, bool Value)>> ReadDigitalAsync(IModbusMaster master);
}
