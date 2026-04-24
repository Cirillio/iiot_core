using System.Text.Json;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using IIoT.WebApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace IIoT.WebApi.Controllers;

/// <summary>
/// Контроллер для управления датчиками (настройки, калибровка, привязка к портам).
/// </summary>
[ApiController]
[Route("api/sensors")]
public class SensorsController(
    ISensorRepository repository,
    IHubContext<MonitoringHub, IMonitoringClient> hubContext
) : ControllerBase
{
    private readonly ISensorRepository _repository = repository;
    private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;

    /// <summary>
    /// Получить список всех датчиков системы.
    /// </summary>
    /// <returns>Список настроек датчиков.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SensorSettings>>> GetAll()
    {
        var sensors = await _repository.GetAllAsync();
        return Ok(sensors);
    }

    /// <summary>
    /// Получить настройки датчика по его ID.
    /// </summary>
    /// <param name="id">Уникальный ID датчика.</param>
    /// <returns>Настройки датчика.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<SensorSettings>> GetById(int id)
    {
        var sensor = await _repository.GetByIdAsync(id);
        if (sensor == null)
            return NotFound($"Датчик с ID {id} не найден");

        return Ok(sensor);
    }

    /// <summary>
    /// Получить все датчики конкретного устройства.
    /// </summary>
    /// <param name="deviceId">ID устройства.</param>
    /// <returns>Список датчиков устройства.</returns>
    [HttpGet("device/{deviceId}")]
    public async Task<ActionResult<IEnumerable<SensorSettings>>> GetByDeviceId(int deviceId)
    {
        var sensors = await _repository.GetByDeviceIdAsync(deviceId);
        return Ok(sensors);
    }

    /// <summary>
    /// Создать новый датчик и оповестить систему через SignalR.
    /// </summary>
    /// <param name="dto">Данные нового датчика.</param>
    /// <returns>ID созданного датчика.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateSensorDto dto)
    {
        if (!Enum.TryParse<SensorDataType>(dto.DataType, true, out var dataType))
        {
            return BadRequest($"Недопустимый тип данных: {dto.DataType}");
        }

        var uiConfig = string.IsNullOrEmpty(dto.UiConfig)
            ? new SensorUiConfig()
            : JsonSerializer.Deserialize<SensorUiConfig>(dto.UiConfig) ?? new SensorUiConfig();

        var sensor = new SensorSettings
        {
            DeviceId = dto.DeviceId,
            PortNumber = dto.PortNumber,
            Name = dto.Name,
            Slug = dto.Slug,
            DataType = dataType,
            Unit = dto.Unit,
            UiConfigJson = uiConfig,
            UpdatedAt = DateTime.UtcNow,
        };

        var id = await _repository.AddAsync(sensor);

        // Оповещение об изменении конфигурации
        await _hubContext.Clients.All.ConfigUpdated("SENSOR", id);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Обновить настройки датчика и оповестить систему.
    /// </summary>
    /// <param name="id">ID датчика.</param>
    /// <param name="dto">Обновленные данные.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateSensorDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound($"Датчик с ID {id} не найден");

        if (!Enum.TryParse<SensorDataType>(dto.DataType, true, out var dataType))
        {
            return BadRequest($"Недопустимый тип данных: {dto.DataType}");
        }

        var uiConfig = string.IsNullOrEmpty(dto.UiConfig)
            ? new SensorUiConfig()
            : JsonSerializer.Deserialize<SensorUiConfig>(dto.UiConfig) ?? new SensorUiConfig();

        var updated = existing with
        {
            PortNumber = dto.PortNumber,
            Name = dto.Name,
            Slug = dto.Slug,
            DataType = dataType,
            Unit = dto.Unit,
            UiConfigJson = uiConfig,
            UpdatedAt = DateTime.UtcNow,
        };

        await _repository.UpdateAsync(updated);

        // Оповещение об изменении конфигурации
        await _hubContext.Clients.All.ConfigUpdated("SENSOR", id);

        return NoContent();
    }

    /// <summary>
    /// Удалить датчик и оповестить систему.
    /// </summary>
    /// <param name="id">ID датчика.</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound($"Датчик с ID {id} не найден");

        await _repository.DeleteAsync(id);

        // Оповещение об изменении конфигурации
        await _hubContext.Clients.All.ConfigUpdated("SENSOR", id);

        return NoContent();
    }
}
