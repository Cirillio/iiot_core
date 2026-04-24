namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для обновления настроек датчика.
/// </summary>
/// <param name="PortNumber">Номер порта/регистра.</param>
/// <param name="Name">Новое имя датчика.</param>
/// <param name="Slug">Новый slug.</param>
/// <param name="DataType">Новый тип данных.</param>
/// <param name="Unit">Новая единица измерения.</param>
/// <param name="UiConfig">Новая JSON-конфигурация UI.</param>
public record UpdateSensorDto(
    int PortNumber,
    string Name,
    string Slug,
    string DataType,
    string Unit,
    string UiConfig
);
