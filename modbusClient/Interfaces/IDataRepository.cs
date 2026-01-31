using ModbusClient.Models;

namespace ModbusClient.Interfaces;

public interface IDataRepository
{
    /// <summary>
    /// Сохраняет измерения в базу (TimescaleDB metrics).
    /// </summary>
    Task SaveMetricsAsync(IEnumerable<Metric> metrics);

    /// <summary>
    /// Обновляет статус сервиса (Heartbeat).
    /// </summary>
    Task UpdateSystemStatusAsync(SystemStatus status);

    /// <summary>
    /// Получает список активных контроллеров для опроса.
    /// </summary>
    Task<IEnumerable<Device>> GetActiveDevicesAsync();

    /// <summary>
    /// Получает настройки всех датчиков для маппинга (Device+Port -> SensorID).
    /// </summary>
    Task<IEnumerable<SensorSettings>> GetSensorSettingsAsync();

    /// <summary>
    /// Получает глобальные настройки системы (интервалы, retention).
    /// </summary>
    Task<SystemConfig> GetSystemConfigAsync();
}
