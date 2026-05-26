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
[Route("api/tags")]
public class TagsController(
    ITagRepository repository,
    IHubContext<MonitoringHub, IMonitoringClient> hubContext
) : ControllerBase
{
    private readonly ITagRepository _repository = repository;
    private readonly IHubContext<MonitoringHub, IMonitoringClient> _hubContext = hubContext;

    /// <summary>
    /// Получить список всех датчиков системы.
    /// </summary>
    /// <returns>Список настроек датчиков.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TagSettings>>> GetAll()
    {
        var tags = await _repository.GetAllAsync();
        return Ok(tags);
    }

    /// <summary>
    /// Получить настройки датчика по его ID.
    /// </summary>
    /// <param name="id">Уникальный ID датчика.</param>
    /// <returns>Настройки датчика.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TagSettings>> GetById(int id)
    {
        var tag = await _repository.GetByIdAsync(id);
        if (tag == null)
            return NotFound($"Датчик с ID {id} не найден");

        return Ok(tag);
    }

    /// <summary>
    /// Получить все датчики конкретного устройства.
    /// </summary>
    /// <param name="deviceId">ID устройства.</param>
    /// <returns>Список датчиков устройства.</returns>
    [HttpGet("device/{deviceId}")]
    public async Task<ActionResult<IEnumerable<TagSettings>>> GetByDeviceId(int deviceId)
    {
        var tags = await _repository.GetByDeviceIdAsync(deviceId);
        return Ok(tags);
    }

    /// <summary>
    /// Создать новый датчик и оповестить систему через SignalR.
    /// </summary>
    /// <param name="dto">Данные нового датчика.</param>
    /// <returns>ID созданного датчика.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateTagDto dto)
    {
        if (!Enum.TryParse<TagDataType>(dto.DataType?.Replace("_", ""), true, out var dataType))
        {
            return BadRequest($"Недопустимый тип данных: {dto.DataType}");
        }

        if (!Enum.TryParse<ModbusRegisterType>(dto.RegisterType, true, out var regType))
        {
            return BadRequest($"Недопустимый тип регистра: {dto.RegisterType}");
        }

        if (!TryParseEndianness(dto.Endianness, out var endianness))
        {
            return BadRequest($"Недопустимый порядок байт: {dto.Endianness}");
        }

        var uiConfig = string.IsNullOrEmpty(dto.UiConfig)
            ? new TagUiConfig()
            : JsonSerializer.Deserialize<TagUiConfig>(dto.UiConfig) ?? new TagUiConfig();

        var tag = new TagSettings
        {
            DeviceId = dto.DeviceId,
            PortNumber = dto.PortNumber,
            Name = dto.Name,
            Slug = dto.Slug,
            DataType = dataType,
            RegisterAddress = dto.RegisterAddress,
            RegisterType = regType,
            RegisterCount = dto.RegisterCount,
            Endianness = endianness,
            Unit = dto.Unit,
            InputMin = dto.InputMin,
            InputMax = dto.InputMax,
            OutputMin = dto.OutputMin,
            OutputMax = dto.OutputMax,
            OffsetVal = dto.OffsetVal,
            DeadbandThreshold = dto.DeadbandThreshold,
            Formula = dto.Formula,
            UiConfigJson = uiConfig,
            UpdatedAt = DateTime.UtcNow,
        };

        var id = await _repository.AddAsync(tag);

        // Оповещение об изменении конфигурации
        await _hubContext.Clients.All.ConfigUpdated("TAG", id);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Обновить настройки датчика и оповестить систему.
    /// </summary>
    /// <param name="id">ID датчика.</param>
    /// <param name="dto">Обновленные данные.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateTagDto dto)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound($"Датчик с ID {id} не найден");

        if (!Enum.TryParse<TagDataType>(dto.DataType?.Replace("_", ""), true, out var dataType))
        {
            return BadRequest($"Недопустимый тип данных: {dto.DataType}");
        }

        if (!Enum.TryParse<ModbusRegisterType>(dto.RegisterType, true, out var regType))
        {
            return BadRequest($"Недопустимый тип регистра: {dto.RegisterType}");
        }

        if (!TryParseEndianness(dto.Endianness, out var endianness))
        {
            return BadRequest($"Недопустимый порядок байт: {dto.Endianness}");
        }

        var uiConfig = string.IsNullOrEmpty(dto.UiConfig)
            ? new TagUiConfig()
            : JsonSerializer.Deserialize<TagUiConfig>(dto.UiConfig) ?? new TagUiConfig();

        var updated = existing with
        {
            PortNumber = dto.PortNumber,
            Name = dto.Name,
            Slug = dto.Slug,
            DataType = dataType,
            RegisterAddress = dto.RegisterAddress,
            RegisterType = regType,
            RegisterCount = dto.RegisterCount,
            Endianness = endianness,
            Unit = dto.Unit,
            InputMin = dto.InputMin,
            InputMax = dto.InputMax,
            OutputMin = dto.OutputMin,
            OutputMax = dto.OutputMax,
            OffsetVal = dto.OffsetVal,
            DeadbandThreshold = dto.DeadbandThreshold,
            Formula = dto.Formula,
            UiConfigJson = uiConfig,
            UpdatedAt = DateTime.UtcNow,
        };

        await _repository.UpdateAsync(updated);

        // Оповещение об изменении конфигурации
        await _hubContext.Clients.All.ConfigUpdated("TAG", id);

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
        await _hubContext.Clients.All.ConfigUpdated("TAG", id);

        return NoContent();
    }

    /// <summary>
    /// Парсит порядок байт из строки. Пустое значение → BigEndian (дефолт).
    /// Принимает как "WORD_SWAP", так и "WordSwap" (подчёркивания игнорируются).
    /// </summary>
    private static bool TryParseEndianness(string? raw, out ModbusEndianness endianness)
    {
        if (string.IsNullOrEmpty(raw))
        {
            endianness = ModbusEndianness.BigEndian;
            return true;
        }

        return Enum.TryParse(raw.Replace("_", ""), true, out endianness);
    }
}
