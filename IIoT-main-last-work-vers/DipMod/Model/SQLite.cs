using System;
using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace DipMod.Model
{
    /// <summary>
    /// Класс для работы с локальной базой данных SQLite.
    /// Реализует методы создания таблиц, вставки и чтения данных с учетом многопоточности.
    /// </summary>
    internal class SQlite
    {
        SqliteConnection connection;

        // Флаг записи, предотвращающий одновременное чтение и запись
        private bool _isWriting = false;

        // Объект для блокировки потоков (thread-safety)
        private readonly object _lockObject = new object();

        // Значение для калибровки напряжения (10V)
        private int znach = 10;
        AnalogPort port;

        public SQlite(string namedb)
        {
            Batteries.Init();
            connection = new SqliteConnection($"Data Source={namedb}");
            connection.Open();
            SQLiteAddTable();
        }

        /// <summary>
        /// Создание таблиц AnalogPort и DigitalPort, если они не существуют.
        /// Используется составной первичный ключ (IDPort, TimeFroze).
        /// </summary>
        int SQLiteAddTable()
        {
            var tableCommand = connection.CreateCommand();
            tableCommand.CommandText =
                @"CREATE TABLE IF NOT EXISTS AnalogPort (
            IDPort INTEGER,
            Value INTEGER NOT NULL,
            TimeFroze DATETIME NOT NULL,
            PRIMARY KEY (IDPort, TimeFroze)
                );

                CREATE TABLE IF NOT EXISTS DigitalPort (
            IDPort INTEGER,
            Value INTEGER NOT NULL,
            TimeFroze DATETIME NOT NULL,
            PRIMARY KEY (IDPort, TimeFroze)
                    );";
            return tableCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Вставка данных аналогового порта.
        /// Использует блокировку для потокобезопасности.
        /// </summary>
        public void InsertAnalog(AnalogPort aport)
        {
            lock (_lockObject)
            {
                _isWriting = true;
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    "INSERT INTO AnalogPort (IDPort, Value,TimeFroze) VALUES (@IDPort, @Value, @TimeFroze);";
                insertCommand.Parameters.AddWithValue("@IDPort", aport.IDPort);
                insertCommand.Parameters.AddWithValue("@Value", aport.value);
                insertCommand.Parameters.AddWithValue("@TimeFroze", aport.TimeFroze);
                insertCommand.ExecuteNonQuery();
                _isWriting = false;
            }
        }

        /// <summary>
        /// Вставка данных цифрового порта.
        /// </summary>
        public void InsertDigital(DigitalPort dport)
        {
            lock (_lockObject)
            {
                _isWriting = true;
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    "INSERT INTO DigitalPort (IDPort, Value,TimeFroze) VALUES (@IDPort, @Value, @TimeFroze);";
                insertCommand.Parameters.AddWithValue("@IDPort", dport.IDPort);
                insertCommand.Parameters.AddWithValue("@Value", dport.value);
                insertCommand.Parameters.AddWithValue("@TimeFroze", dport.TimeFroze);
                insertCommand.ExecuteNonQuery();
                _isWriting = false;
            }
        }

        /// <summary>
        /// Чтение всех данных аналоговых портов.
        /// Преобразует RAW значения в вольты.
        /// </summary>
        public ObservableCollection<AnalogPort> GetDataAnalog()
        {
            if (_isWriting) // Защита от конфликтов
            {
                return null;
            }
            var data = new ObservableCollection<AnalogPort>();
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "select * from AnalogPort";
            var reader = insertCommand.ExecuteReader();
            while (reader.Read())
            {
                // Формула преобразования: (RAW / 65536) * 20 - 10
                float val =
                    ((reader.GetFieldValue<float>(1)) / 65536) * (znach - (-znach)) + (-znach);
                var aport = new AnalogPort(
                    reader.GetFieldValue<ushort>(0),
                    val,
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(aport);
            }

            return data;
        }

        /// <summary>
        /// Чтение всех данных цифровых портов.
        /// </summary>
        public ObservableCollection<DigitalPort> GetDataDigital()
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "select * from DigitalPort";
            var reader = insertCommand.ExecuteReader();
            var data = new ObservableCollection<DigitalPort>();
            while (reader.Read())
            {
                var dport = new DigitalPort(
                    reader.GetFieldValue<ushort>(0),
                    reader.GetFieldValue<bool>(1),
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(dport);
            }
            return data;
        }

        /// <summary>
        /// Чтение аналоговых данных за период времени.
        /// Использует параметризованный запрос для защиты от SQL-инъекций.
        /// </summary>
        public ObservableCollection<AnalogPort> GetDataAnalog(DateTime dateStart, DateTime dateEnd)
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                "SELECT * FROM AnalogPort WHERE TimeFroze > @StartDate AND TimeFroze < @EndDate";
            if (dateStart > dateEnd)
            {
                new Exception("Дата окончания меньше даты начала");
            }
            // Параметры для предотвращения ошибок формата даты и SQL-инъекций
            insertCommand.Parameters.AddWithValue("@StartDate", dateStart);
            insertCommand.Parameters.AddWithValue("@EndDate", dateEnd);
            var reader = insertCommand.ExecuteReader();
            var data = new ObservableCollection<AnalogPort>();
            while (reader.Read())
            {
                float val = reader.GetFieldValue<float>(1) / 65536 * (znach - (-znach)) + (-znach);
                var aport = new AnalogPort(
                    reader.GetFieldValue<ushort>(0),
                    val,
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(aport);
            }
            return data;
        }

        /// <summary>
        /// Чтение цифровых данных за период времени.
        /// ВНИМАНИЕ: Содержит ошибку - запрос идет к таблице AnalogPort вместо DigitalPort.
        /// Также используется небезопасная интерполяция строк.
        /// </summary>
        public ObservableCollection<DigitalPort> GetDataDigital(
            DateTime dateStart,
            DateTime dateEnd
        )
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            // !BUG: Здесь ошибка "select * from AnalogPort" должно быть "from DigitalPort"
            insertCommand.CommandText =
                $"select * from AnalogPort where TimeFroze>{dateStart} and TimeFroze<{dateEnd}";
            var reader = insertCommand.ExecuteReader();
            var data = new ObservableCollection<DigitalPort>();
            while (reader.Read())
            {
                var dport = new DigitalPort(
                    reader.GetFieldValue<ushort>(0),
                    reader.GetFieldValue<bool>(1),
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(dport);
            }
            return data;
        }

        /// <summary>
        /// Получение последних 8 записей аналоговых портов (текущее состояние).
        /// </summary>
        public ObservableCollection<AnalogPort> GetDataAnalogLast()
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                $"SELECT IDPort, Value, TimeFroze FROM AnalogPort ORDER BY TimeFroze DESC LIMIT 8";
            var reader = insertCommand.ExecuteReader();
            var data = new ObservableCollection<AnalogPort>();
            while (reader.Read())
            {
                float val = reader.GetFieldValue<float>(1) / 65536 * (znach - (-znach)) + (-znach);
                var aport = new AnalogPort(
                    reader.GetFieldValue<ushort>(0),
                    val,
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(aport);
            }
            return data;
        }

        DigitalPort portd;

        /// <summary>
        /// Получение последнего значения конкретного аналогового порта.
        /// </summary>
        public AnalogPort GetAnalogPortToIdPort(int IdPort)
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                $"SELECT IDPort, Value, TimeFroze FROM AnalogPort Where IDPort = {IdPort} ORDER BY TimeFroze DESC LIMIT 1";
            var reader = insertCommand.ExecuteReader();

            if (reader.Read())
            {
                float val = reader.GetFieldValue<float>(1) / 65536 * (znach - (-znach)) + (-znach);
                port = new AnalogPort(
                    reader.GetFieldValue<ushort>(0),
                    val,
                    reader.GetFieldValue<DateTime>(2)
                );
            }
            return port;
        }

        /// <summary>
        /// Получение последнего значения конкретного цифрового порта.
        /// </summary>
        public DigitalPort GetDigitalPortToIdPort(int IdPort)
        {
            if (_isWriting)
            {
                return null;
            }

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                $"SELECT IDPort, Value, TimeFroze FROM DigitalPort Where IDPort = {IdPort} ORDER BY TimeFroze DESC LIMIT 1";
            var reader = insertCommand.ExecuteReader();
            if (reader.Read())
                portd = new DigitalPort(
                    reader.GetFieldValue<ushort>(0),
                    reader.GetFieldValue<bool>(1),
                    reader.GetFieldValue<DateTime>(2)
                );
            return portd;
        }

        /// <summary>
        /// Получение последних 2 записей цифровых портов.
        /// </summary>
        public ObservableCollection<DigitalPort> GetDataDigitalLast()
        {
            if (_isWriting)
            {
                return null;
            }
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                "SELECT IDPort, Value, TimeFroze FROM DigitalPort ORDER BY TimeFroze DESC LIMIT 2";
            var reader = insertCommand.ExecuteReader();
            var data = new ObservableCollection<DigitalPort>();
            while (reader.Read())
            {
                var dport = new DigitalPort(
                    reader.GetFieldValue<ushort>(0),
                    reader.GetFieldValue<bool>(1),
                    reader.GetFieldValue<DateTime>(2)
                );
                data.Add(dport);
            }
            return data;
        }
    }
}
