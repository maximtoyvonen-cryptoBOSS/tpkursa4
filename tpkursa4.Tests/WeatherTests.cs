using System;
using System.Linq;

namespace tpkursa4.Tests
{
    /// <summary>
    /// Автотесты предметной логики на MockRepository (без БД).
    /// Покрывают: средние по поселению/области, валидацию, CRUD, период.
    /// </summary>
    public static class WeatherTests
    {
        private static MockRepository NewRepo() => new MockRepository();

        [Test]
        public static void RegionAverage_IsMeanOfSettlements()
        {
            var repo = NewRepo();
            var period = new WeatherPeriod(new DateTime(2000, 1, 1), DateTime.Now.AddDays(1));
            var mgr = new WeatherManager(repo);
            var region = repo.GetAllLocations().OfType<Region>().First();
            var summary = mgr.GetStatistics(region, period);
            // Mock: -15 и -5 => среднее -10
            Assert.Equal(-10.0, Math.Round(summary.AverageTemperature, 1), "среднее области");
        }

        [Test]
        public static void Validator_RejectsBadHumidityAndWind()
        {
            var repo = NewRepo();
            var v = new Validator(repo);
            var loc = repo.GetAllLocations().OfType<Settlement>().First();
            Assert.True(!v.IsValid(new WeatherMeasurement(10, 150, 750, 3, DateTime.Now), loc), "влажность 150");
            Assert.True(!v.IsValid(new WeatherMeasurement(10, 60, 750, 99, DateTime.Now), loc), "ветер 99");
            Assert.True(v.IsValid(new WeatherMeasurement(10, 60, 750, 5, DateTime.Now), loc), "корректный замер");
        }

        [Test]
        public static void Validator_RejectsFutureTimestamp()
        {
            var repo = NewRepo();
            var v = new Validator(repo);
            var loc = repo.GetAllLocations().OfType<Settlement>().First();
            Assert.True(!v.IsValid(new WeatherMeasurement(10, 60, 750, 3, DateTime.Now.AddDays(2)), loc), "будущее");
        }

        [Test]
        public static void Crud_AddUpdateDelete()
        {
            var repo = NewRepo();
            var settlement = repo.GetAllLocations().OfType<Settlement>().First();
            var period = new WeatherPeriod(new DateTime(2000, 1, 1), DateTime.Now.AddDays(1));
            int before = repo.GetHistory(settlement, period).Count();

            var m = new WeatherMeasurement(12.5, 61, 751, 4, new DateTime(2026, 8, 15, 12, 0, 0));
            repo.SaveMeasurement(settlement, m);
            Assert.Equal(before + 1, repo.GetHistory(settlement, period).Count(), "create");

            var updated = new WeatherMeasurement(20.0, 61, 751, 4, new DateTime(2026, 8, 15, 12, 0, 0));
            repo.UpdateObservation(settlement, m.Timestamp, updated);
            Assert.Equal(20.0, repo.GetHistory(settlement, period).First(x => x.Timestamp == m.Timestamp).Temperature, "update");

            repo.DeleteObservation(settlement, m.Timestamp);
            Assert.Equal(before, repo.GetHistory(settlement, period).Count(), "delete");
        }

        [Test]
        public static void Period_FiltersHistory()
        {
            var repo = NewRepo();
            var settlement = repo.GetAllLocations().OfType<Settlement>().First();
            var narrow = new WeatherPeriod(new DateTime(2020, 1, 1), new DateTime(2020, 1, 2));
            Assert.Equal(0, repo.GetHistory(settlement, narrow).Count(), "пустой период");
        }

        [Test]
        public static void Seed_AddsData()
        {
            var repo = NewRepo();
            int added = repo.SeedTestData(3);
            Assert.True(added >= 6, "seed должен добавить замеры");
        }

        public static int Main()
        {
            return TestRunner.Run();
        }
    }
}
