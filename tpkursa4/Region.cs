using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace tpkursa4
{
    /// <summary>
    /// Сущность "Область", реализующая паттерн «Компоновщик».
    /// Группирует населенные пункты и делегирует им запросы данных.
    /// </summary>
    public class Region : Location
    {
        // Коллекция объектов Settlement, входящих в данную область
        private readonly ObservableCollection<Settlement> _settlements;

        /// <summary>
        /// Список населенных пунктов области.
        /// </summary>
        public ReadOnlyObservableCollection<Settlement> Settlements { get; }

        public Region(string name) : base(name)
        {
            _settlements = new ObservableCollection<Settlement>();
            Settlements = new ReadOnlyObservableCollection<Settlement>(_settlements);
        }

        /// <summary>
        /// Добавляет населенный пункт в состав области.
        /// </summary>
        public void AddLocation(Settlement settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            if (!_settlements.Contains(settlement))
            {
                _settlements.Add(settlement);
            }
        }

        /// <summary>
        /// Метод для получения всех измерений со всех городов и сел данной области.
        /// Реализует принцип: значения для области вычисляются на основе данных её пунктов.
        /// </summary>
        public IEnumerable<WeatherMeasurement> GetAllMeasurements()
        {
            // Собираем все замеры из каждого поселения в один плоский список
            return _settlements.SelectMany(s => s.Measurements);
        }
    }
}