using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.DTO;
using Microsoft.AspNetCore.Mvc;

namespace IIoT.WebApi.Controllers;

/// <summary>
/// Контроллер двунаправленного управления (Supervisory Control).
/// Принимает команды записи в Coil/Holding Register от АРМ Администратора.
/// </summary>
[ApiController]
[Route("api/v1/control")]
public class ControlController(
    ITagRepository tagRepository,
    ICommandRepository commandRepository,
    ISystemRepository systemRepository
) : ControllerBase
{
    private const string CollectorServiceName = "ModbusCollector";

    /// <summary>
    /// Регистрирует асинхронную команду записи значения в исполнительный механизм.
    /// Запись разрешена только для Coil (0X) и Holding Register (4X).
    /// </summary>
    /// <returns>202 Accepted с объектом команды; клиент отслеживает статус по Id.</returns>
    [HttpPost("write")]
    [ProducesResponseType(typeof(DeviceCommand), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Write(WriteCommandDto dto)
    {
        // 1. Резолв конфигурации тега
        var tag = await tagRepository.GetByIdAsync(dto.TagId);
        if (tag == null)
            return NotFound(new { error = $"Тег с ID {dto.TagId} не найден." });

        // 2. Верификация прав записи — запрет для Read-Only таблиц
        if (
            tag.RegisterType
            is ModbusRegisterType.DiscreteInput
                or ModbusRegisterType.InputRegister
        )
        {
            return BadRequest(
                new
                {
                    error = "Write operation is prohibited for Read-Only register types (Discrete Inputs / Input Registers).",
                }
            );
        }

        // 3. Резолв статуса соединения прибора.
        // Per-connection статус в схеме не ведётся; используем heartbeat сервиса-коллектора
        // как индикатор доступности шины. Если коллектор не Online — запись недоставима.
        var statuses = await systemRepository.GetStatusAsync();
        var collector = statuses.FirstOrDefault(s => s.ServiceName == CollectorServiceName);
        if (collector is null || collector.Status is ServiceStatus.Offline or ServiceStatus.CriticalError)
        {
            return StatusCode(
                503,
                new { error = "Сервис коллектора недоступен (Offline). Команда не может быть доставлена." }
            );
        }

        // 4. Регистрация транзакции (Pending). INSERT триггерит pg_notify коллектору (шаг 5 — межсервисный пинг).
        var command = await commandRepository.CreateAsync(dto.TagId, dto.Value, dto.OperatorId);

        // 6. Ответ клиенту: 202 Accepted + объект команды (клиент отслеживает по Id через ControlHub).
        return Accepted(command);
    }
}
