using System;
using System.Threading;
using System.Threading.Tasks;

namespace tpkursa4
{
    public class VirtualWeatherStation : IWeatherStation, IDisposable
    {
        private readonly Timer _timer;
        private readonly Random _rnd = new Random();
        private readonly DataInboundGateway _gateway;
        private readonly Settlement _targetSettlement;
        private readonly int _intervalMs;
        private bool _isDisposed;

        public string StationId { get; }
        public event Action<Settlement, WeatherMeasurement> MeasurementReceived;

        public VirtualWeatherStation(string stationId, Settlement settlement, DataInboundGateway gateway, int intervalSeconds = 5)
        {
            StationId = stationId;
            _targetSettlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _intervalMs = intervalSeconds * 1000;

            // Запускаем разово через 1 секунду (Timeout.Infinite исключает наложение)
            _timer = new Timer(async state => await OnTimerTickAsync(), null, 1000, Timeout.Infinite);
        }

        private async Task OnTimerTickAsync()
        {
            if (_isDisposed) return;

            try
            {
                var measurement = GetCurrentMeasurement();

                // Сохранение в БД в фоновом потоке
                await Task.Run(() => _gateway.ProcessIncomingData(_targetSettlement, measurement));

                MeasurementReceived?.Invoke(_targetSettlement, measurement);
            }
            catch
            {
                // Подавление единичных сбоев сети/БД, чтобы не ронять таймер
            }
            finally
            {
                // Планируем следующий тик только после полного завершения текущего
                if (!_isDisposed)
                {
                    _timer.Change(_intervalMs, Timeout.Infinite);
                }
            }
        }

        public WeatherMeasurement GetCurrentMeasurement()
        {
            return new WeatherMeasurement(
                temperature: Math.Round(12.0 + (_rnd.NextDouble() * 10 - 5), 1),
                humidity: Math.Round(55.0 + _rnd.NextDouble() * 30, 1),
                pressure: Math.Round(745.0 + _rnd.NextDouble() * 15, 1),
                windSpeed: Math.Round(1.0 + _rnd.NextDouble() * 6, 1),
                timestamp: DateTime.Now
            );
        }

        public void Dispose()
        {
            _isDisposed = true;
            _timer?.Dispose();
        }
    }
}