using System;

namespace tpkursa4
{
    public class WeatherMeasurement
    {
        public double Temperature { get; }
        public double Humidity { get; }
        public double Pressure { get; }
        public double WindSpeed { get; } // Наше новое свойство
        public DateTime Timestamp { get; }

        // Новый конструктор для БД (5 параметров)
        public WeatherMeasurement(double temperature, double humidity, double pressure, double windSpeed, DateTime timestamp)
        {
            Temperature = temperature;
            Humidity = humidity;
            Pressure = pressure;
            WindSpeed = windSpeed;
            Timestamp = timestamp;
        }

        // Старый конструктор (для совместимости с MockRepository), вызывает новый через : this()
        public WeatherMeasurement(double temperature, double humidity, double pressure, DateTime timestamp)
            : this(temperature, humidity, pressure, 0.0, timestamp)
        {
        }

        public override string ToString() =>
            $"{Timestamp:dd.MM.yyyy HH:mm} | {Temperature}°C | {Humidity}% | {Pressure} mmHg | Ветер: {WindSpeed} м/с";
    }
}