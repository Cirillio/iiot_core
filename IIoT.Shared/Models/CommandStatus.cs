namespace IIoT.Shared.Models;

/// <summary>
/// Жизненный цикл команды управления (запись в исполнительный механизм).
/// Соответствует ENUM 'command_status' в PostgreSQL.
/// </summary>
public enum CommandStatus
{
    /// <summary>Команда создана API и ожидает обработки коллектором.</summary>
    Pending,

    /// <summary>Коллектор захватил сокет и выполняет Modbus-транзакцию.</summary>
    Processing,

    /// <summary>Запись успешно выполнена прибором и подтверждена.</summary>
    Success,

    /// <summary>Modbus Exception, таймаут или сетевой сбой.</summary>
    Failed,
}
