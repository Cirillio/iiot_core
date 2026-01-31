namespace ModbusClient.Models;

/// <summary>
/// Полная конфигурация датчика для калибровки, расчетов и UI.
/// </summary>
public record SensorSettings
{
    // Идентификаторы
    public int SensorId { get; init; } // sensor_id (PK)
    public int? DeviceId { get; init; } // device_id (FK)
    public int? PortNumber { get; init; } // port_number (Nullable, т.к. может отсутствовать у VIRTUAL)

    // Описательные данные
    public string Name { get; init; } = string.Empty; // name
    public string? Slug { get; init; } // slug (для формул и API)
    public SensorDataType DataType { get; init; } = SensorDataType.ANALOG; // data_type (Enum)
    public string? Unit { get; init; } // unit (напр. "°C", "Bar")

    // Границы сигнала (Линейная интерполяция)
    public double InputMin { get; init; } // input_min (обычно 0)
    public double InputMax { get; init; } // input_max (обычно 65535)
    public double OutputMin { get; init; } // output_min
    public double OutputMax { get; init; } // output_max

    // Математика и калибровка
    public double OffsetVal { get; init; } // offset_val (коррекция нуля)
    public string? Formula { get; init; } // formula (для VIRTUAL датчиков)

    // Дополнительные метаданные
    public string UiConfigJson { get; init; } = "{}"; // ui_config (храним как строку или JObject)
    public DateTime UpdatedAt { get; init; } // updated_at
}
