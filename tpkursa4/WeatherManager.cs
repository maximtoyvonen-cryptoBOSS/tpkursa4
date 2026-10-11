using System;
using System.Collections.Generic;

namespace tpkursa4
{
    /// <summary>
    /// Сервис управления погодой. Координирует процессы между слоями системы.
    /// </summary>
    public class WeatherManager
    {
        private readonly IWeatherRepository _repository;
        private readonly Validator _validator;
        private readonly AnalyticsEngine _analytics;

        public WeatherManager(IWeatherRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = new Validator(_repository);
            _analytics = new AnalyticsEngine();
        }

        public bool RecordWeather(Settlement settlement, WeatherMeasurement measurement)
        {
            if (!_validator.IsValid(measurement, settlement))
            {
                return false;
            }

            _repository.SaveMeasurement(settlement, measurement);
            return true;
        }

        /// <summary>
        /// Координация получения аналитических данных.
        /// Для области средние считаются по всем её поселениям (требование варианта).
        /// </summary>
        public WeatherSummary GetStatistics(Location location, WeatherPeriod period)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (period == null) throw new ArgumentNullException(nameof(period));

            var history = _repository.GetHistory(location, period);
            var summary = _analytics.Calculate(history);

            // Проверяем, что выбран именно город/деревня (класс Settlement)
            if (location is Settlement)
            {
                var extra = _repository.GetExtendedStationInfo(location.Id);
                summary.SettlementType = extra.settlementType;
                summary.StationType = extra.stationType;
                summary.ActiveSensorsCount = extra.sensorsCount;
                summary.LatestCondition = extra.latestCondition;
            }
            else if (location is Region)
            {
                var extra = _repository.GetRegionExtendedInfo(location.Id);
                summary.SettlementType = extra.settlementType;
                summary.StationType = extra.stationType;
                summary.ActiveSensorsCount = extra.sensorsCount;
                summary.LatestCondition = extra.latestCondition;
            }

            return summary;
        }

        // Этот метод возвращает плоский список для MainViewModel
        public IEnumerable<Location> GetLocations()
        {
            return _repository.GetAllLocations();
        }

        public System.Threading.Tasks.Task<IEnumerable<Location>> GetLocationsAsync() =>
            _repository.GetAllLocationsAsync();

        // CRUD через валидацию (валидация на уровне приложения, а не по исключениям БД).
        public bool UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated)
        {
            if (!_validator.IsValid(updated, settlement)) return false;
            _repository.UpdateObservation(settlement, oldTimestamp, updated);
            return true;
        }

        public void DeleteObservation(Settlement settlement, DateTime timestamp) =>
            _repository.DeleteObservation(settlement, timestamp);

        public int SeedTestData(int observationsPerSettlement) =>
            _repository.SeedTestData(observationsPerSettlement);

        public System.Threading.Tasks.Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            _repository.SeedTestDataAsync(observationsPerSettlement);
    }
}