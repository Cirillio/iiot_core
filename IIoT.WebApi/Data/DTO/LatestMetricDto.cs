namespace IIoT.WebApi.Data.DTO;

/// <summary>
/// Последнее известное показание тега — снимок для инициализации UI до прихода live-метрик.
/// Форма совпадает с payload SignalR ReceiveMetrics (time, tagId, rawValue, value).
/// </summary>
public record LatestMetricDto(int TagId, DateTime Time, double? RawValue, double Value);
