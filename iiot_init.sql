/**
 * @module IIoT_Database_Core
 * @description Система хранения и обработки временных рядов на базе TimescaleDB.
 * Скрипт является идемпотентным: можно запускать многократно без ошибок.
 */

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- =============================================
-- 1. ENUMS (Типы данных)
-- =============================================

DO $$ BEGIN
    CREATE TYPE sensor_data_type AS ENUM ('ANALOG', 'DIGITAL', 'VIRTUAL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

-- =============================================
-- 2. UTILITY FUNCTIONS (Служебные функции)
-- =============================================

-- Функция автообновления updated_at
CREATE OR REPLACE FUNCTION fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Функция уведомления (Real-time)
CREATE OR REPLACE FUNCTION fn_trigger_notify_metrics()
RETURNS TRIGGER AS $$
DECLARE
    payload JSON;
BEGIN
    -- Формируем JSON с PascalCase ключами для C# моделей
    payload = json_build_object(
        'Time', NEW.time,
        'SensorId', NEW.sensor_id,
        'RawValue', NEW.raw_value,
        'Value', NEW.value
    );
    
    -- Отправляем в канал 'metrics_realtime'
    PERFORM pg_notify('metrics_realtime', payload::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- 3. CORE TABLES (Основные таблицы)
-- =============================================

-- 3.1. Devices (Устройства) - Гарантирует целостность для датчиков
CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    port INT DEFAULT 502,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3.2. Sensor Settings (Конфигурация датчиков)
-- device_id NOT NULL и REFERENCES заменяют триггеры проверки существования устройства
CREATE TABLE IF NOT EXISTS sensor_settings (
    sensor_id SERIAL PRIMARY KEY,
    device_id INT NOT NULL REFERENCES devices(id) ON DELETE RESTRICT,
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
    CONSTRAINT uq_sensor_device_port UNIQUE (device_id, port_number)
);

-- Триггер обновления времени конфига
DROP TRIGGER IF EXISTS trg_update_sensor_settings_time ON sensor_settings;
CREATE TRIGGER trg_update_sensor_settings_time
BEFORE UPDATE ON sensor_settings
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

-- 3.3. Metrics (Телеметрия)
-- sensor_id NOT NULL и REFERENCES заменяют триггеры проверки существования датчика
CREATE TABLE IF NOT EXISTS metrics (
    time TIMESTAMPTZ NOT NULL,
    sensor_id INT NOT NULL REFERENCES sensor_settings(sensor_id) ON DELETE CASCADE,
    raw_value DOUBLE PRECISION,
    value DOUBLE PRECISION NOT NULL
);

-- Индекс для быстрого поиска
CREATE INDEX IF NOT EXISTS ix_metrics_sensor_time ON metrics (sensor_id, time DESC);

-- =============================================
-- 4. TIMESCALEDB SETUP (Настройка гипертаблиц)
-- =============================================

-- Конвертация в гипертаблицу (Safe mode)
SELECT create_hypertable('metrics', 'time', if_not_exists => TRUE);

-- Настройка сжатия (через блок DO для идемпотентности)
DO $$
BEGIN
    ALTER TABLE metrics SET (
      timescaledb.compress,
      timescaledb.compress_segmentby = 'sensor_id'
    );
EXCEPTION WHEN OTHERS THEN NULL;
END $$;

-- Политика сжатия (старее 7 дней)
DO $$
BEGIN
    PERFORM add_compression_policy('metrics', INTERVAL '7 days');
EXCEPTION WHEN OTHERS THEN NULL;
END $$;

-- Триггер уведомлений (Real-time)
DROP TRIGGER IF EXISTS trg_notify_metric ON metrics;
CREATE TRIGGER trg_notify_metric
AFTER INSERT ON metrics
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_metrics();

-- =============================================
-- 5. ANALYTICS (Агрегаты)
-- =============================================

CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_hourly
WITH (timescaledb.continuous) AS
SELECT time_bucket('1 hour', time) AS bucket,
       sensor_id,
       AVG(value) as avg_value,
       MAX(value) as max_value,
       MIN(value) as min_value
FROM metrics
GROUP BY bucket, sensor_id;

-- Политика обновления агрегатов
DO $$
BEGIN
    PERFORM add_continuous_aggregate_policy('metrics_hourly', 
        start_offset => INTERVAL '1 day',
        end_offset => INTERVAL '1 hour',
        schedule_interval => INTERVAL '1 hour');
EXCEPTION WHEN OTHERS THEN NULL;
END $$;

-- =============================================
-- 6. SYSTEM TABLES (Системные таблицы)
-- =============================================

CREATE TABLE IF NOT EXISTS system_status (
    service_name VARCHAR(50) PRIMARY KEY,
    status system_service_status DEFAULT 'OFFLINE',
    uptime_seconds BIGINT DEFAULT 0,
    last_error TEXT,
    last_sync TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    raw_retention_days INT DEFAULT 90,
    agg_retention_days INT DEFAULT 1825,
    polling_interval_ms INT DEFAULT 1000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    deadband_threshold double DEFAULT 0.01,
    data_heartbeat_sec INT DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Триггер конфига
DROP TRIGGER IF EXISTS trg_update_system_config_time ON system_config;
CREATE TRIGGER trg_update_system_config_time
BEFORE UPDATE ON system_config
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

-- Валидация конфига
ALTER TABLE system_config DROP CONSTRAINT IF EXISTS chk_retention_min;
ALTER TABLE system_config ADD CONSTRAINT chk_retention_min CHECK (raw_retention_days >= 1 AND agg_retention_days >= 7);

-- Seed (Начальные данные)
TRUNCATE system_config CASCADE;
INSERT INTO system_config (raw_retention_days, agg_retention_days, deadband_threshold)
VALUES (90, 1825, 0.0);

-- Seed Devices & Sensors
TRUNCATE devices CASCADE;
INSERT INTO devices (name, ip_address, port, is_active)
VALUES ('Test PLC', 'sim', 5020, true);

INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, input_min, input_max, output_min, output_max)
SELECT d.id, 7, 'Temperature', 'temp_1', 'ANALOG', 0, 65535, 0, 100
FROM devices d WHERE d.name = 'Test PLC';

INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, input_min, input_max, output_min, output_max)
SELECT d.id, 0, 'Pressure Status', 'press_1', 'DIGITAL', 0, 65535, 0, 1
FROM devices d WHERE d.name = 'Test PLC';

CREATE TABLE IF NOT EXISTS maintenance_logs (
    log_time TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    table_name VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(10) NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_maintenance_logs_time ON maintenance_logs (log_time DESC);

-- Функция уведомления (Real-time)
CREATE OR REPLACE FUNCTION fn_trigger_notify_metrics()
RETURNS TRIGGER AS $$
DECLARE
    payload JSON;
BEGIN
    -- Формируем JSON с PascalCase ключами для C# моделей
    payload = json_build_object(
        'Time', NEW.time,
        'SensorId', NEW.sensor_id,
        'RawValue', NEW.raw_value,
        'Value', NEW.value
    );
    
    -- Отправляем в канал 'metrics_realtime'
    PERFORM pg_notify('metrics_realtime', payload::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Триггер уведомлений (Real-time)
DROP TRIGGER IF EXISTS trg_notify_metric ON metrics;
CREATE TRIGGER trg_notify_metric
AFTER INSERT ON metrics
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_metrics();

-- =============================================
-- 7. PROCEDURES & JOBS (Обслуживание)
-- =============================================

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

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM timescaledb_information.jobs 
        WHERE proc_name = 'prc_run_retention'
    ) THEN
        PERFORM add_job('prc_run_retention', '1 day');
    END IF;
END $$;

-- 8. Комментарии
COMMENT ON TABLE metrics IS 'Гипертаблица временных рядов телеметрии';
COMMENT ON COLUMN metrics.raw_value IS 'Сырой код АЦП (0-65535)';
COMMENT ON COLUMN metrics.value IS 'Физическое значение после калибровки';
COMMENT ON VIEW metrics_hourly IS 'Почасовые агрегаты для быстрой аналитики';
