namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Команда записи значения в исполнительный механизм (Supervisory Control).
/// </summary>
/// <param name="TagId">Целевой тег. Должен быть типа Coil или Holding Register.</param>
/// <param name="Value">Записываемое значение (для Coil: 1.0 = ON, 0.0 = OFF).</param>
/// <param name="OperatorId">Идентификатор оператора для аудита.</param>
public record WriteCommandDto(int TagId, double Value, string OperatorId);
