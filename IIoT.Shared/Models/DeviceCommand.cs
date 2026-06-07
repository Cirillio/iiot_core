namespace IIoT.Shared.Models;

/// <summary>
/// Команда управления — запись значения в Coil или Holding Register прибора.
/// Соответствует таблице 'device_commands'. Служит единицей аудита и надёжной доставки.
/// </summary>
public record DeviceCommand
{
    /// <summary>Уникальный идентификатор транзакции (Primary Key). Клиент отслеживает по нему статус.</summary>
    public Guid Id { get; init; }

    /// <summary>Целевой тег. Из него резолвятся адрес, тип регистра и устройство.</summary>
    public int TagId { get; init; }

    /// <summary>Записываемое значение (для Coil: 1.0 = ON, 0.0 = OFF).</summary>
    public double Value { get; init; }

    /// <summary>Идентификатор оператора, инициировавшего запись (для аудита).</summary>
    public string OperatorId { get; init; } = string.Empty;

    /// <summary>Текущий статус выполнения команды.</summary>
    public CommandStatus Status { get; init; } = CommandStatus.Pending;

    /// <summary>Описание ошибки при статусе Failed. Null в остальных случаях.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Время создания команды (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Время последнего изменения статуса (UTC).</summary>
    public DateTime UpdatedAt { get; init; }
}
