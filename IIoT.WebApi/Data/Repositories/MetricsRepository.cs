using Dapper;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Data.TypeHandlers;

namespace IIoT.WebApi.Data.Repositories;

/// <summary>
/// Репозиторий для высокопроизводительной выборки временных рядов (метрик).
/// Оптимизирован для работы с гипертаблицами TimescaleDB и Continuous Aggregates.
/// </summary>
public class MetricsRepository(DapperContext context) : IMetricsRepository
{
    private readonly DapperContext _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// Использует автоматическое переключение на агрегаты 'metrics_hourly', если интервал запроса превышает 48 часов.
    /// Данные возвращаются в виде массива массивов [UnixTimestamp_ms, Value], что идеально подходит для ECharts.
    /// </remarks>
    /// <summary>Целевое число точек на графике — определяет ширину бакета под диапазон.</summary>
    private const double TargetPoints = 500;

    public async Task<IEnumerable<object[]>> GetHistoryAsync(
        int tagId,
        DateTime from,
        DateTime to
    )
    {
        var duration = to - from;
        bool useHourly = duration.TotalHours > 48;
        string tableName = useHourly ? "metrics_hourly" : "metrics";

        // Бакет = диапазон / TargetPoints, но не мельче источника:
        // raw — минимум 1с, агрегат metrics_hourly — минимум 1 час.
        double minBucketSec = useHourly ? 3600 : 1;
        double bucketSec = Math.Max(
            Math.Ceiling(duration.TotalSeconds / TargetPoints),
            minBucketSec
        );

        // time_bucket даёт равномерную сетку по времени — график перестаёт «плыть»
        // при неравномерной плотности сырья и не тянет десятки тысяч точек на 24ч.
        string sql =
            $@"
            SELECT time_bucket(make_interval(secs => @BucketSec), time) AS bucket,
                   AVG(value) AS value
            FROM {tableName}
            WHERE tag_id = @TagId
              AND time >= @From
              AND time <= @To
            GROUP BY bucket
            ORDER BY bucket ASC";

        using var connection = _context.CreateConnection();
        var result = await connection.QueryAsync<(DateTime bucket, double value)>(
            sql,
            new
            {
                TagId = tagId,
                From = from,
                To = to,
                BucketSec = bucketSec,
            }
        );

        return result.Select(x =>
            new object[] { new DateTimeOffset(x.bucket).ToUnixTimeMilliseconds(), x.value }
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// DISTINCT ON (tag_id) с сортировкой по времени убыв. — отдаёт одну (последнюю)
    /// строку на тег. Эффективно по индексу ix_metrics_tag_time (tag_id, time DESC).
    /// </remarks>
    public async Task<IEnumerable<LatestMetricDto>> GetLatestAsync()
    {
        const string sql =
            @"
            SELECT DISTINCT ON (tag_id)
                   tag_id    AS TagId,
                   time      AS Time,
                   raw_value AS RawValue,
                   value     AS Value
            FROM metrics
            ORDER BY tag_id, time DESC";

        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<LatestMetricDto>(sql);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Фильтры собираются динамически: NULL-параметры исключают соответствующее условие
    /// через `(@Param IS NULL OR column op @Param)` — один план запроса, без конкатенации SQL.
    /// COUNT и страница выбираются двумя запросами; на гипертаблице это допустимо для табличного UI.
    /// </remarks>
    public async Task<PagedResult<RawMetricDto>> GetRawAsync(
        int? tagId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize
    )
    {
        // Защита от некорректных значений с клиента: страница ≥ 1, размер в разумных пределах.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        const string filter =
            @"
            WHERE (@TagId IS NULL OR m.tag_id = @TagId)
              AND (@From  IS NULL OR m.time >= @From)
              AND (@To    IS NULL OR m.time <= @To)";

        var parameters = new
        {
            TagId = tagId,
            From = from,
            To = to,
            Limit = pageSize,
            Offset = (page - 1) * pageSize,
        };

        var countSql = $"SELECT COUNT(*) FROM metrics m {filter}";

        var pageSql =
            $@"
            SELECT m.tag_id    AS TagId,
                   t.name      AS TagName,
                   t.unit      AS Unit,
                   m.time      AS Time,
                   m.raw_value AS RawValue,
                   m.value     AS Value
            FROM metrics m
            LEFT JOIN tags t ON t.tag_id = m.tag_id
            {filter}
            ORDER BY m.time DESC
            LIMIT @Limit OFFSET @Offset";

        using var connection = _context.CreateConnection();
        var total = await connection.ExecuteScalarAsync<long>(countSql, parameters);
        var items = (await connection.QueryAsync<RawMetricDto>(pageSql, parameters)).ToList();

        return new PagedResult<RawMetricDto>(items, total, page, pageSize);
    }
}
