using IIoT.WebApi.Core.Interfaces;
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
    /// <param name="sensorId">ID датчика</param>
    /// <param name="from">Начало периода (ISO 8601)</param>
    /// <param name="to">Конец периода (ISO 8601)</param>
    /// <returns>Массив массивов [[timestamp_ms, value], ...]</returns>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<object[]>>> GetHistory(
        [FromQuery] int sensorId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to
    )
    {
        if (from >= to)
        {
            return BadRequest("Дата 'from' должна быть меньше даты 'to'");
        }

        var data = await _repository.GetHistoryAsync(sensorId, from, to);
        return Ok(data);
    }
}
