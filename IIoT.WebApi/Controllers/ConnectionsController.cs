using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace IIoT.WebApi.Controllers;

/// <summary>
/// Контроллер управления физическими соединениями Modbus (сокеты ip:port).
/// Несколько устройств могут делить одно соединение (RS-485→TCP шлюз).
/// </summary>
[ApiController]
[Route("api/connections")]
public class ConnectionsController(
    IConnectionRepository repository,
    IHubContext<MonitoringHub, IMonitoringClient> hubContext
) : ControllerBase
{
    private readonly IConnectionRepository _repository = repository;
    private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;

    /// <summary>Получить все соединения.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModbusConnection>>> GetAll()
    {
        var connections = await _repository.GetAllAsync();
        return Ok(connections);
    }

    /// <summary>Получить соединение по ID.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ModbusConnection>> GetById(int id)
    {
        var connection = await _repository.GetByIdAsync(id);
        if (connection is null)
            return NotFound($"Соединение с ID {id} не найдено");

        return Ok(connection);
    }

    /// <summary>Создать новое соединение.</summary>
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateConnectionDto dto)
    {
        var connection = new ModbusConnection
        {
            IpAddress = dto.IpAddress,
            Port = dto.Port,
            Description = dto.Description,
        };

        var id = await _repository.AddAsync(connection);
        await _hubContext.Clients.All.ConfigUpdated("CONNECTION", id);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Обновить соединение.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateConnectionDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null)
            return NotFound($"Соединение с ID {id} не найдено");

        var updated = existing with
        {
            IpAddress = dto.IpAddress,
            Port = dto.Port,
            Description = dto.Description,
        };

        await _repository.UpdateAsync(updated);
        await _hubContext.Clients.All.ConfigUpdated("CONNECTION", id);

        return NoContent();
    }

    /// <summary>Удалить соединение. Запрещено, если к нему привязаны устройства.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null)
            return NotFound($"Соединение с ID {id} не найдено");

        try
        {
            await _repository.DeleteAsync(id);
            await _hubContext.Clients.All.ConfigUpdated("CONNECTION", id);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest("Невозможно удалить соединение: к нему привязаны устройства");
        }

        return NoContent();
    }
}
