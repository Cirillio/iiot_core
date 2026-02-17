namespace DipMod.Model
{
    /// <summary>
    /// Класс для работы с протоколом Modbus TCP.
    /// Отвечает за подключение к устройству, чтение данных и взаимодействие с БД.
    /// </summary>
    internal class Modbus
    {
        // Клиентское подключение по TCP (порт 502)
        readonly TcpClient client;

        // Фабрика для создания Modbus мастеров
        readonly ModbusFactory factory = new ModbusFactory();

        // Modbus мастер для выполнения запросов
        readonly IModbusMaster master;

        // Экземпляр класса для работы с базой данных SQLite
        public SQlite db;

        // Начальный адрес регистров
        public ushort startAddress = 0;

        // Количество считываемых аналоговых регистров (8 каналов)
        public ushort numRegistersAnalog = 8;

        // Количество цифровых регистров (2 канала)
        public ushort numRegistersADigital = 2;

        // Конфигурация канала аналоговых портов (0x0008 - режим АЦП)
        readonly ushort channelConfig = 0x0008;

        // Диапазон напряжения для преобразования (±10V)
        private int znach = 10;

        // Адрес устройства (Slave ID)
        private readonly byte mash;

        /// <summary>
        /// Конструктор класса Modbus.
        /// Инициализирует подключение, настраивает каналы и БД.
        /// </summary>
        /// <param name="IpAdress">IP адрес устройства</param>
        /// <param name="machine">Адрес устройства (Slave ID)</param>
        public Modbus(string IpAdress, int machine)
        {
            // Инициализация TCP клиента для подключения на стандартный порт 502
            client = new TcpClient(IpAdress, 502);
            // Создаем мастера используя подключенного клиента
            master = factory.CreateMaster(client);
            mash = Convert.ToByte(machine);

            // Настройка конфигурации всех 8 аналоговых каналов.
            // Записываем 0x0008 в регистры 40001-40008.
            master.WriteSingleRegister(mash, 40001, channelConfig);
            master.WriteSingleRegister(mash, 40002, channelConfig);
            master.WriteSingleRegister(mash, 40003, channelConfig);
            master.WriteSingleRegister(mash, 40004, channelConfig);
            master.WriteSingleRegister(mash, 40005, channelConfig);
            master.WriteSingleRegister(mash, 40006, channelConfig);
            master.WriteSingleRegister(mash, 40007, channelConfig);
            master.WriteSingleRegister(mash, 40008, channelConfig);

            // Инициализируем базу данных
            db = new SQlite("Port.db");
        }

        public void Disconnect()
        {
            // Разрыв соединения
            client.Close();
        }

        /// <summary>
        /// Чтение аналоговых данных с записью в БД.
        /// Используется функцией 0x03 (Read Holding Registers).
        /// </summary>
        int GetAnalog()
        {
            // Считывание времени для записи в базу данных
            DateTime real = DateTime.Now;
            // Считываем с устройства под указаным адресом аналоговые регистры
            // начиная с адреса записаного в startAddress и количество numRegistersAnalog
            ushort[] analog = master.ReadHoldingRegisters(mash, startAddress, numRegistersAnalog);
            // Записываем стартовый адрес для записи в БД
            ushort countreg = startAddress;
            // Перебираем буфер для записи
            for (ushort i = countreg; i < analog.lingth; i++)
            {
                // Добавляем в базу запись: ID порта, RAW значение (analog[i]), время
                db.InsertAnalog(new AnalogPort(i, analog[i], real));
                // Увеличиваем значение регистра (i) (ID порта) для следующей итерации
            }

            // Возвращаем количество считанных регистров
            return analog.Length;
        }

        /// <summary>
        /// Чтение аналоговых данных для онлайн-графика БЕЗ записи в БД.
        /// Преобразует RAW значения в вольты.
        /// </summary>
        /// <returns>Коллекция данных для отображения</returns>
        public ObservableCollection<AnalogPort> GetAnalogOnline()
        {
            try
            {
                // Считывание времени
                DateTime real = DateTime.Now;
                // Считываем holding registers
                ushort[] analog = master.ReadHoldingRegisters(
                    mash,
                    startAddress,
                    numRegistersAnalog
                );
                // Начальный ID порта
                ushort countreg = startAddress;
                // Создаем коллекцию для последующего вывода на график
                ObservableCollection<AnalogPort> Aports = new ObservableCollection<AnalogPort>();

                // Перебираем полученные значения
                foreach (float s in analog)
                {
                    // Преобразуем абсолютные значения (0-65535) в вольты (-10 до +10V)
                    // Формула: val = s / 65536 * 20 - 10
                    float val = s / 65536 * (znach - (-znach)) + (-znach);
                    // Увеличиваем ID порта (исправлено положение инкремента относительно оригинала для корректности логики, но сохраняем старый порядок если он был важен, хотя тут он был странный)
                    // В оригинале countreg++ был перед Aports.Add, значит IDs начинались с 1? startAddress=0.
                    // Проверим оригинал: countreg++; Aports.Add(...). Да, порты 1-8.
                    countreg++;
                    Aports.Add(new AnalogPort(countreg, val, real));
                }
                // Возвращаем коллекцию
                return Aports;
            }
            // Обработка ошибок
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                client.Close();
                return null;
            }
        }

        /// <summary>
        /// Чтение цифровых входов с записью в БД.
        /// Используется функция 0x02 (Read Discrete Inputs).
        /// </summary>
        int GetDigital()
        {
            // Считывание времени для записи в базу данных
            DateTime real = DateTime.Now;
            // Считываем discrete inputs
            bool[] digitalOutputs = master.ReadInputs(mash, startAddress, numRegistersADigital);
            // Начальный ID порта
            ushort countreg = startAddress;
            // Перебираем полученный список для записи в БД
            foreach (bool s in digitalOutputs)
            {
                // Добавляем запись в таблицу DigitalPort
                db.InsertDigital(new DigitalPort(countreg, s, real));
                // Увеличиваем ID порта
                countreg++;
            }
            // Возвращаем 0 (в оригинале так)
            return 0;
        }

        /// <summary>
        /// Запуск цикла сбора данных (один проход).
        /// Вызывает чтение аналоговых и цифровых портов.
        /// </summary>
        public void Start()
        {
            try
            {
                GetAnalog();
                GetDigital();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                client.Close();
            }
        }
    }
}
