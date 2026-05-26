namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для создания нового датчика.
/// </summary>
public record CreateTagDto(
    int DeviceId,
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
    string UiConfig,
    string? Endianness = null
);
