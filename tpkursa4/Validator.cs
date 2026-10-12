using System;

namespace tpkursa4
{
    public class Validator
    {
        private readonly IWeatherRepository _repository;

        public Validator(IWeatherRepository repository)
        {
            _repository = repository;
        }

        public bool IsValid(WeatherMeasurement measurement, Location location) =>
            TryValidate(measurement, location, out _);

        public bool TryValidate(WeatherMeasurement measurement, Location location, out string error)
        {
            error = null;
            if (measurement == null || location == null)
            {
                error = "Нет данных замера или локации.";
                return false;
            }

            if (!TryRange(measurement.Temperature, -100, 100, "Температура должна быть от -100 до +100 °C.", out error))
                return false;
            if (!TryRange(measurement.Humidity, 0, 100, "Влажность должна быть от 0 до 100%.", out error))
                return false;
            if (!TryRange(measurement.Pressure, 600, 800, "Давление должно быть от 600 до 800 мм рт. ст.", out error))
                return false;
            if (!TryRange(measurement.WindSpeed, 0, 60, "Скорость ветра должна быть от 0 до 60 м/с.", out error))
                return false;

            if (location.Id <= 0)
            {
                error = "Локация не инициализирована.";
                return false;
            }

            if (measurement.Timestamp > DateTime.Now.AddMinutes(1))
            {
                error = "Дата замера не может быть в будущем.";
                return false;
            }

            return true;
        }

        public bool IsPeriodValid(WeatherPeriod period) =>
            TryValidatePeriod(period, out _);

        public bool TryValidatePeriod(WeatherPeriod period, out string error)
        {
            error = null;
            if (period == null)
            {
                error = "Период не задан.";
                return false;
            }
            if (period.Start > period.End)
            {
                error = "Начало периода позже конца.";
                return false;
            }
            if (period.End > DateTime.Now.AddHours(48))
            {
                error = "Конец периода в будущем.";
                return false;
            }
            return true;
        }

        private static bool TryRange(double? value, double min, double max, string message, out string error)
        {
            error = null;
            if (!value.HasValue)
            {
                error = message;
                return false;
            }
            if (value.Value < min || value.Value > max)
            {
                error = message;
                return false;
            }
            return true;
        }
    }
}
