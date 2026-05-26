/**
 * @module IIoT_Database_Core
 * Vendor-agnostic schema: modbus_connections + devices + tags + metrics.
 */

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- 1. ENUMS
DO $$ BEGIN
    CREATE TYPE tag_data_type AS ENUM ('ANALOG', 'DIGITAL', 'VIRTUAL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE modbus_register_type AS ENUM ('INPUT_REGISTER', 'HOLDING_REGISTER', 'DISCRETE_INPUT', 'COIL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE modbus_endianness AS ENUM ('BIG_ENDIAN', 'LITTLE_ENDIAN', 'WORD_SWAP', 'BYTE_WORD_SWAP');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

-- 2. TABLES

-- Физическое сетевое соединение (сокет). Несколько устройств могут делить один шлюз.
CREATE TABLE IF NOT EXISTS modbus_connections (
    id SERIAL PRIMARY KEY,
    ip_address VARCHAR(50) NOT NULL,
    port INT NOT NULL DEFAULT 502,
    description VARCHAR(150),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT uq_connection_socket UNIQUE (ip_address, port)
);

CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    connection_id INT NOT NULL REFERENCES modbus_connections(id) ON DELETE RESTRICT,
    slave_id INT NOT NULL DEFAULT 1,
    use_group_polling BOOLEAN NOT NULL DEFAULT TRUE,
    max_register_span SMALLINT NOT NULL DEFAULT 120,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tags (
    tag_id SERIAL PRIMARY KEY,
    device_id INT NOT NULL REFERENCES devices(id) ON DELETE RESTRICT,
    port_number INT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) UNIQUE,
    data_type tag_data_type DEFAULT 'ANALOG',
    register_address INTEGER NOT NULL DEFAULT 0,
    register_type modbus_register_type NOT NULL DEFAULT 'INPUT_REGISTER',
    register_count SMALLINT NOT NULL DEFAULT 1,
    endianness modbus_endianness NOT NULL DEFAULT 'BIG_ENDIAN',
    unit VARCHAR(20),
    input_min DOUBLE PRECISION DEFAULT 0,
    input_max DOUBLE PRECISION DEFAULT 65535,
    output_min DOUBLE PRECISION DEFAULT 0,
    output_max DOUBLE PRECISION DEFAULT 100,
    offset_val DOUBLE PRECISION DEFAULT 0,
    formula TEXT,
    ui_config JSONB DEFAULT '{}',
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_tag_device_port UNIQUE (device_id, port_number)
);

CREATE TABLE IF NOT EXISTS metrics (
    time TIMESTAMPTZ NOT NULL,
    tag_id INT NOT NULL REFERENCES tags(tag_id) ON DELETE CASCADE,
    raw_value DOUBLE PRECISION,
    value DOUBLE PRECISION NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_metrics_tag_time ON metrics (tag_id, time DESC);

-- 3. TIMESCALEDB
SELECT create_hypertable('metrics', 'time', if_not_exists => TRUE);

CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time) AS time,
    tag_id,
    AVG(value) as value,
    MAX(value) as max_val,
    MIN(value) as min_val
FROM metrics
GROUP BY 1, 2
WITH NO DATA;

SELECT add_continuous_aggregate_policy('metrics_hourly',
    start_offset => INTERVAL '1 month',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE);

-- 4. TRIGGERS ON METRICS
CREATE OR REPLACE FUNCTION fn_trigger_notify_metrics() RETURNS TRIGGER AS $$
DECLARE
    payload JSON;
BEGIN
    payload = json_build_object(
        'time', NEW.time,
        'tagId', NEW.tag_id,
        'rawValue', NEW.raw_value,
        'value', NEW.value
    );
    PERFORM pg_notify('metrics_realtime', payload::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_metric AFTER INSERT ON metrics
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_metrics();

-- Авто-обновление tags.updated_at — нужно для инвалидации кэша конфигурации в коллекторе
CREATE OR REPLACE FUNCTION fn_update_tag_timestamp() RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_tags_updated_at BEFORE UPDATE ON tags
FOR EACH ROW EXECUTE FUNCTION fn_update_tag_timestamp();

-- 5. SYSTEM CONFIG
CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    raw_retention_days INT DEFAULT 90,
    agg_retention_days INT DEFAULT 1825,
    polling_interval_ms INT DEFAULT 5000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    deadband_threshold DOUBLE PRECISION DEFAULT 0.005,
    data_heartbeat_sec INT DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION fn_trigger_notify_config_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('config_changed', 'reload');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_config_change AFTER UPDATE OR INSERT ON system_config
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_config_change();

-- 6. SYSTEM STATUS
CREATE TABLE IF NOT EXISTS system_status (
    service_name VARCHAR(100) PRIMARY KEY,
    status system_service_status DEFAULT 'OFFLINE',
    uptime_seconds BIGINT DEFAULT 0,
    last_error TEXT,
    last_sync TIMESTAMPTZ DEFAULT NOW()
);

-- 7. SEED DATA
TRUNCATE modbus_connections, devices, tags, metrics RESTART IDENTITY CASCADE;

INSERT INTO system_config (raw_retention_days, agg_retention_days, polling_interval_ms, deadband_threshold)
VALUES (90, 1825, 5000, 0.001);

-- Соединение с симулятором
INSERT INTO modbus_connections (ip_address, port, description)
VALUES ('sim', 5020, 'Локальный симулятор ADAM');

-- Device: ADAM-6017 (Advantech) — 8-канальный модуль аналогового ввода
INSERT INTO devices (name, connection_id, slave_id, use_group_polling, max_register_span, is_active)
VALUES ('ADAM-6017',
        (SELECT id FROM modbus_connections WHERE ip_address = 'sim' AND port = 5020),
        1, true, 120, true);

-- Tags for ADAM-6017
-- Аналоговые входы — Input Registers (FC04), 16-bit (register_count=1, endianness не важен)
-- Дискретный — Discrete Input (FC02)
INSERT INTO tags
    (device_id, port_number, name, slug, data_type, register_address, register_type, register_count, unit, input_min, input_max, output_min, output_max)
VALUES
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 0, 'Температура',     'adam_temp',     'ANALOG'::tag_data_type,  0, 'INPUT_REGISTER'::modbus_register_type,  1, '°C',  0, 65535, -50, 150),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 1, 'Входное давление','adam_press_in', 'ANALOG'::tag_data_type,  1, 'INPUT_REGISTER'::modbus_register_type,  1, 'Bar', 0, 65535,   0,  10),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 2, 'Влажность',       'adam_humidity', 'ANALOG'::tag_data_type,  2, 'INPUT_REGISTER'::modbus_register_type,  1, '%',   0, 65535,   0, 100),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 3, 'Ток датчика',     'adam_current',  'ANALOG'::tag_data_type,  3, 'INPUT_REGISTER'::modbus_register_type,  1, 'mA',  0, 65535,   4,  20),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 4, 'Авария',          'adam_alarm',    'DIGITAL'::tag_data_type, 0, 'DISCRETE_INPUT'::modbus_register_type,  1, NULL,  0,     1,   0,   1);
