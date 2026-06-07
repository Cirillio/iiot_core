using IIoT.WebApi.Data.DTO;

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

    /// <summary>
    /// Последнее показание по каждому тегу — снимок для инициализации UI.
    /// </summary>
    Task<IEnumerable<LatestMetricDto>> GetLatestAsync();

    /// <summary>
    /// Постраничная выборка сырых метрик для табличного просмотра.
    /// Возвращает строки в порядке убывания времени (новые сверху) и общее число строк под фильтр.
    /// </summary>
    /// <param name="tagId">Фильтр по тегу; null — все теги.</param>
    /// <param name="from">Нижняя граница времени (UTC); null — без ограничения.</param>
    /// <param name="to">Верхняя граница времени (UTC); null — без ограничения.</param>
    /// <param name="page">Номер страницы (1-based).</param>
    /// <param name="pageSize">Размер страницы.</param>
    Task<PagedResult<RawMetricDto>> GetRawAsync(
        int? tagId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize
    );
}
