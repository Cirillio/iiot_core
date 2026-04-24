namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для регистрации нового устройства в системе.
/// </summary>
/// <param name="Name">Понятное имя устройства (напр. "ADAM-6017 Boiler Room").</param>
/// <param name="IpAddress">IPv4 адрес контроллера.</param>
/// <param name="Port">Порт Modbus TCP (по умолчанию 502).</param>
/// <param name="SlaveId">Modbus Unit ID (1-247).</param>
/// <param name="IsActive">Флаг активности опроса.</param>
public record CreateDeviceDto(string Name, string IpAddress, int Port, int SlaveId, bool IsActive);
