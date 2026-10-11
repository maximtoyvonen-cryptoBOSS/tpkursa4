using System;
using System.Collections.Generic;
using System.Linq;

namespace tpkursa4
{
    public class MockRepository : IWeatherRepository
    {
        private readonly List<Location> _locations;

        public MockRepository()
        {
            // 1. Создаем города
            var novosibirsk = new Settlement("Новосибирск", "Город") { Id = 1 }; // Задали ID = 1
            var berdsk = new Settlement("Бердск", "Город") { Id = 2 };           // Задали ID = 2

            // 2. Создаем область
            var nso = new Region("Новосибирская область") { Id = 3 };            // Задали ID = 3
            nso.AddLocation(novosibirsk);
            nso.AddLocation(berdsk);

            _locations = new List<Location> { nso, novosibirsk, berdsk };

            // 3. Добавляем РАЗНЫЕ замеры
            // В Новосибирске -15
            novosibirsk.AddMeasurement(new WeatherMeasurement(-15.0, 80, 750, DateTime.Now));

            // В Бердске -5 (для контраста)
            berdsk.AddMeasurement(new WeatherMeasurement(-5.0, 60, 760, DateTime.Now));

            // Теперь:
            // Выберешь Новосибирск -> покажет -15
            // Выберешь Бердск -> покажет -5
            // Выберешь Область -> покажет -10 (среднее между -15 и -5)
        }

        public void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement)
        {
            settlement.AddMeasurement(measurement);
        }

        public IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period)
        {
            if (location is Settlement settlement)
                return settlement.Measurements.Where(m => period.Contains(m.Timestamp));

            if (location is Region region)
                return region.GetAllMeasurements().Where(m => period.Contains(m.Timestamp));

            return Enumerable.Empty<WeatherMeasurement>();
        }
        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId)
        {
            // Возвращаем заглушку, чтобы заглушить ошибку компилятора
            return ("Город (Тест)", "Автоматическая (Тест)", 4, "Ясно (Тест)");
        }

        public (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId)
        {
            return ("Область (Тест)", "Несколько станций (Тест)", 8, "Ясно (Тест)");
        }

        public void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated)
        {
            var target = _locations.OfType<Settlement>().FirstOrDefault(s => s.Id == settlement.Id) ?? settlement;
            if (!target.ReplaceMeasurement(oldTimestamp, updated))
                throw new InvalidOperationException("Наблюдение не найдено.");
        }

        public void DeleteObservation(Settlement settlement, DateTime timestamp)
        {
            var target = _locations.OfType<Settlement>().FirstOrDefault(s => s.Id == settlement.Id) ?? settlement;
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

        public System.Threading.Tasks.Task<IEnumerable<Location>> GetAllLocationsAsync() =>
            System.Threading.Tasks.Task.FromResult(GetAllLocations());
        public System.Threading.Tasks.Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period) =>
            System.Threading.Tasks.Task.FromResult(GetHistory(location, period));
        public System.Threading.Tasks.Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement) =>
            System.Threading.Tasks.Task.Run(() => SaveMeasurement(settlement, measurement));
        public System.Threading.Tasks.Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            System.Threading.Tasks.Task.FromResult(SeedTestData(observationsPerSettlement));

        public IEnumerable<Location> GetAllLocations() => _locations;
    }
}