using System;
using System.Collections.Generic;
using System.Linq;

namespace tpkursa4
{
    public class MockRepository : IWeatherRepository
    {
        private readonly List<Location> _locations;
        private readonly HashSet<int> _favorites = new HashSet<int>();

        public MockRepository()
        {
            var novosibirsk = new Settlement("Новосибирск", "Город") { Id = 1 };
            var berdsk = new Settlement("Бердск", "Город") { Id = 2 };
            var nso = new Region("Новосибирская область") { Id = 3 };
            nso.AddLocation(novosibirsk);
            nso.AddLocation(berdsk);

            _locations = new List<Location> { nso, novosibirsk, berdsk };

            novosibirsk.AddMeasurement(new WeatherMeasurement(-15.0, 80, 750, DateTime.Now));
            berdsk.AddMeasurement(new WeatherMeasurement(-5.0, 60, 760, DateTime.Now));
        }

        public void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement)
        {
            var target = FindSettlement(settlement) ?? settlement;
            target.AddMeasurement(measurement);
        }

        public IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period)
        {
            if (location is Settlement settlement)
                return (FindSettlement(settlement) ?? settlement).Measurements.Where(m => period.Contains(m.Timestamp));

            if (location is Region region)
                return region.GetAllMeasurements().Where(m => period.Contains(m.Timestamp));

            return Enumerable.Empty<WeatherMeasurement>();
        }

        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId)
        {
            return ("Город (Тест)", "Автоматическая (Тест)", 4, "Ясно (Тест)");
        }

        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId)
        {
            return ("Область (Тест)", "Несколько станций (Тест)", 8, "Ясно (Тест)");
        }

        public void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated)
        {
            var target = FindSettlement(settlement) ?? settlement;
            if (!target.ReplaceMeasurement(oldTimestamp, updated))
                throw new InvalidOperationException("Наблюдение не найдено.");
        }

        public void DeleteObservation(Settlement settlement, DateTime timestamp)
        {
            var target = FindSettlement(settlement) ?? settlement;
            if (!target.RemoveMeasurement(timestamp))
                throw new InvalidOperationException("Наблюдение не найдено.");
        }

        public int SeedTestData(int observationsPerSettlement)
        {
            var rnd = new Random(42);
            int n = 0;
            foreach (var s in _locations.OfType<Settlement>())
            {
                for (int i = 0; i < observationsPerSettlement; i++)
                {
                    s.AddMeasurement(new WeatherMeasurement(
                        Math.Round(10 + rnd.NextDouble() * 8, 1),
                        Math.Round(58 + rnd.NextDouble() * 30, 1),
                        Math.Round(742 + rnd.NextDouble() * 18, 1),
                        Math.Round(1 + rnd.NextDouble() * 7, 1),
                        DateTime.Today.AddDays(-rnd.Next(0, 60)).AddHours(rnd.Next(0, 24))));
                    n++;
                }
            }
            return n;
        }

        public IReadOnlyList<string> GetWeatherConditionNames() =>
            new[] { WeatherConditionRules.Clear, WeatherConditionRules.Cloudy, WeatherConditionRules.Rain, WeatherConditionRules.Storm };

        public IReadOnlyList<string> GetSettlementsByWeather(string conditionName) =>
            _locations.OfType<Settlement>().Select(s => s.Name).ToList();

        public IReadOnlyList<int> GetFavoriteSettlementIds(int userId) => _favorites.ToList();

        public void SetFavorite(int userId, int settlementId, bool isFavorite)
        {
            if (isFavorite) _favorites.Add(settlementId);
            else _favorites.Remove(settlementId);
        }

        public System.Threading.Tasks.Task<IEnumerable<Location>> GetAllLocationsAsync() =>
            System.Threading.Tasks.Task.FromResult(GetAllLocations());
        public System.Threading.Tasks.Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period) =>
            System.Threading.Tasks.Task.FromResult(GetHistory(location, period));
        public System.Threading.Tasks.Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement) =>
            System.Threading.Tasks.Task.Run(() => SaveMeasurement(settlement, measurement));
        public System.Threading.Tasks.Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            System.Threading.Tasks.Task.FromResult(SeedTestData(observationsPerSettlement));

        public IEnumerable<Location> GetAllLocations() => _locations;

        private Settlement FindSettlement(Settlement settlement) =>
            _locations.OfType<Settlement>().FirstOrDefault(s => s.Id == settlement.Id);
    }
}
