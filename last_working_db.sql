/**
 * @module IIoT_Database_Core
 * @description Система хранения и обработки временных рядов на базе TimescaleDB.
 * Обеспечивает автоматическое сжатие, непрерывную агрегацию и управление жизненным циклом данных.
 * 
 * ## Особенности:
 * - Гипертаблицы для оптимизации временных рядов
 * - Автоматическая очистка (Retention Policy)
 * - Встроенные механизмы аудита изменений
 */

CREATE EXTENSION IF NOT EXISTS timescaledb;

/**
 * @enum {string} sensor_data_type
 * @description Тип физической или логической природы источника данных.
 * @value ANALOG - Аналоговый сигнал (требует калибровки).
 * @value DIGITAL - Дискретный сигнал (0/1).
 * @value VIRTUAL - Вычисляемое значение (на основе формулы).
 */
DO $$ BEGIN
    CREATE TYPE sensor_data_type AS ENUM ('ANALOG', 'DIGITAL', 'VIRTUAL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

/**
 * @enum {string} system_service_status
 * @description Статусы жизненного цикла системных компонентов.
 * @value ONLINE - Штатная работа.
 * @value OFFLINE - Сервис остановлен.
 * @value DEGRADED - Работа с ошибками.
 * @value CRITICAL_ERROR - Критический сбой.
 * @value MAINTENANCE - Техническое обслуживание.
 */
DO $$ BEGIN
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

/**
 * @function fn_update_timestamp
 * @description Триггерная функция для автоматического обновления поля updated_at.
 * Устанавливает значение текущего времени при изменении записи.
 * @returns {trigger} Объект триггера PostgreSQL.
 */
CREATE OR REPLACE FUNCTION fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

/**
 * @table devices
 * @description Реестр физических контроллеров/устройств сбора данных (PLC, IoT Gateway).
 * Создается ПЕРЕД датчиками для обеспечения ссылочной целостности.
 * 
 * @property {serial} id - Уникальный идентификатор устройства.
 * @property {varchar} name - Имя устройства.
 * @property {varchar} ip_address - IP адрес (например, '192.168.0.10').
 * @property {integer} port - Порт Modbus TCP (default 502).
 * @property {boolean} is_active - Флаг активности опроса.
 */
CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    port INT DEFAULT 502,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

/**
 * @table sensor_settings
 * @description Реестр конфигураций датчиков и параметров нормализации сигналов.
 * Хранит метаданные, необходимые для интерпретации сырых значений и отображения их в UI.
 * 
 * @property {serial} sensor_id - Уникальный идентификатор датчика (PK).
 * @property {integer} device_id - Ссылка на устройство (PLC).
 * @property {integer} port_number - Физический номер порта на контроллере.
 * @property {varchar} name - Человекочитаемое название датчика.
 * @property {varchar} slug - Уникальный строковый идентификатор для API и формул (Unique).
 * @property {sensor_data_type} data_type - Тип сигнала (по умолчанию 'ANALOG').
 * @property {varchar} unit - Единица измерения (например, '°C', 'Bar').
 * @property {double precision} input_min - Минимальное значение входного сигнала (ADC).
 * @property {double precision} input_max - Максимальное значение входного сигнала (ADC).
 * @property {double precision} output_min - Физическое значение при input_min.
 * @property {double precision} output_max - Физическое значение при input_max.
 * @property {double precision} offset_val - Статическое смещение для калибровки.
 * @property {text} formula - Математическая формула для виртуальных датчиков.
 * @property {jsonb} ui_config - Конфигурация интерфейса (цвет, иконка, границы).
 * @property {timestamptz} updated_at - Время последнего изменения настройки.
 * @see sensor_data_type
 */
CREATE TABLE IF NOT EXISTS sensor_settings (
    sensor_id SERIAL PRIMARY KEY,
    device_id INT REFERENCES devices(id) ON DELETE RESTRICT,
    port_number INT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) UNIQUE,
    data_type sensor_data_type DEFAULT 'ANALOG',
    unit VARCHAR(20),
    input_min DOUBLE PRECISION DEFAULT 0,
    input_max DOUBLE PRECISION DEFAULT 65535,
    output_min DOUBLE PRECISION DEFAULT 0,
    output_max DOUBLE PRECISION DEFAULT 100,
    offset_val DOUBLE PRECISION DEFAULT 0,
    formula TEXT,
    ui_config JSONB DEFAULT '{}',
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    -- Гарантия уникальности порта на устройстве (Физическая уникальность)
    CONSTRAINT uq_sensor_device_port UNIQUE (device_id, port_number)
);

DROP TRIGGER IF EXISTS trg_update_sensor_settings_time ON sensor_settings;
CREATE TRIGGER trg_update_sensor_settings_time
BEFORE UPDATE ON sensor_settings
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

/**
 * @table metrics
 * @description Основная гипертаблица для хранения временных рядов телеметрии.
 * Сжатие данных активировано по сегментам (sensor_id).
 * 
 * @property {timestamptz} time - Метка времени события (UTC). Ключ партиционирования.
 * @property {integer} sensor_id - Ссылка на датчик (FK -> sensor_settings).
 * @property {double precision} raw_value - Сырое значение сигнала (до калибровки).
 * @property {double precision} value - Нормализованное физическое значение.
 */
CREATE TABLE IF NOT EXISTS metrics (
    time TIMESTAMPTZ NOT NULL,
    sensor_id INT REFERENCES sensor_settings(sensor_id) ON DELETE CASCADE,
    raw_value DOUBLE PRECISION,
    value DOUBLE PRECISION NOT NULL
);

SELECT create_hypertable('metrics', 'time', if_not_exists => TRUE);

ALTER TABLE metrics SET (
  timescaledb.compress,
  timescaledb.compress_segmentby = 'sensor_id'
);

SELECT add_compression_policy('metrics', INTERVAL '7 days');

CREATE INDEX IF NOT EXISTS ix_metrics_sensor_time ON metrics (sensor_id, time DESC);

/**
 * @view metrics_hourly
 * @description Материализованное представление (Continuous Aggregate).
 * Содержит предварительно вычисленные агрегаты данных с часовым интервалом.
 * Используется для быстрого построения графиков за длинные периоды.
 * 
 * @property {timestamptz} bucket - Начало часового интервала.
 * @property {integer} sensor_id - Идентификатор датчика.
 * @property {double precision} avg_value - Среднее значение за час.
 * @property {double precision} max_value - Максимальное значение за час.
 * @property {double precision} min_value - Минимальное значение за час.
 */
CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_hourly
WITH (timescaledb.continuous) AS
SELECT time_bucket('1 hour', time) AS bucket,
       sensor_id,
       AVG(value) as avg_value,
       MAX(value) as max_value,
       MIN(value) as min_value
FROM metrics
GROUP BY bucket, sensor_id;

SELECT add_continuous_aggregate_policy('metrics_hourly', 
    start_offset => INTERVAL '1 day',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour');

/**
 * @table system_status
 * @description Мониторинг текущего состояния и здоровья системных сервисов.
 * 
 * @property {varchar} service_name - Уникальное имя сервиса (PK).
 * @property {system_service_status} status - Текущий статус работы.
 * @property {bigint} uptime_seconds - Время аптайма в секундах.
 * @property {text} last_error - Текст последней ошибки (если есть).
 * @property {timestamptz} last_sync - Время последнего "heartbeat" от сервиса.
 */
CREATE TABLE IF NOT EXISTS system_status (
    service_name VARCHAR(50) PRIMARY KEY,
    status system_service_status DEFAULT 'OFFLINE',
    uptime_seconds BIGINT DEFAULT 0,
    last_error TEXT,
    last_sync TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

/**
 * @table system_config
 * @description Глобальные настройки системы и параметры политик хранения (Retention).
 * 
 * @property {serial} id - Идентификатор конфигурации.
 * @property {integer} raw_retention_days - Срок хранения сырых данных в днях (мин. 1).
 * @property {integer} agg_retention_days - Срок хранения агрегатов в днях (мин. 7).
 * @property {timestamptz} updated_at - Время последнего обновления настроек.
 */
CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    raw_retention_days INT DEFAULT 90,
    agg_retention_days INT DEFAULT 1825,
    polling_interval_ms INT DEFAULT 1000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    deadband_threshold double DEFAULT 0.01,
    data_heartbeat_sec int DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

DROP TRIGGER IF EXISTS trg_update_system_config_time ON system_config;
CREATE TRIGGER trg_update_system_config_time
BEFORE UPDATE ON system_config
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

ALTER TABLE system_config 
DROP CONSTRAINT IF EXISTS chk_retention_min;

ALTER TABLE system_config 
ADD CONSTRAINT chk_retention_min CHECK (raw_retention_days >= 1 AND agg_retention_days >= 7);

INSERT INTO system_config (raw_retention_days, agg_retention_days)
SELECT 90, 1825
WHERE NOT EXISTS (SELECT 1 FROM system_config);

/**
 * @table maintenance_logs
 * @description Журнал аудита выполнения автоматических процедур обслуживания.
 * 
 * @property {timestamptz} log_time - Время записи события.
 * @property {varchar} table_name - Имя таблицы или компонента ('system').
 * @property {text} description - Описание операции или ошибки.
 * @property {varchar} status - Результат операции ('SUCCESS', 'ERROR').
 */
CREATE TABLE IF NOT EXISTS maintenance_logs (
    log_time TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    table_name VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(10) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_maintenance_logs_time ON maintenance_logs (log_time DESC);

/**
 * @procedure prc_run_retention
 * @description Выполняет политику очистки устаревших данных (Retention Policy).
 * Читает настройки сроков хранения из таблицы system_config и удаляет старые чанки.
 * 
 * @throws {exception} Перехватывает любые ошибки и записывает их в maintenance_logs.
 * @example CALL prc_run_retention();
 */
CREATE OR REPLACE PROCEDURE prc_run_retention()
LANGUAGE plpgsql
AS $$
DECLARE
    v_raw_days INT;
    v_agg_days INT;
BEGIN
    SELECT raw_retention_days, agg_retention_days 
    INTO v_raw_days, v_agg_days 
    FROM system_config LIMIT 1;

    PERFORM drop_chunks('metrics', (v_raw_days || ' days')::interval);
    PERFORM drop_chunks('metrics_hourly', (v_agg_days || ' days')::interval);

    INSERT INTO maintenance_logs (table_name, description, status) 
    VALUES ('system', 'Retention completed: Raw=' || v_raw_days || 'd, Agg=' || v_agg_days || 'd', 'SUCCESS');

EXCEPTION WHEN OTHERS THEN
    INSERT INTO maintenance_logs (table_name, description, status) 
    VALUES ('system', 'Error: ' || SQLERRM, 'ERROR');
END;
$$;

/**
 * @description Блок регистрации фонового задания TimescaleDB.
 * Проверяет наличие задания 'prc_run_retention' и создает его, если оно отсутствует.
 * Расписание запуска: каждые 24 часа.
 * @see prc_run_retention
 */
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM timescaledb_information.jobs 
        WHERE proc_name = 'prc_run_retention'
    ) THEN
        PERFORM add_job('prc_run_retention', '1 day');
    END IF;
END $$;

-- 10. КОММЕНТАРИИ ДЛЯ ДОКУМЕНТАЦИИ (Дополнительные)
COMMENT ON TABLE metrics IS 'Гипертаблица временных рядов телеметрии';
COMMENT ON COLUMN metrics.raw_value IS 'Сырой код АЦП (0-65535)';
COMMENT ON COLUMN metrics.value IS 'Физическое значение после калибровки';
COMMENT ON VIEW metrics_hourly IS 'Почасовые агрегаты для быстрой аналитики';
