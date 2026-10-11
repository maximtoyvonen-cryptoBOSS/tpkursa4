using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace tpkursa4
{
    /// <summary>
    /// ViewModel главного окна (паттерн MVVM).
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IWeatherRepository _repository;
        private readonly WeatherManager _manager;
        private readonly Validator _validator;

        private Location _selectedLocation;
        private WeatherSummary _currentSummary = new WeatherSummary();
        private WeatherMeasurement _selectedMeasurement;
        private DateTime _startDate = new DateTime(2020, 1, 1);
        private DateTime _endDate = DateTime.Today.AddDays(1);
        private bool _isBusy;
        private bool _isRefreshing;
        private string _errorMessage;
        private string _statusText;

        // Поля для фонового датчика
        private VirtualWeatherStation _weatherStation;
        private bool _isSensorRunning;

        public ObservableCollection<Location> Locations { get; } = new ObservableCollection<Location>();
        public ObservableCollection<WeatherMeasurement> Measurements { get; } = new ObservableCollection<WeatherMeasurement>();

        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (_selectedLocation != value)
                {
                    _selectedLocation = value;
                    OnPropertyChanged();
                    _ = RefreshAsync();
                }
            }
        }

        public WeatherSummary CurrentSummary
        {
            get => _currentSummary;
            set { _currentSummary = value; OnPropertyChanged(); }
        }

        public WeatherMeasurement SelectedMeasurement
        {
            get => _selectedMeasurement;
            set { _selectedMeasurement = value; OnPropertyChanged(); FillInputsFromSelection(); }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        public string NewTemperature { get; set; } = "15";
        public string NewHumidity { get; set; } = "65";
        public string NewPressure { get; set; } = "750";
        public string NewWindSpeed { get; set; } = "4";

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsSensorRunning
        {
            get => _isSensorRunning;
            set
            {
                _isSensorRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SensorButtonText));
            }
        }

        public string SensorButtonText => IsSensorRunning ? "Остановить датчик" : "Запустить датчик";

        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SeedCommand { get; }
        public ICommand ToggleSensorCommand { get; }

        public MainViewModel() : this(DbRepository.CreateDefault())
        {
        }

        public MainViewModel(IWeatherRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _manager = new WeatherManager(repository);
            _validator = new Validator(repository);

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(_ => AddAsync());
            UpdateCommand = new RelayCommand(_ => UpdateAsync(), _ => SelectedMeasurement != null);
            DeleteCommand = new RelayCommand(_ => DeleteAsync(), _ => SelectedMeasurement != null);
            SeedCommand = new RelayCommand(_ => SeedAsync());
            ToggleSensorCommand = new RelayCommand(_ => ToggleSensor());

            LoadAsync();
        }

        private async void LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                var locs = await _repository.GetAllLocationsAsync();
                Locations.Clear();
                foreach (var l in locs) Locations.Add(l);

                SelectedLocation = Locations.FirstOrDefault();
                StatusText = $"Загружено объектов: {Locations.Count}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось загрузить данные из БД: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        #region Управление фоновым датчиком

        private void ToggleSensor()
        {
            if (IsSensorRunning)
            {
                StopSensor();
            }
            else
            {
                StartSensor();
            }
        }

        private void StartSensor()
        {
            ErrorMessage = null;
            var settlement = SelectedLocation as Settlement ?? Locations.OfType<Settlement>().FirstOrDefault();

            if (settlement == null)
            {
                ErrorMessage = "Для запуска датчика выберите населенный пункт.";
                return;
            }

            var gateway = new DataInboundGateway(_repository);
            _weatherStation = new VirtualWeatherStation($"STATION-{settlement.Id}", settlement, gateway, intervalSeconds: 5);
            _weatherStation.MeasurementReceived += OnSensorMeasurementReceived;

            IsSensorRunning = true;
            StatusText = $"Датчик запущен для: {settlement.Name} (интервал: 5 с)";
        }

        private void StopSensor()
        {
            if (_weatherStation != null)
            {
                _weatherStation.MeasurementReceived -= OnSensorMeasurementReceived;
                _weatherStation.Dispose();
                _weatherStation = null;
            }

            IsSensorRunning = false;
            StatusText = "Датчик остановлен.";
        }

        private void OnSensorMeasurementReceived(Settlement settlement, WeatherMeasurement measurement)
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                StatusText = $"[Датчик] {settlement.Name}: {measurement.Temperature:F1}°C в {measurement.Timestamp:HH:mm:ss}";

                if (SelectedLocation != null)
                {
                    bool isCurrentSettlement = SelectedLocation.Id == settlement.Id;
                    bool isParentRegion = SelectedLocation is Region reg && reg.Settlements.Any(s => s.Id == settlement.Id);

                    if (isCurrentSettlement || isParentRegion)
                    {
                        await RefreshAsync();
                    }
                }
            });
        }

        #endregion

        private WeatherPeriod BuildPeriod(out string error)
        {
            error = null;
            var period = new WeatherPeriod(StartDate.Date, EndDate.Date.AddDays(1).AddTicks(-1));
            if (!_validator.TryValidatePeriod(period, out error)) return null;
            return period;
        }

        public async Task RefreshAsync()
        {
            if (SelectedLocation == null || _isRefreshing) return;

            _isRefreshing = true;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var period = BuildPeriod(out string periodError);
                if (period == null)
                {
                    ErrorMessage = periodError;
                    return;
                }

                var summary = await Task.Run(() => _manager.GetStatistics(SelectedLocation, period));
                CurrentSummary = summary;

                var history = await _repository.GetHistoryAsync(SelectedLocation, period);
                var list = history.OrderByDescending(m => m.Timestamp).Take(100).ToList();

                Measurements.Clear();
                foreach (var m in list)
                {
                    Measurements.Add(m);
                }

                if (!IsSensorRunning)
                {
                    StatusText = $"Замеров: {list.Count} | {SelectedLocation.Name}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка при расчёте: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _isRefreshing = false;
            }
        }

        private bool TryParseInputs(out WeatherMeasurement m, out string error)
        {
            error = null;
            m = null;
            if (!TryParse(NewTemperature, out double t)) { error = "Температура — число."; return false; }
            if (!TryParse(NewHumidity, out double h)) { error = "Влажность — число."; return false; }
            if (!TryParse(NewPressure, out double p)) { error = "Давление — число."; return false; }
            if (!TryParse(NewWindSpeed, out double w)) { error = "Ветер — число."; return false; }

            m = new WeatherMeasurement(t, h, p, w, DateTime.Now);
            return true;
        }

        private static bool TryParse(string s, out double v) =>
            double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private void FillInputsFromSelection()
        {
            if (SelectedMeasurement == null) return;
            NewTemperature = SelectedMeasurement.Temperature.ToString(CultureInfo.InvariantCulture);
            NewHumidity = SelectedMeasurement.Humidity.ToString(CultureInfo.InvariantCulture);
            NewPressure = SelectedMeasurement.Pressure.ToString(CultureInfo.InvariantCulture);
            NewWindSpeed = SelectedMeasurement.WindSpeed.ToString(CultureInfo.InvariantCulture);

            OnPropertyChanged(nameof(NewTemperature));
            OnPropertyChanged(nameof(NewHumidity));
            OnPropertyChanged(nameof(NewPressure));
            OnPropertyChanged(nameof(NewWindSpeed));
        }

        private async void AddAsync()
        {
            ErrorMessage = null;
            if (!(SelectedLocation is Settlement settlement))
            {
                ErrorMessage = "Для добавления замера выберите поселение (не область).";
                return;
            }
            if (!TryParseInputs(out var m, out string parseError))
            {
                ErrorMessage = parseError;
                return;
            }
            if (!_validator.TryValidate(m, settlement, out string validationError))
            {
                ErrorMessage = validationError;
                return;
            }

            IsBusy = true;
            try
            {
                await _repository.SaveMeasurementAsync(settlement, m);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось сохранить: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void UpdateAsync()
        {
            ErrorMessage = null;
            if (!(SelectedLocation is Settlement settlement))
            {
                ErrorMessage = "Для изменения замера выберите поселение.";
                return;
            }
            if (SelectedMeasurement == null)
            {
                ErrorMessage = "Выберите замер в таблице.";
                return;
            }
            if (!TryParseInputs(out var m, out string parseError))
            {
                ErrorMessage = parseError;
                return;
            }
            if (!_validator.TryValidate(m, settlement, out string validationError))
            {
                ErrorMessage = validationError;
                return;
            }

            IsBusy = true;
            try
            {
                await Task.Run(() => _manager.UpdateObservation(settlement, SelectedMeasurement.Timestamp, m));
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось обновить: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void DeleteAsync()
        {
            ErrorMessage = null;
            if (!(SelectedLocation is Settlement settlement))
            {
                ErrorMessage = "Для удаления замера выберите поселение.";
                return;
            }
            if (SelectedMeasurement == null)
            {
                ErrorMessage = "Выберите замер в таблице.";
                return;
            }

            IsBusy = true;
            try
            {
                var ts = SelectedMeasurement.Timestamp;
                await Task.Run(() => _manager.DeleteObservation(settlement, ts));
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось удалить: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void SeedAsync()
        {
            ErrorMessage = null;
            IsBusy = true;
            try
            {
                int rows = await _repository.SeedTestDataAsync(30);
                StatusText = $"Добавлено записей: {rows}";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Seed не удался: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}