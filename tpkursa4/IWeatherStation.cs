namespace tpkursa4
{
    public interface IWeatherStation
    {
        // Текущий замер с датчика станции
        WeatherMeasurement GetCurrentMeasurement();

        // Идентификатор станции
        string StationId { get; }
    }
}