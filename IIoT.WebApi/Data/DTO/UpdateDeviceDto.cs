namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для обновления существующего устройства.
/// </summary>
/// <param name="Name">Новое имя устройства.</param>
/// <param name="IpAddress">Новый IPv4 адрес.</param>
/// <param name="Port">Новый порт Modbus TCP.</param>
/// <param name="SlaveId">Новый Modbus Unit ID.</param>
/// <param name="IsActive">Обновленное состояние активности.</param>
public record UpdateDeviceDto(string Name, string IpAddress, int Port, int SlaveId, bool IsActive);
