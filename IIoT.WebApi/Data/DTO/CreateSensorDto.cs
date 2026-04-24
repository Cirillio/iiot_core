namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для создания нового датчика.
/// </summary>
/// <param name="DeviceId">ID родительского устройства.</param>
/// <param name="PortNumber">Номер порта/регистра на контроллере.</param>
/// <param name="Name">Имя датчика (напр. "Давление в системе").</param>
/// <param name="Slug">Уникальный строковый код (напр. "sys_pressure").</param>
/// <param name="DataType">Тип: ANALOG, DIGITAL, VIRTUAL.</param>
/// <param name="Unit">Ед. изм. (напр. "Bar", "°C").</param>
/// <param name="UiConfig">JSON-строка с конфигурацией отображения.</param>
public record CreateSensorDto(
    int DeviceId,
    int PortNumber,
    string Name,
    string Slug,
    string DataType,
    string Unit,
    string UiConfig
);
