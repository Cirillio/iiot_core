using IIoT.Shared.Models;

namespace IIoT.Collector.Interfaces;

/// <summary>
/// Репозиторий команд управления со стороны коллектора.
/// Отвечает за выборку команды и перевод её по жизненному циклу статусов.
/// </summary>
public interface ICommandRepository
{
    /// <summary>
    /// Возвращает команду по идентификатору транзакции или null.
    /// </summary>
    Task<DeviceCommand?> GetByIdAsync(Guid id);

    /// <summary>
    /// Переводит команду в новый статус. UPDATE status триггерит pg_notify в WebApi.
    /// </summary>
    /// <param name="errorMessage">Текст ошибки при статусе Failed; null в остальных случаях.</param>
    Task UpdateStatusAsync(Guid id, CommandStatus status, string? errorMessage = null);
}
