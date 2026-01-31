using ModbusClient.Models;

namespace ModbusClient.Interfaces;

public interface IBufferRepository
{
    /// <summary>
    /// Создает файл БД и таблицы.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Сохраняет метрики в локальный буфер.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Metric> metrics);

    /// <summary>
    /// Берет N самых старых метрик без удаления (Peek).
    /// </summary>
    Task<IEnumerable<Metric>> PeekAsync(int count);

    /// <summary>
    /// Удаляет N самых старых записей (FIFO).
    /// </summary>
    Task RemoveOldestAsync(int count);

    /// <summary>
    /// Возвращает количество записей в буфере.
    /// </summary>
    Task<long> CountAsync();
}
