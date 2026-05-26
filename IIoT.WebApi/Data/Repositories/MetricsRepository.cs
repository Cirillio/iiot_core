using Dapper;
using IIoT.WebApi.Core.Interfaces;
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
    public async Task<IEnumerable<object[]>> GetHistoryAsync(
        int tagId,
        DateTime from,
        DateTime to
    )
    {
        var duration = to - from;
        string tableName = duration.TotalHours > 48 ? "metrics_hourly" : "metrics";

        string sql =
            $@"
            SELECT time, value
            FROM {tableName}
            WHERE tag_id = @TagId
              AND time >= @From
              AND time <= @To
            ORDER BY time ASC";

        using var connection = _context.CreateConnection();
        var result = await connection.QueryAsync<(DateTime time, double value)>(
            sql,
            new
            {
                TagId = tagId,
                From = from,
                To = to,
            }
        );

        return result.Select(x =>
            new object[] { new DateTimeOffset(x.time).ToUnixTimeMilliseconds(), x.value }
        );
    }
}
