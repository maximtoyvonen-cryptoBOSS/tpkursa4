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
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IWeatherRepository _repository;
        private readonly WeatherManager _manager;
        private readonly Validator _validator;
        private readonly UserAccount _user;

        private Location _selectedLocation;
        private WeatherSummary _currentSummary = new WeatherSummary();
        private WeatherMeasurement _selectedMeasurement;
        private DateTime _startDate = new DateTime(2020, 1, 1);
        private DateTime _endDate = DateTime.Today.AddDays(1);
        private bool _isBusy;
        private bool _isRefreshing;
        private string _errorMessage;
        private string _statusText;
        private string _conditionFilter;
        private string _weatherQueryResult;

        private VirtualWeatherStation _weatherStation;
        private bool _isSensorRunning;

        public ObservableCollection<Location> Locations { get; } = new ObservableCollection<Location>();
        public ObservableCollection<WeatherMeasurement> Measurements { get; } = new ObservableCollection<WeatherMeasurement>();
        public ObservableCollection<string> WeatherConditions { get; } = new ObservableCollection<string>();

        public UserAccount CurrentUser => _user;
        public bool CanEdit { get; }
        public string WindowTitle => _user != null
            ? $"Система контроля погоды — {_user.Username} ({_user.RoleName})"
            : "Система контроля погоды";

        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (_selectedLocation != value)
                {
                    _selectedLocation = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FavoriteButtonText));
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
            set
            {
                if (_startDate == value) return;
                _startDate = value;
                OnPropertyChanged();
                _ = RefreshAsync();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate == value) return;
                _endDate = value;
                OnPropertyChanged();
                _ = RefreshAsync();
            }
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

        public string ConditionFilter
        {
            get => _conditionFilter;
            set { _conditionFilter = value; OnPropertyChanged(); }
        }

        public string WeatherQueryResult
        {
            get => _weatherQueryResult;
            set { _weatherQueryResult = value; OnPropertyChanged(); }
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
        public string FavoriteButtonText =>
            SelectedLocation is Settlement s && s.IsFavorite ? "Убрать из избранного" : "В избранное";

        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SeedCommand { get; }
        public ICommand ToggleSensorCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand FilterByWeatherCommand { get; }

        public MainViewModel() : this(DbRepository.CreateDefault(), null)
        {
        }

        public MainViewModel(IWeatherRepository repository) : this(repository, null)
        {
        }

        public MainViewModel(IWeatherRepository repository, UserAccount user)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _user = user;
            CanEdit = user == null || user.IsAdmin;
            _manager = new WeatherManager(repository);
            _validator = new Validator(repository);

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddCommand = new RelayCommand(_ => AddAsync(), _ => CanEdit);
            UpdateCommand = new RelayCommand(_ => UpdateAsync(), _ => CanEdit && SelectedMeasurement != null);
            DeleteCommand = new RelayCommand(_ => DeleteAsync(), _ => CanEdit && SelectedMeasurement != null);
            SeedCommand = new RelayCommand(_ => SeedAsync(), _ => CanEdit);
            ToggleSensorCommand = new RelayCommand(_ => ToggleSensor());
            ToggleFavoriteCommand = new RelayCommand(_ => ToggleFavorite(), _ => SelectedLocation is Settlement && _user != null && _user.Id > 0);
            FilterByWeatherCommand = new RelayCommand(_ => FilterByWeather());

            LoadAsync();
        }

        private async void LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                var locs = await _manager.GetLocationsAsync();
                var favorites = _user != null && _user.Id > 0
                    ? _repository.GetFavoriteSettlementIds(_user.Id)
                    : Array.Empty<int>();

                Locations.Clear();
                foreach (var l in locs)
                {
                    if (l is Settlement s) s.IsFavorite = favorites.Contains(s.Id);
                    Locations.Add(l);
                }

                WeatherConditions.Clear();
                foreach (var name in _repository.GetWeatherConditionNames())
                    WeatherConditions.Add(name);
                if (WeatherConditions.Count > 0)
                    ConditionFilter = WeatherConditions[0];

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

        private void ToggleSensor()
        {
            if (IsSensorRunning) StopSensor();
            else StartSensor();
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
                        await RefreshAsync();
                }
            });
        }

        private void ToggleFavorite()
        {
            if (!(_selectedLocation is Settlement settlement) || _user == null || _user.Id <= 0)
            {
                ErrorMessage = "Избранное доступно для поселения после входа.";
                return;
            }

            try
            {
                bool next = !settlement.IsFavorite;
                _repository.SetFavorite(_user.Id, settlement.Id, next);
                settlement.IsFavorite = next;
                OnPropertyChanged(nameof(FavoriteButtonText));
                StatusText = next ? $"В избранном: {settlement.Name}" : $"Убрано из избранного: {settlement.Name}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось обновить избранное: {ex.Message}";
            }
        }

        private void FilterByWeather()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ConditionFilter))
                {
                    WeatherQueryResult = "Выберите условие погоды.";
                    return;
                }

                var names = _repository.GetSettlementsByWeather(ConditionFilter);
                WeatherQueryResult = names.Count == 0
                    ? $"Нет поселений с погодой «{ConditionFilter}»."
                    : "Поселения: " + string.Join(", ", names);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Запрос к БД не удался: {ex.Message}";
            }
        }

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
                    Measurements.Add(m);

                if (!IsSensorRunning)
                    StatusText = $"Замеров: {list.Count} | {SelectedLocation.Name}";
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
            NewTemperature = Format(SelectedMeasurement.Temperature);
            NewHumidity = Format(SelectedMeasurement.Humidity);
            NewPressure = Format(SelectedMeasurement.Pressure);
            NewWindSpeed = Format(SelectedMeasurement.WindSpeed);

            OnPropertyChanged(nameof(NewTemperature));
            OnPropertyChanged(nameof(NewHumidity));
            OnPropertyChanged(nameof(NewPressure));
            OnPropertyChanged(nameof(NewWindSpeed));
        }

        private static string Format(double? value) =>
            value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

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

            IsBusy = true;
            try
            {
                string validationError = null;
                bool ok = false;
                await Task.Run(() => ok = _manager.TryRecordWeather(settlement, m, out validationError));
                if (!ok)
                {
                    ErrorMessage = validationError;
                    return;
                }
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

            IsBusy = true;
            try
            {
                var oldTs = SelectedMeasurement.Timestamp;
                string validationError = null;
                bool ok = false;
                await Task.Run(() => ok = _manager.TryUpdateObservation(settlement, oldTs, m, out validationError));
                if (!ok)
                {
                    ErrorMessage = validationError;
                    return;
                }
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
                int rows = await _manager.SeedTestDataAsync(30);
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
