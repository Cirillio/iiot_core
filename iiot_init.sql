/**
 * @module IIoT_Database_Core
 */

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- 1. ENUMS
DO $$ BEGIN
    CREATE TYPE sensor_data_type AS ENUM ('ANALOG', 'DIGITAL', 'VIRTUAL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE modbus_register_type AS ENUM ('INPUT_REGISTER', 'HOLDING_REGISTER', 'DISCRETE_INPUT', 'COIL');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

-- 2. TABLES
CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    port INT DEFAULT 502,
    slave_id INT DEFAULT 1,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sensor_settings (
    sensor_id SERIAL PRIMARY KEY,
    device_id INT NOT NULL REFERENCES devices(id) ON DELETE RESTRICT,
    port_number INT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) UNIQUE,
    data_type sensor_data_type DEFAULT 'ANALOG',
    register_address SMALLINT NOT NULL DEFAULT 0,
    register_type modbus_register_type NOT NULL DEFAULT 'INPUT_REGISTER',
    register_count SMALLINT NOT NULL DEFAULT 1,
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

CREATE TABLE IF NOT EXISTS metrics (
    time TIMESTAMPTZ NOT NULL,
    sensor_id INT NOT NULL REFERENCES sensor_settings(sensor_id) ON DELETE CASCADE,
    raw_value DOUBLE PRECISION,
    value DOUBLE PRECISION NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_metrics_sensor_time ON metrics (sensor_id, time DESC);

-- 3. TIMESCALEDB
SELECT create_hypertable('metrics', 'time', if_not_exists => TRUE);

CREATE MATERIALIZED VIEW IF NOT EXISTS metrics_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time) AS time,
    sensor_id,
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
        'sensorId', NEW.sensor_id,
        'rawValue', NEW.raw_value,
        'value', NEW.value
    );
    PERFORM pg_notify('metrics_realtime', payload::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_metric AFTER INSERT ON metrics
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_metrics();

-- 5. SYSTEM CONFIG
CREATE TABLE IF NOT EXISTS system_config (
    id SERIAL PRIMARY KEY,
    raw_retention_days INT DEFAULT 90,
    agg_retention_days INT DEFAULT 1825,
    polling_interval_ms INT DEFAULT 5000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    ui_update_interval_ms INT DEFAULT 2000,
    deadband_threshold DOUBLE PRECISION DEFAULT 0.005,
    data_heartbeat_sec INT DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Trigger on system_config — must be after the table
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
TRUNCATE devices, sensor_settings, metrics RESTART IDENTITY CASCADE;

INSERT INTO system_config (raw_retention_days, agg_retention_days, polling_interval_ms, deadband_threshold)
VALUES (90, 1825, 5000, 0.001);

-- Device: ADAM-6017 (Advantech) — 8-channel analog input module
-- Simulator: Slave ID 1, port 5020
INSERT INTO devices (name, ip_address, port, slave_id, is_active)
VALUES ('ADAM-6017', 'sim', 5020, 1, true);

-- Sensors for ADAM-6017
-- All use Input Registers (FC04), 16-bit (register_count=1)
-- Simulator writes to IR addresses 0-3; DI address 0 for alarm
INSERT INTO sensor_settings
    (device_id, port_number, name, slug, data_type, register_address, register_type, register_count, unit, input_min, input_max, output_min, output_max)
VALUES
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 0, 'Температура',     'adam_temp',     'ANALOG'::sensor_data_type,  0, 'INPUT_REGISTER'::modbus_register_type,  1, '°C',  0, 65535, -50, 150),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 1, 'Входное давление','adam_press_in', 'ANALOG'::sensor_data_type,  1, 'INPUT_REGISTER'::modbus_register_type,  1, 'Bar', 0, 65535,   0,  10),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 2, 'Влажность',       'adam_humidity', 'ANALOG'::sensor_data_type,  2, 'INPUT_REGISTER'::modbus_register_type,  1, '%',   0, 65535,   0, 100),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 3, 'Ток датчика',     'adam_current',  'ANALOG'::sensor_data_type,  3, 'INPUT_REGISTER'::modbus_register_type,  1, 'mA',  0, 65535,   4,  20),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 4, 'Авария',          'adam_alarm',    'DIGITAL'::sensor_data_type, 0, 'DISCRETE_INPUT'::modbus_register_type,  1, NULL,  0,     1,   0,   1);
