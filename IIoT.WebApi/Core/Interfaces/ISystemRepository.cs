using IIoT.Shared.Models;

namespace IIoT.WebApi.Core.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с глобальными настройками и статусами системы.
/// </summary>
public interface ISystemRepository
{
    /// <summary>
    /// Получить текущую глобальную конфигурацию всей системы мониторинга.
    /// </summary>
    /// <returns>Объект SystemConfig.</returns>
    Task<SystemConfig> GetConfigAsync();

    /// <summary>
    /// Обновить глобальную конфигурацию системы.
    /// </summary>
    /// <param name="config">Объект конфигурации с новыми параметрами.</param>
    Task UpdateConfigAsync(SystemConfig config);

    /// <summary>
    /// Получить статусы активности всех сервисов системы.
    /// </summary>
    /// <returns>Список статусов сервисов.</returns>
    Task<IEnumerable<SystemStatus>> GetStatusAsync();

    /// <summary>
    /// Обновить статус конкретного сервиса (Health Check).
    /// </summary>
    /// <param name="status">Объект со статусом сервиса.</param>
    Task UpdateSystemStatusAsync(SystemStatus status);
}
