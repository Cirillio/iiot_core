namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для обновления физического соединения Modbus.
/// </summary>
/// <param name="IpAddress">Новый IPv4-адрес или hostname.</param>
/// <param name="Port">Новый TCP-порт.</param>
/// <param name="Description">Новое описание.</param>
public record UpdateConnectionDto(string IpAddress, int Port, string? Description);
