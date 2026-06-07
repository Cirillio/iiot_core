/**
 * @module IIoT_Database_Core
 * Vendor-agnostic schema: modbus_connections + devices + tags + metrics.
 */

CREATE EXTENSION IF NOT EXISTS timescaledb;

-- 1. ENUMS
DO $$ BEGIN
    CREATE TYPE tag_data_type AS ENUM ('ANALOG_RAW', 'ANALOG_PHYSICAL', 'DIGITAL');
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
    CREATE TYPE modbus_raw_data_type AS ENUM ('INT16', 'UINT16', 'INT32', 'UINT32', 'FLOAT32', 'FLOAT64');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE system_service_status AS ENUM ('ONLINE', 'OFFLINE', 'DEGRADED', 'CRITICAL_ERROR', 'MAINTENANCE');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE command_status AS ENUM ('PENDING', 'PROCESSING', 'SUCCESS', 'FAILED');
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
    max_bit_span SMALLINT NOT NULL DEFAULT 2000,
    is_active BOOLEAN DEFAULT TRUE,           -- намерение оператора: опрашивать устройство или нет
    is_online BOOLEAN NOT NULL DEFAULT FALSE, -- рантайм: достучался ли коллектор сейчас (пишет коллектор)
    last_seen TIMESTAMPTZ,                    -- время последнего успешного контакта
    last_conn_error VARCHAR(500),             -- текст последней ошибки связи
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tags (
    tag_id SERIAL PRIMARY KEY,
    device_id INT NOT NULL REFERENCES devices(id) ON DELETE RESTRICT,
    port_number INT,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) UNIQUE,
    data_type tag_data_type DEFAULT 'ANALOG_RAW',
    register_address INTEGER NOT NULL DEFAULT 0,
    register_type modbus_register_type NOT NULL DEFAULT 'INPUT_REGISTER',
    register_count SMALLINT NOT NULL DEFAULT 1,
    raw_data_type modbus_raw_data_type NOT NULL DEFAULT 'UINT16',
    endianness modbus_endianness NOT NULL DEFAULT 'BIG_ENDIAN',
    unit VARCHAR(20),
    input_min DOUBLE PRECISION DEFAULT 0,
    input_max DOUBLE PRECISION DEFAULT 65535,
    output_min DOUBLE PRECISION DEFAULT 0,
    output_max DOUBLE PRECISION DEFAULT 100,
    offset_val DOUBLE PRECISION DEFAULT 0,
    deadband_threshold DOUBLE PRECISION,
    ui_config JSONB DEFAULT '{}',
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_tag_device_port UNIQUE (device_id, port_number)
);

-- Идемпотентные миграции для ранее созданных БД (на свежей БД колонки уже в CREATE TABLE выше).
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'tags' AND column_name = 'raw_data_type'
    ) THEN
        ALTER TABLE tags ADD COLUMN raw_data_type modbus_raw_data_type NOT NULL DEFAULT 'UINT16';
        -- Бэкафилл по прежней неявной логике (ширина в регистрах → бинарный тип).
        UPDATE tags SET raw_data_type = CASE
            WHEN register_count = 2 THEN 'FLOAT32'::modbus_raw_data_type
            WHEN register_count = 4 THEN 'FLOAT64'::modbus_raw_data_type
            ELSE 'UINT16'::modbus_raw_data_type
        END;
    END IF;
END $$;

-- Удаление наследия виртуальных тегов: фича вырезана, колонка формул больше не нужна.
ALTER TABLE tags DROP COLUMN IF EXISTS formula;

ALTER TABLE devices ADD COLUMN IF NOT EXISTS max_bit_span SMALLINT NOT NULL DEFAULT 2000;

-- Рантайм-статус доступности (отделён от is_active — намерения оператора).
ALTER TABLE devices ADD COLUMN IF NOT EXISTS is_online BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_seen TIMESTAMPTZ;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_conn_error VARCHAR(500);

CREATE TABLE IF NOT EXISTS metrics (
    time TIMESTAMPTZ NOT NULL,
    tag_id INT NOT NULL REFERENCES tags(tag_id) ON DELETE CASCADE,
    raw_value DOUBLE PRECISION,
    value DOUBLE PRECISION NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_metrics_tag_time ON metrics (tag_id, time DESC);

-- Таблица команд управления (Supervisory Control).
-- Единица аудита действий операторов и надёжной асинхронной доставки команд в коллектор.
CREATE TABLE IF NOT EXISTS device_commands (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tag_id INT NOT NULL REFERENCES tags(tag_id) ON DELETE CASCADE,
    value DOUBLE PRECISION NOT NULL,
    operator_id VARCHAR(100) NOT NULL,
    status command_status NOT NULL DEFAULT 'PENDING',
    error_message VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_commands_status ON device_commands (status, created_at);

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

-- Новая команда (Pending) → мгновенный пинг коллектору. Канал несёт только UUID,
-- коллектор сам вычитывает строку из БД. Заменяет polling.
CREATE OR REPLACE FUNCTION fn_trigger_notify_new_command() RETURNS TRIGGER AS $$
BEGIN
    PERFORM pg_notify('control_commands', NEW.id::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_new_command AFTER INSERT ON device_commands
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_new_command();

-- Изменение статуса команды → уведомление WebApi для трансляции в канал диспетчеризации (SignalR).
-- Авто-обновление updated_at и pg_notify в одном BEFORE-триггере.
CREATE OR REPLACE FUNCTION fn_trigger_notify_command_status() RETURNS TRIGGER AS $$
DECLARE
    payload JSON;
BEGIN
    NEW.updated_at = NOW();
    payload = json_build_object(
        'commandId', NEW.id,
        'status', NEW.status,
        'errorMessage', NEW.error_message
    );
    PERFORM pg_notify('command_status_changed', payload::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_notify_command_status BEFORE UPDATE OF status ON device_commands
FOR EACH ROW EXECUTE FUNCTION fn_trigger_notify_command_status();

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

-- Применение политик хранения TimescaleDB по значениям из system_config.
-- Срабатывает при seed-INSERT и при каждом сохранении настроек из UI (PUT /api/system/config),
-- поэтому период хранения меняется на лету — без перезапуска и ручного SQL.
-- remove+add, а не правка «на месте»: TimescaleDB не умеет менять интервал существующей политики.
-- Сырьё (metrics) дропается раньше, часовые агрегаты (metrics_hourly) живут дольше.
CREATE OR REPLACE FUNCTION fn_apply_retention_policies() RETURNS TRIGGER AS $$
BEGIN
    PERFORM remove_retention_policy('metrics', if_exists => true);
    PERFORM add_retention_policy('metrics',
        drop_after => make_interval(days => COALESCE(NEW.raw_retention_days, 90)));

    PERFORM remove_retention_policy('metrics_hourly', if_exists => true);
    PERFORM add_retention_policy('metrics_hourly',
        drop_after => make_interval(days => COALESCE(NEW.agg_retention_days, 1825)));

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE TRIGGER trg_apply_retention
AFTER INSERT OR UPDATE OF raw_retention_days, agg_retention_days ON system_config
FOR EACH ROW EXECUTE FUNCTION fn_apply_retention_policies();

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
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 0, 'Температура',     'adam_temp',     'ANALOG_RAW'::tag_data_type,  0, 'INPUT_REGISTER'::modbus_register_type,  1, '°C',  0, 65535, -50, 150),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 1, 'Входное давление','adam_press_in', 'ANALOG_RAW'::tag_data_type,  1, 'INPUT_REGISTER'::modbus_register_type,  1, 'Bar', 0, 65535,   0,  10),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 2, 'Влажность',       'adam_humidity', 'ANALOG_RAW'::tag_data_type,  2, 'INPUT_REGISTER'::modbus_register_type,  1, '%',   0, 65535,   0, 100),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 3, 'Ток датчика',     'adam_current',  'ANALOG_RAW'::tag_data_type,  3, 'INPUT_REGISTER'::modbus_register_type,  1, 'mA',  0, 65535,   4,  20),
    ((SELECT id FROM devices WHERE name = 'ADAM-6017'), 4, 'Авария',          'adam_alarm',    'DIGITAL'::tag_data_type,     0, 'DISCRETE_INPUT'::modbus_register_type,  1, NULL,  0,     1,   0,   1);

-- Device: Siemens S7-1200 — привод асинхронного двигателя (slave 2 на том же шлюзе sim:5020).
INSERT INTO devices (name, connection_id, slave_id, use_group_polling, max_register_span, is_active)
VALUES ('Siemens S7-1200',
        (SELECT id FROM modbus_connections WHERE ip_address = 'sim' AND port = 5020),
        2, true, 120, true);

-- Теги Siemens. Измерения процесса — Input Registers (RO, FC04, FLOAT32 ABCD).
-- Управление — Holding/Coil (RW): уставка оборотов и команда пуска.
INSERT INTO tags
    (device_id, port_number, name, slug, data_type, register_address, register_type, register_count, raw_data_type, endianness, unit, input_min, input_max, output_min, output_max)
VALUES
    -- Read-only измерения
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 0, 'Обороты двигателя', 's7_rpm',      'ANALOG_PHYSICAL'::tag_data_type, 0, 'INPUT_REGISTER'::modbus_register_type,   2, 'FLOAT32'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, 'rpm', 0, 0, 0, 0),
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 1, 'Температура мотора','s7_temp',     'ANALOG_PHYSICAL'::tag_data_type, 2, 'INPUT_REGISTER'::modbus_register_type,   2, 'FLOAT32'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, '°C',  0, 0, 0, 0),
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 2, 'Мощность',         's7_power',     'ANALOG_PHYSICAL'::tag_data_type, 4, 'INPUT_REGISTER'::modbus_register_type,   2, 'FLOAT32'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, 'kW',  0, 0, 0, 0),
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 3, 'Вращается',        's7_running',   'DIGITAL'::tag_data_type,         0, 'DISCRETE_INPUT'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type,  'BIG_ENDIAN'::modbus_endianness, NULL, 0, 1, 0, 1),
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 4, 'Перегрев',         's7_overheat',  'DIGITAL'::tag_data_type,         1, 'DISCRETE_INPUT'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type,  'BIG_ENDIAN'::modbus_endianness, NULL, 0, 1, 0, 1),
    -- Read/Write управление
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 5, 'Уставка оборотов', 's7_rpm_sp',    'ANALOG_PHYSICAL'::tag_data_type, 0, 'HOLDING_REGISTER'::modbus_register_type, 2, 'FLOAT32'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, 'rpm', 0, 0, 0, 0),
    ((SELECT id FROM devices WHERE name = 'Siemens S7-1200'), 6, 'Пуск двигателя',   's7_run_cmd',   'DIGITAL'::tag_data_type,         0, 'COIL'::modbus_register_type,             1, 'UINT16'::modbus_raw_data_type,  'BIG_ENDIAN'::modbus_endianness, NULL, 0, 1, 0, 1);

-- Device: Schneider M221 — насосная станция / резервуар (slave 3).
INSERT INTO devices (name, connection_id, slave_id, use_group_polling, max_register_span, is_active)
VALUES ('Schneider M221',
        (SELECT id FROM modbus_connections WHERE ip_address = 'sim' AND port = 5020),
        3, true, 120, true);

-- Теги Schneider. Давление/уровень — Input Registers (RO, 16-bit, линейное масштабирование).
-- Управление — реле насоса (Coil) и уставка давления (Holding).
INSERT INTO tags
    (device_id, port_number, name, slug, data_type, register_address, register_type, register_count, raw_data_type, endianness, unit, input_min, input_max, output_min, output_max)
VALUES
    -- Read-only измерения
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 0, 'Давление в баке',  'm221_press',    'ANALOG_RAW'::tag_data_type, 0, 'INPUT_REGISTER'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, 'Bar', 0, 65535, 0,  10),
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 1, 'Уровень в баке',   'm221_level',    'ANALOG_RAW'::tag_data_type, 1, 'INPUT_REGISTER'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, '%',   0, 65535, 0, 100),
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 2, 'Давление достигнуто','m221_press_ok','DIGITAL'::tag_data_type,   0, 'DISCRETE_INPUT'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, NULL,  0, 1, 0, 1),
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 3, 'Низкий уровень',   'm221_low_lvl',  'DIGITAL'::tag_data_type,    1, 'DISCRETE_INPUT'::modbus_register_type,   1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, NULL,  0, 1, 0, 1),
    -- Read/Write управление
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 4, 'Реле насоса',      'm221_pump',     'DIGITAL'::tag_data_type,    0, 'COIL'::modbus_register_type,             1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, NULL,  0, 1, 0, 1),
    ((SELECT id FROM devices WHERE name = 'Schneider M221'), 5, 'Уставка давления', 'm221_press_sp', 'ANALOG_RAW'::tag_data_type, 0, 'HOLDING_REGISTER'::modbus_register_type, 1, 'UINT16'::modbus_raw_data_type, 'BIG_ENDIAN'::modbus_endianness, 'Bar', 0, 65535, 0,  10);
