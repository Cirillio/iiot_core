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
    /// Читает регистры для списка тегов одного типа.
    /// При useGroupPolling=true группирует смежные адреса в batch-запросы (ширина чанка
    /// ограничена maxSpan); при false — каждый тег читается отдельным запросом.
    /// Поддерживает многорегистровые теги (32-bit).
    /// </summary>
    /// <param name="master">Активный Modbus Master.</param>
    /// <param name="slaveId">Unit ID устройства.</param>
    /// <param name="tags">Список настроек тегов одного типа.</param>
    /// <param name="registerType">Тип регистра (Input, Holding, Discrete, Coil).</param>
    /// <param name="maxSpan">Максимальная ширина чанка для этого типа таблицы (регистры — MaxRegisterSpan, биты — MaxBitSpan). Урезается жёстким лимитом протокола.</param>
    /// <param name="useGroupPolling">Включить групповой опрос смежных регистров.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>
    /// Коллекция кортежей (TagId, Массив сырых значений).
    /// </returns>
    Task<IEnumerable<(int TagId, ushort[] RawValues)>> ReadRegistersAsync(
        IModbusMaster master,
        byte slaveId,
        IEnumerable<TagSettings> tags,
        ModbusRegisterType registerType,
        int maxSpan,
        bool useGroupPolling,
        CancellationToken ct
    );

    /// <summary>
    /// Записывает значение в исполнительный механизм. Разрешено только для Coil (FC05)
    /// и Holding Register (FC06 для 16-бит, FC16 для 32/64-бит с учётом Endianness).
    /// </summary>
    /// <param name="master">Активный Modbus Master.</param>
    /// <param name="slaveId">Unit ID устройства.</param>
    /// <param name="tag">Целевой тег (адрес, тип регистра, бинарный тип, порядок байт).</param>
    /// <param name="value">Записываемое значение в "сыром" регистровом представлении (Coil: 0/1).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <exception cref="InvalidOperationException">Тип регистра запрещён для записи (Read-Only).</exception>
    Task WriteTagAsync(
        IModbusMaster master,
        byte slaveId,
        TagSettings tag,
        double value,
        CancellationToken ct
    );
}
