CREATE TABLE IF NOT EXISTS meters (
    meter_id BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL PRIMARY KEY,
    meter_number BIGINT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS meter_readings (
    meter_id BIGINT NOT NULL REFERENCES meters(meter_id),
    value_at TIMESTAMPTZ NOT NULL,
    value NUMERIC NOT NULL,
    received_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (meter_id, value_at)
);
