namespace IIoT.Shared.Models;

/// <summary>
/// Тип регистра Modbus — определяет Function Code при чтении.
/// </summary>
public enum ModbusRegisterType
{
    /// <summary>
    /// FC 0x04 — Аналоговые входы (Read-only).
    /// </summary>
    InputRegister,

    /// <summary>
    /// FC 0x03 — Универсальные регистры (Read/Write).
    /// </summary>
    HoldingRegister,

    /// <summary>
    /// FC 0x02 — Дискретные входы (Read-only).
    /// </summary>
    DiscreteInput,

    /// <summary>
    /// FC 0x01 — Дискретные выходы (Coils, Read/Write).
    /// </summary>
    Coil,
}
