using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

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

        public static string CreateDefaultConnectionString()
        {
            try
            {
                var cs = System.Configuration.ConfigurationManager
                    .ConnectionStrings["Weather"]?.ConnectionString;
                if (!string.IsNullOrWhiteSpace(cs)) return cs;
            }
            catch { }
            return "Host=localhost;Username=postgres;Password=123;Database=Weather";
        }

        public static DbRepository CreateDefault() =>
            new DbRepository(CreateDefaultConnectionString());

        public IEnumerable<Location> GetAllLocations()
        {
            var locations = new List<Location>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

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

                using (var cmd = new NpgsqlCommand("SELECT id, name, type, region_id FROM settlements", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var settlement = new Settlement(reader.GetString(1), reader.GetString(2));
                        settlement.Id = reader.GetInt32(0);
                        locations.Add(settlement);

                        if (!reader.IsDBNull(3))
                        {
                            int parentRegionId = reader.GetInt32(3);
                            var region = locations.OfType<Region>().FirstOrDefault(r => r.Id == parentRegionId);
                            region?.AddLocation(settlement);
                        }
                    }
                }
            }
            return locations;
        }

        public IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period)
        {
            var measurements = new List<WeatherMeasurement>();
            if (location == null || period == null) return measurements;

            bool isRegion = location is Region;
            string locationFilter = isRegion
                ? "ws.settlement_id IN (SELECT id FROM public.settlements WHERE region_id = @locId)"
                : "ws.settlement_id = @locId";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                string sql = $@"
                    SELECT 
                        m.timestamp,
                        MAX(CASE WHEN s.unit_id = 1 THEN m.value END) as temperature,
                        MAX(CASE WHEN s.unit_id = 2 THEN m.value END) as humidity,
                        MAX(CASE WHEN s.unit_id = 3 THEN m.value END) as pressure,
                        MAX(CASE WHEN s.unit_id = 4 THEN m.value END) as wind_speed,
                        MAX(wc.name) as condition
                    FROM public.measurements m
                    JOIN public.sensors s ON m.sensor_id = s.id
                    JOIN public.weather_stations ws ON ws.id = s.station_id
                    JOIN public.weather_conditions wc ON m.condition_id = wc.id
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
                                ReadDouble(reader, "temperature"),
                                ReadDouble(reader, "humidity"),
                                ReadDouble(reader, "pressure"),
                                ReadDouble(reader, "wind_speed"),
                                Convert.ToDateTime(reader["timestamp"]),
                                reader["condition"] == DBNull.Value ? null : Convert.ToString(reader["condition"])
                            ));
                        }
                    }
                }
            }
            return measurements;
        }

        public void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    int stationId = RequireStationId(conn, tx, settlement);
                    int conditionId = ResolveConditionId(conn, tx, WeatherConditionRules.Infer(measurement));

                    const string sql = @"
                    INSERT INTO public.measurements (sensor_id, condition_id, value, timestamp)
                    VALUES 
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 1), @conditionId, @temp, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 2), @conditionId, @hum, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 3), @conditionId, @press, @time),
                        ((SELECT id FROM public.sensors WHERE station_id = @stationId AND unit_id = 4), @conditionId, @wind, @time);";

                    using (var cmd = new NpgsqlCommand(sql, conn, tx))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("stationId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = stationId });
                        cmd.Parameters.AddWithValue("conditionId", conditionId);
                        cmd.Parameters.AddWithValue("temp", measurement.Temperature ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("hum", measurement.Humidity ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("press", measurement.Pressure ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("wind", measurement.WindSpeed ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("time", measurement.Timestamp);

                        int affected = cmd.ExecuteNonQuery();
                        if (affected != 4)
                            throw new InvalidOperationException("Не найдены все 4 датчика станции, запись отклонена.");
                    }

                    tx.Commit();
                }
            }
        }

        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"
            SELECT 
                s.type AS settlement_type,
                COALESCE(
                    (SELECT st.station_type FROM public.get_station_types_by_settlement(s.name) st LIMIT 1),
                    'Нет станции') AS station_type,
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

        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"
            SELECT
                (SELECT COUNT(*)::int FROM public.get_settlements_by_region(
                    (SELECT name FROM public.regions WHERE id = @regionId))) AS places,
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

        public void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (updated == null) throw new ArgumentNullException(nameof(updated));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    int stationId = RequireStationId(conn, tx, settlement);
                    int conditionId = ResolveConditionId(conn, tx, WeatherConditionRules.Infer(updated));

                    const string sql = @"
                    UPDATE public.measurements m SET value = @v, timestamp = @newTs, condition_id = @cid
                    FROM public.sensors s
                    WHERE m.sensor_id = s.id
                      AND s.station_id = @stationId
                      AND s.unit_id = @unit AND m.timestamp = @oldTs;";

                    int total = 0;
                    double?[] values = { updated.Temperature, updated.Humidity, updated.Pressure, updated.WindSpeed };
                    for (int unit = 1; unit <= 4; unit++)
                    {
                        using (var cmd = new NpgsqlCommand(sql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("stationId", stationId);
                            cmd.Parameters.AddWithValue("unit", unit);
                            cmd.Parameters.AddWithValue("oldTs", oldTimestamp);
                            cmd.Parameters.AddWithValue("newTs", updated.Timestamp);
                            cmd.Parameters.AddWithValue("v", values[unit - 1] ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("cid", conditionId);
                            total += cmd.ExecuteNonQuery();
                        }
                    }
                    if (total == 0)
                        throw new InvalidOperationException("Наблюдение не найдено.");
                    tx.Commit();
                }
            }
        }

        public void DeleteObservation(Settlement settlement, DateTime timestamp)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"
                    DELETE FROM public.measurements m
                    USING public.sensors s
                    WHERE m.sensor_id = s.id
                      AND s.station_id = (SELECT id FROM public.weather_stations WHERE settlement_id = @settlementId)
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
                    catch (PostgresException ex) when (ex.SqlState == "23505") { }
                }
            }
            return inserted;
        }

        public IReadOnlyList<string> GetWeatherConditionNames()
        {
            var names = new List<string>();
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT name FROM public.weather_conditions ORDER BY id", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) names.Add(reader.GetString(0));
                }
            }
            return names;
        }

        public IReadOnlyList<string> GetSettlementsByWeather(string conditionName)
        {
            var names = new List<string>();
            if (string.IsNullOrWhiteSpace(conditionName)) return names;

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT settlement_name FROM public.get_settlements_by_weather(@c)", conn))
                {
                    cmd.Parameters.AddWithValue("c", conditionName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) names.Add(reader.GetString(0));
                    }
                }
            }
            return names;
        }

        public IReadOnlyList<int> GetFavoriteSettlementIds(int userId)
        {
            var ids = new List<int>();
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(
                    "SELECT settlement_id FROM public.favorite_settlements WHERE user_id = @u", conn))
                {
                    cmd.Parameters.AddWithValue("u", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) ids.Add(reader.GetInt32(0));
                    }
                }
            }
            return ids;
        }

        public void SetFavorite(int userId, int settlementId, bool isFavorite)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                if (isFavorite)
                {
                    using (var cmd = new NpgsqlCommand(@"
                        INSERT INTO public.favorite_settlements (user_id, settlement_id)
                        VALUES (@u, @s)
                        ON CONFLICT (user_id, settlement_id) DO NOTHING", conn))
                    {
                        cmd.Parameters.AddWithValue("u", userId);
                        cmd.Parameters.AddWithValue("s", settlementId);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmd = new NpgsqlCommand(
                        "DELETE FROM public.favorite_settlements WHERE user_id = @u AND settlement_id = @s", conn))
                    {
                        cmd.Parameters.AddWithValue("u", userId);
                        cmd.Parameters.AddWithValue("s", settlementId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public Task<IEnumerable<Location>> GetAllLocationsAsync() =>
            Task.Run(() => GetAllLocations());
        public Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period) =>
            Task.Run(() => GetHistory(location, period));
        public Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement) =>
            Task.Run(() => SaveMeasurement(settlement, measurement));
        public Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            Task.Run(() => SeedTestData(observationsPerSettlement));

        private static int RequireStationId(NpgsqlConnection conn, NpgsqlTransaction tx, Settlement settlement)
        {
            using (var findCmd = new NpgsqlCommand(
                "SELECT id FROM public.weather_stations WHERE settlement_id = @settlementId", conn, tx))
            {
                findCmd.Parameters.Add(new NpgsqlParameter("settlementId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = settlement.Id });
                var obj = findCmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value)
                    throw new InvalidOperationException($"У поселения '{settlement.Name}' (id={settlement.Id}) нет метеостанции.");
                return Convert.ToInt32(obj);
            }
        }

        private static int ResolveConditionId(NpgsqlConnection conn, NpgsqlTransaction tx, string conditionName)
        {
            using (var cmd = new NpgsqlCommand(
                "SELECT id FROM public.weather_conditions WHERE name = @n", conn, tx))
            {
                cmd.Parameters.AddWithValue("n", conditionName ?? WeatherConditionRules.Clear);
                var obj = cmd.ExecuteScalar();
                if (obj == null || obj == DBNull.Value)
                    throw new InvalidOperationException("Неизвестное погодное условие: " + conditionName);
                return Convert.ToInt32(obj);
            }
        }

        private static double? ReadDouble(IDataRecord reader, string column)
        {
            int i = reader.GetOrdinal(column);
            return reader.IsDBNull(i) ? (double?)null : Convert.ToDouble(reader.GetValue(i));
        }
    }
}
