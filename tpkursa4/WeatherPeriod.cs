using System;

namespace tpkursa4
{
    public class WeatherPeriod
    {
        public DateTime Start { get; }
        public DateTime End { get; }

        public WeatherPeriod(DateTime start, DateTime end)
        {
            if (start > end)
                throw new ArgumentException("Дата начала не может быть позже конца.");

            Start = start;
            End = end;
        }

        public bool Contains(DateTime date) => date >= Start && date <= End;

        public override bool Equals(object obj) =>
            obj is WeatherPeriod other && Start == other.Start && End == other.End;

        public override int GetHashCode() => (Start, End).GetHashCode();
    }
}