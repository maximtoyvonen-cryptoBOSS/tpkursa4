using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tpkursa4
{
    /// <summary>
    /// Паттерн «Репозиторий»: единый контракт доступа к данным (БД или mock).
    /// </summary>
    public interface IWeatherRepository
    {
        void SaveMeasurement(Settlement settlement, WeatherMeasurement measurement);
        IEnumerable<WeatherMeasurement> GetHistory(Location location, WeatherPeriod period);
        IEnumerable<Location> GetAllLocations();
        // Карточка поселения: тип, станция, датчики, последнее условие.
        (string settlementType, string stationType, int sensorsCount, string latestCondition) GetExtendedStationInfo(int settlementId);
        // Карточка области: агрегат по всем её поселениям.
        (string settlementType, string stationType, int sensorsCount, string latestCondition) GetRegionExtendedInfo(int regionId);

        // CRUD: обновление и удаление одного наблюдения
        // (одно наблюдение = 4 строки датчиков с одной меткой времени).
        void UpdateObservation(Settlement settlement, DateTime oldTimestamp, WeatherMeasurement updated);
        void DeleteObservation(Settlement settlement, DateTime timestamp);

        // Автозаполнение БД тестовыми данными. Возвращает число вставленных строк.
        int SeedTestData(int observationsPerSettlement);

        // Асинхронные версии для фоновых задач (не блокируют UI).
        Task<IEnumerable<Location>> GetAllLocationsAsync();
        Task<IEnumerable<WeatherMeasurement>> GetHistoryAsync(Location location, WeatherPeriod period);
        Task SaveMeasurementAsync(Settlement settlement, WeatherMeasurement measurement);
        Task<int> SeedTestDataAsync(int observationsPerSettlement);
    }
}
