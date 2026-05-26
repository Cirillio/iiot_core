namespace IIoT.Shared.Models;

/// <summary>
/// Расширения для работы с тегами.
/// Содержит логику преобразования "сырых" значений в физические величины.
/// </summary>
public static class TagExtensions
{
    /// <summary>
    /// Вычисляет физическое значение тега на основе "сырых" данных и конфигурации.
    /// </summary>
    /// <param name="rawValue">Сырое значение, полученное с Modbus-устройства (обычно 0-65535).</param>
    /// <param name="settings">Настройки тега (Min/Max, Offset, тип данных).</param>
    /// <returns>Откалиброванное значение (double).</returns>
    /// <remarks>
    /// DIGITAL → 1.0 если rawValue > 0, иначе 0.0.
    /// VIRTUAL → возвращает rawValue без изменений (парсинг формул — TODO).
    /// ANALOG  → линейная интерполяция: ((Raw-InMin)/(InMax-InMin))*(OutMax-OutMin)+OutMin+Offset,
    /// с защитой от деления на ноль.
    /// </remarks>
    public static double Calculate(double rawValue, TagSettings settings)
    {
        if (settings.DataType == TagDataType.Digital)
        {
            return rawValue > 0 ? 1.0 : 0.0;
        }

        if (settings.DataType == TagDataType.Virtual)
        {
            return rawValue;
        }

        // Защита от деления на ноль (эпсилон-сравнение).
        if (Math.Abs(settings.InputMax - settings.InputMin) < 0.000001)
        {
            return settings.OutputMin + settings.OffsetVal;
        }

        double scaledValue =
            (rawValue - settings.InputMin)
                / (settings.InputMax - settings.InputMin)
                * (settings.OutputMax - settings.OutputMin)
            + settings.OutputMin;

        return scaledValue + settings.OffsetVal;
    }
}
