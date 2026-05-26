namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Данные для создания нового физического соединения Modbus.
/// </summary>
/// <param name="IpAddress">IPv4-адрес или hostname шлюза/устройства.</param>
/// <param name="Port">TCP-порт (обычно 502).</param>
/// <param name="Description">Описание (напр. "Шлюз цеха №2").</param>
public record CreateConnectionDto(string IpAddress, int Port, string? Description);
