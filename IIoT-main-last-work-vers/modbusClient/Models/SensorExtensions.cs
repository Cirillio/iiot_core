using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModbusClient.Models;

namespace ModbusClient.Models
{
    /// <summary>
    /// Класс расширений для работы с данными сенсоров.
    /// Содержит логику преобразования "сырых" значений в физические величины.
    /// </summary>
    public static class SensorExtensions
    {
        /// <summary>
        /// Вычисляет физическое значение датчика на основе "сырых" данных и конфигурации.
        /// </summary>
        /// <param name="rawValue">Сырое значение, полученное с Modbus-устройства (обычно 0-65535).</param>
        /// <param name="settings">Настройки датчика, содержащие параметры калибровки (Min/Max, Offset).</param>
        /// <returns>
        /// Откалиброванное значение (double).
        /// </returns>
        /// <remarks>
        /// Логика расчета зависит от типа датчика (<see cref="SensorDataType"/>):
        /// <list type="bullet">
        /// <item>
        /// <description><b>DIGITAL:</b> Возвращает 1.0, если rawValue > 0, иначе 0.0.</description>
        /// </item>
        /// <item>
        /// <description><b>VIRTUAL:</b> В текущей реализации возвращает значение без изменений (заглушка). В будущем здесь ожидается парсинг формул.</description>
        /// </item>
        /// <item>
        /// <description><b>ANALOG:</b> Применяет линейную интерполяцию:
        /// <code>Value = ((Raw - InMin) / (InMax - InMin)) * (OutMax - OutMin) + OutMin + Offset</code>
        /// Предусмотрена защита от деления на ноль: если (InMax - InMin) близко к 0, возвращается (OutMin + Offset).
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static double Calculate(double rawValue, SensorSettings settings)
        {
            // Для дискретных датчиков просто возвращаем 0 или 1
            if (settings.DataType == SensorDataType.DIGITAL)
            {
                return rawValue > 0 ? 1.0 : 0.0;
            }

            // Для виртуальных датчиков здесь может быть вызов парсера формул (NCalc или аналоги)
            if (settings.DataType == SensorDataType.VIRTUAL)
            {
                // Пока заглушка, возвращаем как есть
                return rawValue;
            }

            // Защита от деления на ноль. Используем эпсилон для сравнения float/double.
            if (Math.Abs(settings.InputMax - settings.InputMin) < 0.000001)
            {
                return settings.OutputMin + settings.OffsetVal;
            }

            // Линейная интерполяция (Scaling)
            // Формула переводит значение из диапазона [InputMin, InputMax] в диапазон [OutputMin, OutputMax]
            double scaledValue =
                (rawValue - settings.InputMin)
                    / (settings.InputMax - settings.InputMin)
                    * (settings.OutputMax - settings.OutputMin)
                + settings.OutputMin;

            // Добавляем смещение (Offset) для калибровки нуля
            return scaledValue + settings.OffsetVal;
        }
    }
}
