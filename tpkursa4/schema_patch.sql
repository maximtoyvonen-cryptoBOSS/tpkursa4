-- Ограничения, пароли и проверка диапазонов значений датчиков.

CREATE UNIQUE INDEX IF NOT EXISTS weather_stations_settlement_uidx
    ON public.weather_stations (settlement_id);

CREATE UNIQUE INDEX IF NOT EXISTS sensors_station_unit_uidx
    ON public.sensors (station_id, unit_id);

ALTER TABLE public.sensors
    ALTER COLUMN unit_id SET NOT NULL;

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS password_hash character varying(64);

UPDATE public.users
SET password_hash = 'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3'
WHERE password_hash IS NULL OR password_hash = '';

ALTER TABLE public.users
    ALTER COLUMN password_hash SET NOT NULL;

CREATE OR REPLACE FUNCTION public.enforce_measurement_range()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    u integer;
BEGIN
    SELECT unit_id INTO u FROM public.sensors WHERE id = NEW.sensor_id;
    IF u IS NULL THEN
        RAISE EXCEPTION 'Датчик % не найден', NEW.sensor_id;
    END IF;
    IF u = 1 AND (NEW.value < -100 OR NEW.value > 100) THEN
        RAISE EXCEPTION 'Температура вне диапазона -100..100';
    ELSIF u = 2 AND (NEW.value < 0 OR NEW.value > 100) THEN
        RAISE EXCEPTION 'Влажность вне диапазона 0..100';
    ELSIF u = 3 AND (NEW.value < 600 OR NEW.value > 800) THEN
        RAISE EXCEPTION 'Давление вне диапазона 600..800';
    ELSIF u = 4 AND (NEW.value < 0 OR NEW.value > 60) THEN
        RAISE EXCEPTION 'Ветер вне диапазона 0..60';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_measurements_range ON public.measurements;
CREATE TRIGGER trg_measurements_range
BEFORE INSERT OR UPDATE ON public.measurements
FOR EACH ROW EXECUTE FUNCTION public.enforce_measurement_range();

WITH obs AS (
    SELECT ws.id AS station_id, m.timestamp,
           MAX(CASE WHEN s.unit_id = 2 THEN m.value END) AS hum,
           MAX(CASE WHEN s.unit_id = 4 THEN m.value END) AS wind
    FROM public.measurements m
    JOIN public.sensors s ON s.id = m.sensor_id
    JOIN public.weather_stations ws ON ws.id = s.station_id
    GROUP BY ws.id, m.timestamp
)
UPDATE public.measurements m
SET condition_id = CASE
    WHEN COALESCE(o.wind, 0) >= 15 AND COALESCE(o.hum, 0) >= 70 THEN 4
    WHEN COALESCE(o.hum, 0) >= 85 THEN 3
    WHEN COALESCE(o.hum, 0) >= 70 THEN 2
    ELSE 1
END
FROM obs o
JOIN public.sensors s ON s.id = m.sensor_id AND s.station_id = o.station_id
WHERE m.timestamp = o.timestamp;
