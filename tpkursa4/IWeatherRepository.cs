using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tpkursa4
{
    public interface IWeatherRepository
    {
        void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement);
        IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period);
        IEnumerable<Location> GetAllLocations();
        (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId);
        (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId);

        void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated);
        void DeleteObservation(Settlement settlement, DateTime timestamp);

        int SeedTestData(int observationsPerSettlement);

        IReadOnlyList<string> GetWeatherConditionNames();
        IReadOnlyList<string> GetSettlementsByWeather(string conditionName);
        IReadOnlyList<int> GetFavoriteSettlementIds(int userId);
        void SetFavorite(int userId, int settlementId, bool isFavorite);

        Task<IEnumerable<Location>> GetAllLocationsAsync();
        Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period);
        Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement);
        Task<int> SeedTestDataAsync(int observationsPerSettlement);
    }
}
