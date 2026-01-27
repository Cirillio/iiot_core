using ModbusClient.Models;

namespace ModbusClient.Interfaces;

/// <summary>
/// Interface for ADAM devices usage
/// </summary>
public interface IModbusService
{
    // Проверка связи и инициализация регистров (режим 0x0008)
    Task<bool> InitializeAsync(string ipAddress, int port, CancellationToken ct);

    // Чтение всех аналоговых портов (возвращает список RAW значений)
    Task<IEnumerable<RawMeasurement>> ReadAllAnalogAsync(CancellationToken ct);

    // Чтение цифровых входов
    Task<IEnumerable<DigitalMeasurement>> ReadAllDigitalAsync(CancellationToken ct);

    // Безопасное закрытие соединения
    Task DisconnectAsync();
}
