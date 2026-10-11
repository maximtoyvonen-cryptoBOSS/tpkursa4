using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tpkursa4
{
    public class DbRepository : IWeatherRepository
    {
        private readonly string _connectionString;

        public DbRepository(string host, string user, string password, string database)
        {
            _connectionString = $"Host={host};Username={user};Password={password};Database={database}";
        }

        public DbRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Строка подключения: сначала App.config, иначе значения по умолчанию для курсовой.
        /// </summary>
        public static string CreateDefaultConnectionString()
        {
            try
            {
                var cs = System.Configuration.ConfigurationManager
                    .ConnectionStrings["Weather"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(cs)) return cs;
            }
            catch { /* нет System.Configuration — используем дефолт */ }
            return "Host=localhost;Username=postgres;Password=123;Database=Weather";
        }

        public static DbRepository CreateDefault() =>
            new DbRepository(CreateDefaultConnectionString());

        // Загрузка локаций (осталась без изменений, типы int скорректированы)
        public IEnumerable<Location> GetAllLocations()
        {
            var locations = new List<Location>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Загружаем регионы
                using (var cmd = new NpgsqlCommand("SELECT id, name FROM regions", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var region = new Region(reader.GetString(1));
                        region.Id = reader.GetInt32(0);
                        locations.Add(region);
                    }
                }

                // 2. Загружаем города
                string settlementSql = "SELECT id, name, type, region_id FROM settlements";

                using (var cmd = new NpgsqlCommand(settlementSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var settlement = new Settlement(reader.GetString(1), reader.GetString(2));
                        settlement.Id = reader.GetInt32(0);

                        locations.Add(settlement);

                        // 3. Связываем город с его регионом
                        if (!reader.IsDBNull(3))
                        {
                            int parentRegionId = reader.GetInt32(3);
                            var region = locations.OfType<Region>().FirstOrDefault(r => r.Id == parentRegionId);
                            if (region != null)
                            {
                                region.AddLocation(settlement);
                            }
                        }
                    }
                }
            }
            return locations;
        }

        // 1. ЧТЕНИЕ: сводные замеры из 4 датчиков (unit_id 1-4) с одной меткой времени.
        // Для Region берём все поселения области, для Settlement — одно поселение.
        // Фильтр по периоду выполняется в SQL, а не в памяти.
        public IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period)
        {
            var measurements = new List<WeatherMeasurement>();
            if (location == null || period == null) return measurements;

            bool isRegion = location is Region;

            // Для области считаем по всем её поселениям (требование курсовой),
            // группируем по (timestamp, settlement), чтобы замеры разных станций
            // с одинаковым временем не склеивались через MAX.
            string locationFilter = isRegion
                ? "ws.settlement_id IN (SELECT id FROM public.settlements WHERE region_id = @locId)"
                : "ws.settlement_id = @locId";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // Теперь мы джойним таблицу датчиков (s) и смотрим на s.unit_id:
                // 1 - Температура, 2 - Влажность, 3 - Давление, 4 - Скорость ветра
                string sql = $@"
                    SELECT 
                        m.timestamp,
                        COALESCE(MAX(CASE WHEN s.unit_id = 1 THEN m.value END), 0) as temperature,
                        COALESCE(MAX(CASE WHEN s.unit_id = 2 THEN m.value END), 0) as humidity,
                        COALESCE(MAX(CASE WHEN s.unit_id = 3 THEN m.value END), 0) as pressure,
                        COALESCE(MAX(CASE WHEN s.unit_id = 4 THEN m.value END), 0) as wind_speed
                    FROM public.measurements m
                    JOIN public.sensors s ON m.sensor_id = s.id
                    JOIN public.weather_stations ws ON ws.id = s.station_id
                    WHERE {locationFilter}
                      AND m.timestamp BETWEEN @start AND @end
                    GROUP BY m.timestamp, ws.settlement_id
                    ORDER BY m.timestamp";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("locId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = location.Id });
                    cmd.Parameters.AddWithValue("start", period.Start);
                    cmd.Parameters.AddWithValue("end", period.End);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            measurements.Add(new WeatherMeasurement(
                                Convert.ToDouble(reader["temperature"]),
                                Convert.ToDouble(reader["humidity"]),
                                Convert.ToDouble(reader["pressure"]),
                                Convert.ToDouble(reader["wind_speed"]),
                                Convert.ToDateTime(reader["timestamp"])
                            ));
                        }
                    }
                }
            }
            return measurements;
        }

        // 2. ЗАПИСЬ: один замер = 4 строки (по датчикам unit 1-4) с одной меткой времени.
        // Всё в одной транзакции: либо все 4 строки, либо ничего.
        public void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    // Один раз находим станцию поселения, дальше работаем по её id.
                    int? stationId;
                    using (var findCmd = new NpgsqlCommand(
                        "SELECT id FROM public.weather_stations WHERE settlement_id = @settlementId LIMIT 1", conn, tx))
                    {
                        findCmd.Parameters.Add(new NpgsqlParameter("settlementId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = settlement.Id });
                        var obj = findCmd.ExecuteScalar();
                        stationId = (obj == null || obj == DBNull.Value) ? (int?)null : Convert.ToInt32(obj);
                    }

                    if (stationId == null)
                        throw new InvalidOperationException($"У поселения '{settlement.Name}' (id={settlement.Id}) нет метеостанции.");

                    // Ищем ID нужного датчика через подзапрос, фильтруя по станции города и unit_id датчика.
                    // В поле condition_id передаем @conditionId (по умолчанию 1 - 'Ясно').
                    string sql = @"
                    INSERT INTO public.measurements (sensor_id, condition_id, value, timestamp)
                    VALUES 
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 1), @conditionId, @temp, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 2), @conditionId, @hum, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 3), @conditionId, @press, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 4), @conditionId, @wind, @time);";

                    using (var cmd = new NpgsqlCommand(sql, conn, tx))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("stationId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = stationId.Value });

                        // Передаем ID погодного условия. Сейчас зашито 1 (Ясно).
                        cmd.Parameters.AddWithValue("conditionId", 1);

                        cmd.Parameters.AddWithValue("temp", measurement.Temperature);
                        cmd.Parameters.AddWithValue("hum", measurement.Humidity);
                        cmd.Parameters.AddWithValue("press", measurement.Pressure);
                        cmd.Parameters.AddWithValue("wind", measurement.WindSpeed);
                        cmd.Parameters.AddWithValue("time", measurement.Timestamp);

                        int affected = cmd.ExecuteNonQuery();
                        if (affected != 4)
                            throw new InvalidOperationException("Не найдены все 4 датчика станции, запись отклонена.");
                    }

                    tx.Commit();
                }
            }
        }
        // Вставь этот метод внутрь класса DbRepository
        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            SELECT 
                s.type AS settlement_type,
                COALESCE(ws.type, 'Нет станции') AS station_type,
                (SELECT COUNT(*)::int FROM public.sensors WHERE station_id = ws.id) AS sensors_count,
                COALESCE(
                    (SELECT wc.name 
                     FROM public.measurements m
                     JOIN public.weather_conditions wc ON m.condition_id = wc.id
                     JOIN public.sensors sn ON m.sensor_id = sn.id
                     WHERE sn.station_id = ws.id
                     ORDER BY m.timestamp DESC LIMIT 1
                    ), 'Нет данных'
                ) AS latest_condition
            FROM public.settlements s
            LEFT JOIN public.weather_stations ws ON s.id = ws.settlement_id
            WHERE s.id = @settlementId;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("settlementId", settlementId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return (
                                reader.IsDBNull(0) ? "Неизвестно" : reader.GetString(0),
                                reader.IsDBNull(1) ? "Нет станции" : reader.GetString(1),
                                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                reader.IsDBNull(3) ? "Нет данных" : reader.GetString(3)
                            );
                        }
                    }
                }
            }
            return ("Неизвестно", "Нет станции", 0, "Нет данных");
        }

        // Сводка для области: агрегируем по всем её поселениям.
        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
            SELECT
                (SELECT COUNT(*)::int FROM public.settlements WHERE region_id = @regionId) AS places,
                (SELECT COUNT(*)::int FROM public.sensors sn
                  JOIN public.weather_stations ws ON ws.id = sn.station_id
                  JOIN public.settlements s ON s.id = ws.settlement_id
                 WHERE s.region_id = @regionId) AS sensors_count,
                COALESCE(
                    (SELECT wc.name
                     FROM public.measurements m
                     JOIN public.sensors sn ON m.sensor_id = sn.id
                     JOIN public.weather_stations ws ON ws.id = sn.station_id
                     JOIN public.settlements s ON s.id = ws.settlement_id
                     JOIN public.weather_conditions wc ON m.condition_id = wc.id
                     WHERE s.region_id = @regionId
                     ORDER BY m.timestamp DESC LIMIT 1),
                    'Нет данных') AS latest_condition;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("regionId", regionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int places = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            int sensors = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            string latest = reader.IsDBNull(2) ? "Нет данных" : reader.GetString(2);
                            return ($"Область ({places} п.)", "Несколько станций", sensors, latest);
                        }
                    }
                }
            }
            return ("Область", "Несколько станций", 0, "Нет данных");
        }

        // CRUD Update: обновляем 4 строки одного наблюдения (один timestamp -> новый замер).
        public void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (updated == null) throw new ArgumentNullException(nameof(updated));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    string sql = @"
                    UPDATE public.measurements m SET value = @v, timestamp = @newTs
                    FROM public.sensors s
                    WHERE m.sensor_id = s.id
                      AND s.station_id = (SELECT id FROM public.weather_stations WHERE settlement_id = @settlementId LIMIT 1)
                      AND s.unit_id = @unit AND m.timestamp = @oldTs;";

                    int total = 0;
                    double[] values = { updated.Temperature, updated.Humidity, updated.Pressure, updated.WindSpeed };
                    for (int unit = 1; unit <= 4; unit++)
                    {
                        using (var cmd = new NpgsqlCommand(sql, conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("settlementId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = settlement.Id });
                            cmd.Parameters.AddWithValue("unit", unit);
                            cmd.Parameters.AddWithValue("oldTs", oldTimestamp);
                            cmd.Parameters.AddWithValue("newTs", updated.Timestamp);
                            cmd.Parameters.AddWithValue("v", values[unit - 1]);
                            total += cmd.ExecuteNonQuery();
                        }
                    }
                    if (total == 0)
                        throw new InvalidOperationException("Наблюдение не найдено.");
                    tx.Commit();
                }
            }
        }

        // CRUD Delete: удаляем 4 строки одного наблюдения.
        public void DeleteObservation(Settlement settlement, DateTime timestamp)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    DELETE FROM public.measurements m
                    USING public.sensors s
                    WHERE m.sensor_id = s.id
                      AND s.station_id = (SELECT id FROM public.weather_stations WHERE settlement_id = @settlementId LIMIT 1)
                      AND m.timestamp = @ts;";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("settlementId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = settlement.Id });
                    cmd.Parameters.AddWithValue("ts", timestamp);
                    int affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                        throw new InvalidOperationException("Наблюдение не найдено.");
                }
            }
        }

        // Автозаполнение БД тестовыми различимыми данными (по поселениям разные базы температур).
        public int SeedTestData(int observationsPerSettlement)
        {
            if (observationsPerSettlement <= 0) throw new ArgumentOutOfRangeException(nameof(observationsPerSettlement));
            var rnd = new Random(42);
            int inserted = 0;

            List<int> settlementIds;
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT id FROM public.settlements ORDER BY id", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    settlementIds = new List<int>();
                    while (reader.Read()) settlementIds.Add(reader.GetInt32(0));
                }
            }

            foreach (int sid in settlementIds)
            {
                double baseTemp = sid == 3 ? 10.0 : sid == 2 ? 16.0 : sid == 1 ? 14.0 : 15.0;
                var tmp = new Settlement("seed") { Id = sid };
                for (int i = 0; i < observationsPerSettlement; i++)
                {
                    var ts = DateTime.Today.AddDays(-rnd.Next(0, 60))
                        .AddHours(rnd.Next(0, 24)).AddMinutes(rnd.Next(0, 60)).AddSeconds(rnd.Next(0, 60));
                    var m = new WeatherMeasurement(
                        Math.Round(baseTemp + (rnd.NextDouble() * 6 - 3), 1),
                        Math.Round(58 + rnd.NextDouble() * 30, 1),
                        Math.Round(742 + rnd.NextDouble() * 18, 1),
                        Math.Round(1 + rnd.NextDouble() * 7, 1),
                        ts);
                    try { SaveMeasurement(tmp, m); inserted += 4; }
                    catch (PostgresException ex) when (ex.SqlState == "23505") { /* коллизия timestamp — пропускаем */ }
                }
            }
            return inserted;
        }

        // Асинхронные обёртки: выносят блокирующий доступ к БД в фоновый поток.
        public System.Threading.Tasks.Task<IEnumerable<Location>> GetAllLocationsAsync() =>
            System.Threading.Tasks.Task.Run(() => GetAllLocations());
        public System.Threading.Tasks.Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period) =>
            System.Threading.Tasks.Task.Run(() => GetHistory(location, period));
        public System.Threading.Tasks.Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement) =>
            System.Threading.Tasks.Task.Run(() => SaveMeasurement(settlement, measurement));
        public System.Threading.Tasks.Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            System.Threading.Tasks.Task.Run(() => SeedTestData(observationsPerSettlement));
    }
}