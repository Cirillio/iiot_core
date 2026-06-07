namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для обновления существующего устройства.
/// </summary>
/// <param name="Name">Новое имя устройства.</param>
/// <param name="ConnectionId">ID физического соединения (modbus_connections).</param>
/// <param name="SlaveId">Новый Modbus Unit ID.</param>
/// <param name="UseGroupPolling">Групповой опрос смежных регистров.</param>
/// <param name="MaxRegisterSpan">Макс. ширина чанка при групповом опросе регистров.</param>
/// <param name="MaxBitSpan">Макс. ширина чанка при групповом опросе битовых таблиц (Coil/DiscreteInput).</param>
/// <param name="IsActive">Обновлённое состояние активности.</param>
public record UpdateDeviceDto(
    string Name,
    int ConnectionId,
    int SlaveId,
    bool UseGroupPolling,
    int MaxRegisterSpan,
    bool IsActive,
    int MaxBitSpan = 2000
);
