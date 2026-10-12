using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tpkursa4
{
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

        public bool TryRecordWeather(Settlement settlement, WeatherMeasurement measurement, out string error)
        {
            if (!_validator.TryValidate(measurement, settlement, out error))
                return false;

            _repository.SaveMeasurement(settlement, measurement);
            return true;
        }

        public bool RecordWeather(Settlement settlement, WeatherMeasurement measurement) =>
            TryRecordWeather(settlement, measurement, out _);

        public WeatherSummary GetStatistics(Location location, WeatherPeriod period)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (period == null) throw new ArgumentNullException(nameof(period));

            var history = _repository.GetHistory(location, period);
            var summary = _analytics.GetFullSummary(history);

            if (location is Settlement)
            {
                var extra = _repository.GetExtendedStationInfo(location.Id);
                ApplyExtra(summary, extra);
            }
            else if (location is Region)
            {
                var extra = _repository.GetRegionExtendedInfo(location.Id);
                ApplyExtra(summary, extra);
            }

            return summary;
        }

        private static void ApplyExtra(WeatherSummary summary, (string settlementType, string stationType, int sensorsCount, string latestCondition) extra)
        {
            summary.SettlementType = extra.settlementType;
            summary.StationType = extra.stationType;
            summary.ActiveSensorsCount = extra.sensorsCount;
            summary.LatestCondition = extra.latestCondition;
        }

        public IEnumerable<Location> GetLocations() => _repository.GetAllLocations();

        public Task<IEnumerable<Location>> GetLocationsAsync() =>
            _repository.GetAllLocationsAsync();

        public bool TryUpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated, out string error)
        {
            if (!_validator.TryValidate(updated, settlement, out error)) return false;
            _repository.UpdateObservation(settlement, oldTimestamp, updated);
            return true;
        }

        public bool UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated) =>
            TryUpdateObservation(settlement, oldTimestamp, updated, out _);

        public void DeleteObservation(Settlement settlement, DateTime timestamp) =>
            _repository.DeleteObservation(settlement, timestamp);

        public int SeedTestData(int observationsPerSettlement) =>
            _repository.SeedTestData(observationsPerSettlement);

        public Task<int> SeedTestDataAsync(int observationsPerSettlement) =>
            _repository.SeedTestDataAsync(observationsPerSettlement);
    }
}
