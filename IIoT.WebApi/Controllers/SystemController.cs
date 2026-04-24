using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IIoT.WebApi.Controllers;

/// <summary>
/// Контроллер для работы с системными настройками и мониторинга состояния сервисов.
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController(ISystemRepository repository) : ControllerBase
{
    private readonly ISystemRepository _repository = repository;

    /// <summary>
    /// Получить текущие глобальные настройки системы (Retention, Polling, и т.д.).
    /// </summary>
    /// <returns>Глобальная конфигурация системы.</returns>
    [HttpGet("config")]
    public async Task<ActionResult<SystemConfig>> GetConfig()
    {
        var config = await _repository.GetConfigAsync();
        return Ok(config);
    }

    /// <summary>
    /// Обновить глобальные системные настройки.
    /// </summary>
    /// <param name="config">Новый объект конфигурации.</param>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig(SystemConfig config)
    {
        await _repository.UpdateConfigAsync(config);
        return NoContent();
    }

    /// <summary>
    /// Получить текущий статус здоровья и пульс всех сервисов (Collector, Gateway).
    /// </summary>
    /// <returns>Список статусов сервисов из таблицы system_status.</returns>
    [HttpGet("status")]
    public async Task<ActionResult<IEnumerable<SystemStatus>>> GetStatus()
    {
        var status = await _repository.GetStatusAsync();
        return Ok(status);
    }
}
