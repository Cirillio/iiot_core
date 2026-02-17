using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DipMod.Model
{
    /// <summary>
    /// Модель данных для аналогового порта.
    /// Хранит информацию о значении напряжения и времени измерения.
    /// </summary>
    internal class AnalogPort
    {
        public ushort IDPort;       // ID порта (0-7)
        public float value;         // Значение в вольтах (-10 до +10V). Преобразовано из RAW данных.
        public DateTime TimeFroze;  // Время снятия показания
        public AnalogPort(ushort iDPort, float value, DateTime timeFroze)
        {
            IDPort = iDPort;
            this.value = value;
            TimeFroze = timeFroze;
        }
    }
}
