using System;

namespace tpkursa4
{
    public class WeatherMeasurement
    {
        public double? Temperature { get; }
        public double? Humidity { get; }
        public double? Pressure { get; }
        public double? WindSpeed { get; }
        public DateTime Timestamp { get; }
        public string Condition { get; }

        public WeatherMeasurement(double? temperature, double? humidity, double? pressure, double? windSpeed, DateTime timestamp, string condition = null)
        {
            Temperature = temperature;
            Humidity = humidity;
            Pressure = pressure;
            WindSpeed = windSpeed;
            Timestamp = timestamp;
            Condition = condition;
        }

        public WeatherMeasurement(double temperature, double humidity, double pressure, DateTime timestamp)
            : this(temperature, humidity, pressure, 0.0, timestamp)
        {
        }

        public override string ToString() =>
            $"{Timestamp:dd.MM.yyyy HH:mm} | {Temperature}°C | {Humidity}% | {Pressure} mmHg | Ветер: {WindSpeed} м/с | {Condition}";
    }
}
