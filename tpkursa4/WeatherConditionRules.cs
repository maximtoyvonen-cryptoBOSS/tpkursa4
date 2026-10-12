namespace tpkursa4
{
    public static class WeatherConditionRules
    {
        public const string Clear = "Ясно";
        public const string Cloudy = "Пасмурно";
        public const string Rain = "Дождь";
        public const string Storm = "Гроза";

        public static string Infer(WeatherMeasurement measurement)
        {
            if (measurement == null) return Clear;

            double humidity = measurement.Humidity ?? 0;
            double wind = measurement.WindSpeed ?? 0;

            if (wind >= 15 && humidity >= 70) return Storm;
            if (humidity >= 85) return Rain;
            if (humidity >= 70) return Cloudy;
            return Clear;
        }
    }
}
