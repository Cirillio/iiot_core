using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IIoT.WebApi.Controllers;

[ApiController]
[Route("api/metrics")]
public class MetricsController(IMetricsRepository repository) : ControllerBase
{
    private readonly IMetricsRepository _repository = repository;

    /// <summary>
    /// Возвращает историю показаний датчика для графиков.
    /// Автоматически выбирает между сырыми данными и часовыми агрегатами.
    /// </summary>
    /// <param name="tagId">ID датчика</param>
    /// <param name="from">Начало периода (ISO 8601)</param>
    /// <param name="to">Конец периода (ISO 8601)</param>
    /// <returns>Массив массивов [[timestamp_ms, value], ...]</returns>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<object[]>>> GetHistory(
        [FromQuery] int tagId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to
    )
    {
        if (from >= to)
        {
            return BadRequest("Дата 'from' должна быть меньше даты 'to'");
        }

        var data = await _repository.GetHistoryAsync(tagId, from, to);
        return Ok(data);
    }

    /// <summary>
    /// Последнее показание по каждому тегу — снимок для инициализации UI
    /// (карточки, АРМ управления) до прихода live-метрик по SignalR.
    /// </summary>
    [HttpGet("latest")]
    public async Task<ActionResult> GetLatest()
    {
        var data = await _repository.GetLatestAsync();
        return Ok(data);
    }

    /// <summary>
    /// Постраничная выборка сырых метрик для табличного просмотра (новые сверху).
    /// </summary>
    /// <param name="tagId">Фильтр по тегу; не задан — все теги.</param>
    /// <param name="from">Начало периода (ISO 8601); не задано — без нижней границы.</param>
    /// <param name="to">Конец периода (ISO 8601); не задано — без верхней границы.</param>
    /// <param name="page">Номер страницы (1-based, по умолчанию 1).</param>
    /// <param name="pageSize">Размер страницы (1..500, по умолчанию 50).</param>
    /// <returns>Страница строк метрик с общим числом записей под фильтр.</returns>
    [HttpGet("raw")]
    public async Task<ActionResult<PagedResult<RawMetricDto>>> GetRaw(
        [FromQuery] int? tagId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50
    )
    {
        if (from.HasValue && to.HasValue && from >= to)
        {
            return BadRequest("Дата 'from' должна быть меньше даты 'to'");
        }

        var data = await _repository.GetRawAsync(tagId, from, to, page, pageSize);
        return Ok(data);
    }
}
