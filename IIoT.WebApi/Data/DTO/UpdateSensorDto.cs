namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для обновления настроек датчика.
/// </summary>
public record UpdateSensorDto(
    int PortNumber,
    string Name,
    string Slug,
    string DataType,
    int RegisterAddress,
    string RegisterType,
    int RegisterCount,
    string Unit,
    double InputMin,
    double InputMax,
    double OutputMin,
    double OutputMax,
    double OffsetVal,
    string? Formula,
    string UiConfig
);
