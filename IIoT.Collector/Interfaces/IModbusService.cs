using System.Net.Sockets;
using IIoT.Shared.Models;
using NModbus;

namespace IIoT.Collector.Interfaces;

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
    /// Читает регистры для списка датчиков одного типа.
    /// Группирует смежные адреса в один batch-запрос для эффективности.
    /// Поддерживает многорегистровые датчики (32-bit).
    /// </summary>
    /// <param name="master">Активный Modbus Master.</param>
    /// <param name="slaveId">Unit ID устройства.</param>
    /// <param name="sensors">Список настроек сенсоров одного типа.</param>
    /// <param name="registerType">Тип регистра (Input, Holding, Discrete, Coil).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>
    /// Коллекция кортежей (SensorId, Массив сырых значений).
    /// </returns>
    Task<IEnumerable<(int SensorId, ushort[] RawValues)>> ReadRegistersAsync(
        IModbusMaster master,
        byte slaveId,
        IEnumerable<SensorSettings> sensors,
        ModbusRegisterType registerType,
        CancellationToken ct
    );
}
