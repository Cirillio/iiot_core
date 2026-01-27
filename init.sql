CREATE TABLE IF NOT EXISTS sensor_settings (
    port_id INT PRIMARY KEY,
    sensor_name VARCHAR(100),
    unit VARCHAR(20),
    input_min DOUBLE PRECISION,
    input_max DOUBLE PRECISION,
    output_min DOUBLE PRECISION,
    output_max DOUBLE PRECISION,
    offset_val DOUBLE PRECISION,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS raw_data_logs (
    id SERIAL PRIMARY KEY,
    port_id INT,
    raw_value DOUBLE PRECISION,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS digital_data_logs (
    id SERIAL PRIMARY KEY,
    port_id INT,
    value BOOLEAN,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS system_status_logs (
    id SERIAL PRIMARY KEY,
    is_connected BOOLEAN,
    last_error TEXT,
    last_sync TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
