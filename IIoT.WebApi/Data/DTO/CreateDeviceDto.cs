namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для регистрации нового устройства в системе.
/// Сокет (ip:port) задаётся через ConnectionId (modbus_connections).
/// </summary>
/// <param name="Name">Понятное имя устройства (напр. "ADAM-6017 Boiler Room").</param>
/// <param name="ConnectionId">ID физического соединения (modbus_connections).</param>
/// <param name="SlaveId">Modbus Unit ID (1-247).</param>
/// <param name="UseGroupPolling">Групповой опрос смежных регистров.</param>
/// <param name="MaxRegisterSpan">Макс. ширина чанка при групповом опросе регистров.</param>
/// <param name="MaxBitSpan">Макс. ширина чанка при групповом опросе битовых таблиц (Coil/DiscreteInput).</param>
/// <param name="IsActive">Флаг активности опроса.</param>
public record CreateDeviceDto(
    string Name,
    int ConnectionId,
    int SlaveId,
    bool UseGroupPolling,
    int MaxRegisterSpan,
    bool IsActive,
    int MaxBitSpan = 2000
);
