using ModbusClient.Models;

namespace ModbusClient.Interfaces;

/// <summary>
/// Интерфейс основного репозитория данных (PostgreSQL/TimescaleDB).
/// Отвечает за сохранение временных рядов (метрик), получение конфигураций и обновление статусов.
/// </summary>
public interface IDataRepository
{
    /// <summary>
    /// Сохраняет пакет измерений (метрик) в гипертаблицу 'metrics'.
    /// </summary>
    /// <param name="metrics">Коллекция метрик для вставки.</param>
    Task SaveMetricsAsync(IEnumerable<Metric> metrics);

    /// <summary>
    /// Обновляет информацию о текущем состоянии сервиса (Heartbeat) в таблице системных статусов.
    /// </summary>
    /// <param name="status">Объект статуса системы.</param>
    Task UpdateSystemStatusAsync(SystemStatus status);

    /// <summary>
    /// Получает список всех активных устройств (контроллеров), которые необходимо опрашивать.
    /// Учитывает флаг IsActive = true.
    /// </summary>
    /// <returns>Коллекция устройств.</returns>
    Task<IEnumerable<Device>> GetActiveDevicesAsync();

    /// <summary>
    /// Получает полные настройки всех датчиков.
    /// Используется для маппинга данных (Device + Port -> SensorID) и калибровки значений.
    /// </summary>
    /// <returns>Коллекция настроек датчиков.</returns>
    Task<IEnumerable<SensorSettings>> GetSensorSettingsAsync();

    /// <summary>
    /// Получает глобальную конфигурацию системы (интервалы опроса, политики хранения данных и т.д.).
    /// </summary>
    /// <returns>Объект конфигурации системы.</returns>
    Task<SystemConfig> GetSystemConfigAsync();
}
