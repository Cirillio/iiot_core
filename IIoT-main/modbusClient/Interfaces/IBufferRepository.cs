using ModbusClient.Models;

namespace ModbusClient.Interfaces;

/// <summary>
/// Интерфейс для работы с локальным буфером данных (SQLite).
/// Используется для временного хранения метрик при отсутствии связи с основной базой данных.
/// </summary>
public interface IBufferRepository
{
    /// <summary>
    /// Инициализирует репозиторий: создает файл базы данных и необходимые таблицы, если они не существуют.
    /// Должен вызываться при старте приложения.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Сохраняет пакет метрик в локальный буфер.
    /// </summary>
    /// <param name="metrics">Коллекция метрик для сохранения.</param>
    Task AddRangeAsync(IEnumerable<Metric> metrics);

    /// <summary>
    /// Возвращает заданное количество самых старых метрик из буфера, не удаляя их.
    /// Используется для попытки отправки данных в основную БД.
    /// </summary>
    /// <param name="count">Количество записей для получения.</param>
    /// <returns>Коллекция метрик.</returns>
    Task<IEnumerable<Metric>> PeekAsync(int count);

    /// <summary>
    /// Удаляет заданное количество самых старых записей из буфера.
    /// Вызывается после успешного подтверждения сохранения данных в основной БД.
    /// Реализует принцип FIFO (First In, First Out).
    /// </summary>
    /// <param name="count">Количество удаляемых записей.</param>
    Task RemoveOldestAsync(int count);

    /// <summary>
    /// Возвращает текущее количество записей в буфере.
    /// Используется для мониторинга заполненности буфера.
    /// </summary>
    /// <returns>Количество записей (long).</returns>
    Task<long> CountAsync();
}
