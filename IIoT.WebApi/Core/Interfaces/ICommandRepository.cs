using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Репозиторий команд управления (таблица 'device_commands').
/// Со стороны WebApi отвечает за регистрацию новых команд и их выборку для отслеживания.
/// </summary>
public interface ICommandRepository
{
    /// <summary>
    /// Регистрирует новую команду со статусом Pending. INSERT триггерит pg_notify коллектору.
    /// </summary>
    /// <returns>Созданная команда с заполненными Id, CreatedAt, UpdatedAt.</returns>
    Task<DeviceCommand> CreateAsync(int tagId, double value, string operatorId);

    /// <summary>
    /// Возвращает команду по идентификатору транзакции или null.
    /// </summary>
    Task<DeviceCommand?> GetByIdAsync(Guid id);
}
