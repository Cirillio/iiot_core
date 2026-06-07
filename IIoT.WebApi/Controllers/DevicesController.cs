using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace IIoT.WebApi.Controllers;

/// <summary>
/// Контроллер для управления физическими устройствами (контроллерами).
/// </summary>
[ApiController]
[Route("api/devices")]
public class DevicesController(
    IDeviceRepository repository,
    IHubContext<MonitoringHub, IMonitoringClient> hubContext
) : ControllerBase
{
    private readonly IDeviceRepository _repository = repository;
    private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;

    /// <summary>
    /// Получить данные всех устройств и их датчиков для главного экрана мониторинга.
    /// </summary>
    /// <param name="limit">Ограничение количества датчиков на одно устройство (сортировка по mainPagePosition).</param>
    /// <returns>Список устройств с вложенными датчиками.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DashboardDeviceDTO>>> GetDevices(
        [FromQuery] int? limit
    )
    {
        var devices = await _repository.GetDevicesWithTagsAsync(limit);
        return Ok(devices);
    }

    /// <summary>
    /// Получить подробную информацию об устройстве по ID.
    /// </summary>
    /// <param name="id">ID устройства.</param>
    /// <returns>Объект устройства.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> GetById(int id)
    {
        var device = await _repository.GetDeviceByIdWithTagsAsync(id);

        if (device is null)
        {
            return NotFound($"Устройство с ID {id} не найдено");
        }

        return Ok(device);
    }

    /// <summary>
    /// Добавить новое устройство в систему и оповестить SignalR клиентов.
    /// </summary>
    /// <param name="dto">Параметры нового устройства.</param>
    /// <returns>ID созданной записи.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateDeviceDto dto)
    {
        var device = new Device
        {
            Name = dto.Name,
            ConnectionId = dto.ConnectionId,
            SlaveId = dto.SlaveId,
            UseGroupPolling = dto.UseGroupPolling,
            MaxRegisterSpan = dto.MaxRegisterSpan,
            MaxBitSpan = dto.MaxBitSpan,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        var id = await _repository.AddAsync(device);

        await _hubContext.Clients.All.ConfigUpdated("DEVICE", id);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Обновить параметры существующего устройства.
    /// </summary>
    /// <param name="id">ID устройства.</param>
    /// <param name="dto">Обновленные данные.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateDeviceDto dto)
    {
        var existingDevice = await _repository.GetDeviceByIdWithTagsAsync(id);
        if (existingDevice is null)
        {
            return NotFound($"Устройство с ID {id} не найдено");
        }

        var updatedDevice = existingDevice with
        {
            Name = dto.Name,
            ConnectionId = dto.ConnectionId,
            SlaveId = dto.SlaveId,
            UseGroupPolling = dto.UseGroupPolling,
            MaxRegisterSpan = dto.MaxRegisterSpan,
            MaxBitSpan = dto.MaxBitSpan,
            IsActive = dto.IsActive,
        };

        await _repository.UpdateAsync(updatedDevice);

        await _hubContext.Clients.All.ConfigUpdated("DEVICE", id);

        return NoContent();
    }

    /// <summary>
    /// Удалить устройство из системы.
    /// </summary>
    /// <param name="id">ID устройства.</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existingDevice = await _repository.GetDeviceByIdWithTagsAsync(id);
        if (existingDevice is null)
        {
            return NotFound($"Устройство с ID {id} не найдено");
        }

        try
        {
            await _repository.DeleteAsync(id);
            await _hubContext.Clients.All.ConfigUpdated("DEVICE", id);
        }
        catch (Exception ex)
        {
            return BadRequest($"Невозможно удалить устройство: {ex.Message}");
        }

        return NoContent();
    }
}
