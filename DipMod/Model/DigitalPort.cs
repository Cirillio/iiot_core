using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DipMod.Model
{
    /// <summary>
    /// Модель данных для цифрового порта.
    /// Хранит логическое состояние (вкл/выкл) и время измерения.
    /// </summary>
    internal class DigitalPort
    {
        public ushort IDPort;       // ID порта (0-1). Всего 2 цифровых порта.
        public bool value;          // Логическое значение (true/false)
        public DateTime TimeFroze;  // Время снятия показания


        public DigitalPort(ushort iDPort, bool value, DateTime timeFroze)
        {
            IDPort = iDPort;
            this.value = value;
            TimeFroze = timeFroze;
        }
    }
}
