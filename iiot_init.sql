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
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

-- 2. TABLES
CREATE TABLE IF NOT EXISTS devices (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    port INT DEFAULT 502,
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

-- 4. FUNCTIONS & TRIGGERS
CREATE OR REPLACE FUNCTION fn_trigger_notify_metrics() RETURNS TRIGGER AS $$
DECLARE
    payload JSON;
BEGIN
    payload = json_build_object(
        'Time', NEW.time,
        'SensorId', NEW.sensor_id,
        'RawValue', NEW.raw_value,
        'Value', NEW.value
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
    polling_interval_ms INT DEFAULT 1000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    deadband_threshold DOUBLE PRECISION DEFAULT 0.005, -- Снизил порог для чувствительности
    data_heartbeat_sec INT DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 6. SEED DATA
TRUNCATE devices, sensor_settings, metrics RESTART IDENTITY CASCADE;

INSERT INTO system_config (raw_retention_days, agg_retention_days, deadband_threshold)
VALUES (90, 1825, 0.001);

INSERT INTO devices (name, ip_address, port, is_active)
VALUES ('ADAM-6017 Unit', '127.0.0.1', 5020, true);

-- Sensor 1: Temperature (AI 7)
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max)
SELECT id, 7, 'Boiler Temperature', 'temp_main', 'ANALOG', '°C', 0, 65535, 0, 120
FROM devices WHERE name = 'ADAM-6017 Unit';

-- Sensor 2: Pressure (AI 6)
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max)
SELECT id, 6, 'System Pressure', 'press_main', 'ANALOG', 'Bar', 0, 65535, 0, 16
FROM devices WHERE name = 'ADAM-6017 Unit';

-- Sensor 3: Pump Status (DI 0)
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max)
SELECT id, 0, 'Main Pump Status', 'pump_state', 'DIGITAL', 'ON/OFF', 0, 1, 0, 1
FROM devices WHERE name = 'ADAM-6017 Unit';
