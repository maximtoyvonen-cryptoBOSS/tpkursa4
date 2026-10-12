using System;
using System.Collections.ObjectModel;

namespace tpkursa4
{
    public class Settlement : Location
    {
        private readonly ObservableCollection<WeatherMeasurement> _measurements;
        public string SettlementType { get; set; }
        public ReadOnlyObservableCollection<WeatherMeasurement> Measurements { get; }

        public Settlement(string name, string settlementType = "Город") : base(name)
        {
            SettlementType = settlementType;
            _measurements = new ObservableCollection<WeatherMeasurement>();
            Measurements = new ReadOnlyObservableCollection<WeatherMeasurement>(_measurements);
        }

        public void AddMeasurement(WeatherMeasurement measurement)
        {
            if (measurement == null) throw new ArgumentNullException(nameof(measurement));
            _measurements.Add(measurement);
        }

        public bool RemoveMeasurement(DateTime timestamp)
        {
            var m = FindByTimestamp(timestamp);
            if (m == null) return false;
            return _measurements.Remove(m);
        }

        public bool ReplaceMeasurement(DateTime oldTimestamp, WeatherMeasurement updated)
        {
            if (updated == null) throw new ArgumentNullException(nameof(updated));
            var m = FindByTimestamp(oldTimestamp);
            if (m == null) return false;
            int idx = _measurements.IndexOf(m);
            _measurements[idx] = updated;
            return true;
        }

        private WeatherMeasurement FindByTimestamp(DateTime timestamp)
        {
            foreach (var m in _measurements)
                if (m.Timestamp == timestamp) return m;
            return null;
        }
    }
}