using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IIoT.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController(IDeviceRepository repository) : ControllerBase
{
    // GET: api/devices
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetAll()
    {
        var devices = await repository.GetDevicesWithSensorsAsync();
        return Ok(devices);
    }

    // GET: api/devices/1
    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> GetById(int id)
    {
        var device = await repository.GetDeviceByIdWithSensorsAsync(id);

        if (device is null)
        {
            return NotFound($"Устройство с ID {id} не найдено");
        }

        return Ok(device);
    }
}
