using System;
using System.Collections.Generic;
using System.Linq;

namespace tpkursa4
{
    public class AnalyticsEngine
    {
        public double CalculateAverageTemperature(IEnumerable<WeatherMeasurement> data) =>
            Average(data, m => m.Temperature);

        public WeatherSummary GetFullSummary(IEnumerable<WeatherMeasurement> data) =>
            Calculate(data);

        public WeatherSummary Calculate(IEnumerable<WeatherMeasurement> data)
        {
            if (data == null) return new WeatherSummary();

            var list = data as IList<WeatherMeasurement> ?? data.ToList();
            if (list.Count == 0) return new WeatherSummary();

            return new WeatherSummary
            {
                AverageTemperature = Average(list, m => m.Temperature),
                AverageHumidity = Average(list, m => m.Humidity),
                AveragePressure = Average(list, m => m.Pressure),
                AverageWindSpeed = Average(list, m => m.WindSpeed)
            };
        }

        private static double Average(IEnumerable<WeatherMeasurement> data, Func<WeatherMeasurement, double?> selector)
        {
            var values = data.Select(selector).Where(v => v.HasValue).Select(v => v.Value).ToList();
            return values.Count == 0 ? 0 : values.Average();
        }
    }

    public class WeatherSummary
    {
        public double AverageTemperature { get; set; }
        public double AverageHumidity { get; set; }
        public double AveragePressure { get; set; }
        public double AverageWindSpeed { get; set; }
        public string SettlementType { get; set; } = "Неизвестно";
        public string StationType { get; set; } = "Нет станции";
        public int ActiveSensorsCount { get; set; }
        public string LatestCondition { get; set; } = "Нет данных";
    }
}
