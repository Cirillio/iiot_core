using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModbusClient.Models;

namespace ModbusClient.Models
{
    public static class SensorExtensions
    {
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

            // Защита от деления на ноль
            if (Math.Abs(settings.InputMax - settings.InputMin) < 0.000001)
            {
                return settings.OutputMin + settings.OffsetVal;
            }

            // Линейная интерполяция (как в DipMod)
            double scaledValue =
                (rawValue - settings.InputMin)
                    / (settings.InputMax - settings.InputMin)
                    * (settings.OutputMax - settings.OutputMin)
                + settings.OutputMin;

            return scaledValue + settings.OffsetVal;
        }
    }
}
