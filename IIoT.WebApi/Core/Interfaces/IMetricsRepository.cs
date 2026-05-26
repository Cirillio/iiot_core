namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с историческими данными (временными рядами) датчиков.
/// </summary>
public interface IMetricsRepository
{
    /// <summary>
    /// Получить историю показаний для конкретного датчика за указанный период.
    /// </summary>
    /// <param name="tagId">Уникальный ID датчика.</param>
    /// <param name="from">Начальная дата (UTC).</param>
    /// <param name="to">Конечная дата (UTC).</param>
    /// <returns>Коллекция массивов вида [timestamp_ms, value], оптимизированная для ECharts.</returns>
    Task<IEnumerable<object[]>> GetHistoryAsync(int tagId, DateTime from, DateTime to);
}
