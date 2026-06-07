namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Строка сырой метрики для табличного просмотра. Включает имя тега (JOIN с tags),
/// чтобы клиент не делал отдельную загрузку справочника тегов ради подписи строки.
/// </summary>
/// <param name="TagId">ID тега.</param>
/// <param name="TagName">Человекочитаемое имя тега (может быть пустым, если тег удалён).</param>
/// <param name="Unit">Единица измерения тега.</param>
/// <param name="Time">Время измерения (UTC).</param>
/// <param name="RawValue">Сырое значение с устройства до калибровки.</param>
/// <param name="Value">Откалиброванное (физическое) значение.</param>
public record RawMetricDto(
    int TagId,
    string? TagName,
    string? Unit,
    DateTime Time,
    double? RawValue,
    double Value
);
