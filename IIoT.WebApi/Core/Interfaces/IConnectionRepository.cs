using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Репозиторий управления физическими соединениями Modbus (modbus_connections).
/// </summary>
public interface IConnectionRepository
{
    /// <summary>Получить все соединения.</summary>
    Task<IEnumerable<ModbusConnection>> GetAllAsync();

    /// <summary>Получить соединение по ID.</summary>
    Task<ModbusConnection?> GetByIdAsync(int id);

    /// <summary>Добавить соединение. Возвращает ID созданной записи.</summary>
    Task<int> AddAsync(ModbusConnection connection);

    /// <summary>Обновить соединение.</summary>
    Task UpdateAsync(ModbusConnection connection);

    /// <summary>Удалить соединение. Бросает при наличии привязанных устройств (FK RESTRICT).</summary>
    Task DeleteAsync(int id);
}
