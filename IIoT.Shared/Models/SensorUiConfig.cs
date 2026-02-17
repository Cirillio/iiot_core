namespace IIoT.Shared.Models;

public record SensorUiConfig
{
    public string? Color { get; init; }
    public string? Icon { get; init; }

    public int MainPagePosition { get; init; }
    public int GraphPosition { get; init; }
    public int TablePosition { get; init; }
    public int AlarmPosition { get; init; }
    public int HistoryPosition { get; init; }

    public double? MinCritical { get; init; } // порог "Критически низко".
    public double? MinWarning { get; init; } // порог "Предупреждение (низкое)".
    public double? MaxWarning { get; init; } // порог "Предупреждение (высокое)".
    public double? MaxCritical { get; init; } // порог "Критически высоко".
}
