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

-- Create Materialized View for Hourly Aggregates (Analytics)
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

-- Add Refresh Policy for metrics_hourly (Continuous Aggregate)
SELECT add_continuous_aggregate_policy('metrics_hourly',
    start_offset => INTERVAL '1 month',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE);

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
    polling_interval_ms INT DEFAULT 5000,
    config_reload_interval_sec INT DEFAULT 60,
    health_check_interval_sec INT DEFAULT 30,
    ui_update_interval_ms INT DEFAULT 2000,
    deadband_threshold DOUBLE PRECISION DEFAULT 0.005,
    data_heartbeat_sec INT DEFAULT 600,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Notification function for config change
CREATE OR REPLACE FUNCTION fn_trigger_notify_config_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('config_changed', 'reload');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_config_change AFTER UPDATE OR INSERT ON system_config
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_config_change();

-- 6. SYSTEM STATUS (Health Check)
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

-- 1. Добавляем контроллеры (Девайсы) на один IP симулятора, но с разными Slave ID
INSERT INTO devices (name, ip_address, port, slave_id, is_active) VALUES
('Pump Station Alpha', 'sim', 5020, 1, true),
('Chiller Unit A',     'sim', 5020, 2, true),
('HVAC Main',          'sim', 5020, 3, true);

-- 2. Датчики для "Pump Station Alpha"
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max, ui_config)
SELECT id, 1, 'Входное давление', 'pump_press_in', 'ANALOG'::sensor_data_type, 'Bar', 0, 65535, 0, 10, 
    '{"color": "#38bdf8", "mainPagePosition": 1, "minCritical": 0.5, "minWarning": 1.5, "maxWarning": 8.5, "maxCritical": 9.5}'::jsonb
FROM devices WHERE name = 'Pump Station Alpha' UNION ALL
SELECT id, 2, 'Выходное давление', 'pump_press_out', 'ANALOG'::sensor_data_type, 'Bar', 0, 65535, 0, 16, 
    '{"color": "#0ea5e9", "mainPagePosition": 2, "maxWarning": 14.0, "maxCritical": 15.5}'::jsonb
FROM devices WHERE name = 'Pump Station Alpha' UNION ALL
SELECT id, 3, 'Температура насоса', 'pump_temp', 'ANALOG'::sensor_data_type, '°C', 0, 65535, -20, 150, 
    '{"color": "#f97316", "mainPagePosition": 3, "maxWarning": 85.0, "maxCritical": 110.0}'::jsonb
FROM devices WHERE name = 'Pump Station Alpha' UNION ALL
SELECT id, 0, 'Статус Насоса', 'pump_status', 'DIGITAL'::sensor_data_type, 'ON/OFF', 0, 1, 0, 1, 
    '{"color": "#10b981", "mainPagePosition": 4}'::jsonb
FROM devices WHERE name = 'Pump Station Alpha';

-- 3. Датчики для "Chiller Unit A"
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max, ui_config)
SELECT id, 3, 'Температура хладагента', 'chil_temp_in', 'ANALOG'::sensor_data_type, '°C', 0, 65535, -50, 50, 
    '{"color": "#6366f1", "mainPagePosition": 1, "minCritical": -45.0, "maxCritical": 45.0}'::jsonb
FROM devices WHERE name = 'Chiller Unit A' UNION ALL
SELECT id, 1, 'Давление (High)', 'chil_press_hi', 'ANALOG'::sensor_data_type, 'Bar', 0, 65535, 0, 30, 
    '{"color": "#ef4444", "mainPagePosition": 2, "maxCritical": 28.0}'::jsonb
FROM devices WHERE name = 'Chiller Unit A' UNION ALL
SELECT id, 0, 'Компрессор', 'chil_comp_status', 'DIGITAL'::sensor_data_type, 'ON/OFF', 0, 1, 0, 1, 
    '{"color": "#10b981", "mainPagePosition": 3}'::jsonb
FROM devices WHERE name = 'Chiller Unit A';

-- 4. Датчики для "HVAC Main"
INSERT INTO sensor_settings (device_id, port_number, name, slug, data_type, unit, input_min, input_max, output_min, output_max, ui_config)
SELECT id, 3, 'Уровень CO2', 'hvac_co2', 'ANALOG'::sensor_data_type, 'ppm', 0, 65535, 400, 2000, 
    '{"color": "#a855f7", "mainPagePosition": 1, "maxWarning": 800.0, "maxCritical": 1200.0}'::jsonb
FROM devices WHERE name = 'HVAC Main' UNION ALL
SELECT id, 1, 'Температура притока', 'hvac_temp_sup', 'ANALOG'::sensor_data_type, '°C', 0, 65535, -40, 60, 
    '{"color": "#fbbf24", "mainPagePosition": 2}'::jsonb
FROM devices WHERE name = 'HVAC Main' UNION ALL
SELECT id, 4, 'Загрязнение фильтра', 'hvac_filter_err', 'DIGITAL'::sensor_data_type, 'OK/ERR', 0, 1, 0, 1, 
    '{"color": "#f43f5e", "mainPagePosition": 3}'::jsonb
FROM devices WHERE name = 'HVAC Main';



