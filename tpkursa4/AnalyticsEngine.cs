using System;
using System.Collections.Generic;
using System.Linq;

namespace tpkursa4
{
    public class AnalyticsEngine
    {
        public double CalculateAverageTemperature(Location location, WeatherPeriod period)
        {
            IEnumerable<WeatherMeasurement> measurements;

            if (location is Settlement settlement)
            {
                measurements = settlement.Measurements.Where(m => period.Contains(m.Timestamp));
            }
            else if (location is Region region)
            {
                measurements = region.GetAllMeasurements().Where(m => period.Contains(m.Timestamp));
            }
            else
            {
                return 0;
            }

            if (!measurements.Any()) return 0;

            return measurements.Average(m => m.Temperature);
        }

        public WeatherSummary GetFullSummary(Location location, WeatherPeriod period)
        {
            IEnumerable<WeatherMeasurement> data = Enumerable.Empty<WeatherMeasurement>();

            if (location is Settlement s)
                data = s.Measurements.Where(m => period.Contains(m.Timestamp));
            else if (location is Region r)
                data = r.GetAllMeasurements().Where(m => period.Contains(m.Timestamp));

            return Calculate(data);
        }

        // Этот метод вызывается в WeatherManager при работе с БД
        public WeatherSummary Calculate(IEnumerable<WeatherMeasurement> data)
        {
            if (data == null || !data.Any())
                return new WeatherSummary();

            return new WeatherSummary
            {
                AverageTemperature = data.Average(m => m.Temperature),
                AverageHumidity = data.Average(m => m.Humidity),
                AveragePressure = data.Average(m => m.Pressure),
                AverageWindSpeed = data.Average(m => m.WindSpeed) // Считаем средний ветер
            };
        }
    }

    public class WeatherSummary
    {
        public double AverageTemperature { get; set; }
        public double AverageHumidity { get; set; }
        public double AveragePressure { get; set; }
        public double AverageWindSpeed { get; set; } // Добавили свойство в структуру
                                                     // Добавь эти свойства внутрь класса WeatherSummary
        public string SettlementType { get; set; } = "Неизвестно";
        public string StationType { get; set; } = "Нет станции";
        public int ActiveSensorsCount { get; set; } = 0;
        public string LatestCondition { get; set; } = "Нет данных";
    }
}