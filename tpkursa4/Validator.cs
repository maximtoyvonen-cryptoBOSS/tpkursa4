using System;
using System.Linq;

namespace tpkursa4
{
    /// <summary>
    /// Компонент, следящий за соблюдением логических ограничений и правил предметной области.
    /// </summary>
    public class Validator
    {
        private readonly IWeatherRepository _repository;

        public Validator(IWeatherRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Проверяет измерение на соответствие физическим и логическим нормам.
        /// </summary>
        public bool IsValid(WeatherMeasurement measurement, Location location) =>
            TryValidate(measurement, location, out _);

        /// <summary>
        /// Валидация с текстом ошибки для UI (валидация на уровне приложения).
        /// </summary>
        public bool TryValidate(WeatherMeasurement measurement, Location location, out string error)
        {
            error = null;
            // 1. Проверка на наличие объекта
            if (measurement == null || location == null)
            {
                error = "Нет данных замера или локации.";
                return false;
            }

            // 2. Проверка физических границ температуры (отсеивание аномалий)
            if (measurement.Temperature < -100 || measurement.Temperature > 100)
            {
                error = "Температура должна быть от -100 до +100 °C.";
                return false;
            }

            // 3. Проверка влажности (от 0% до 100%)
            if (measurement.Humidity < 0 || measurement.Humidity > 100)
            {
                error = "Влажность должна быть от 0 до 100%.";
                return false;
            }

            // 4. Проверка давления (нормальные значения в мм рт. ст., например от 600 до 800)
            if (measurement.Pressure < 600 || measurement.Pressure > 800)
            {
                error = "Давление должно быть от 600 до 800 мм рт. ст.";
                return false;
            }

            // 5. Проверка ветра (0..60 м/с, выше — ураган/ошибка датчика)
            if (measurement.WindSpeed < 0 || measurement.WindSpeed > 60)
            {
                error = "Скорость ветра должна быть от 0 до 60 м/с.";
                return false;
            }
            //var allLocations = _repository.GetAllLocations();
            //if (!allLocations.Any(l => l.Id == location.Id))
            //{
            //    error = "Локация не найдена в системе.";
            //    return false;
            //}
            if (location.Id <= 0)
            {
                error = "Локация не инициализирована.";
                return false;
            }

            // 7. Метка времени не из будущего.
            if (measurement.Timestamp > DateTime.Now.AddMinutes(1))
            {
                error = "Дата замера не может быть в будущем.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Проверяет корректность временного периода.
        /// </summary>
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
    }
}
