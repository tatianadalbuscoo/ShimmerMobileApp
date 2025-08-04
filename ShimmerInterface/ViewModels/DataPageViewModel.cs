using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI.IMU;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;

namespace ShimmerInterface.ViewModels;

/// <summary>
/// Specifies the display mode for the chart: either a single parameter or a group of parameters (e.g., X/Y/Z).
/// </summary>
public enum ChartDisplayMode
{
    Single,
    Multi
}

public partial class DataPageViewModel : ObservableObject, IDisposable
{
    // Device reference from the Shimmer API
    private readonly XR2Learn_ShimmerIMU shimmer;

    // Timer that triggers data updates every second
    private System.Timers.Timer? timer;

    private bool _disposed = false;

    // Dizionario che memorizza i dati delle serie temporali per ciascun parametro (X/Y/Z, GSR, PPG...)
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();

    private readonly object _dataLock = new();

    // Elapsed seconds since data collection started

    private int sampleCounter = 0;


    // Sensor enable flags (set from SensorConfiguration passed in constructor)
    private bool enableLowNoiseAccelerometer;
    private bool enableWideRangeAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;
    private bool enablePressureTemperature;
    private bool enableBattery;
    private bool enableExtA6;
    private bool enableExtA7;
    private bool enableExtA15;

    // Limiti generali numerici
    private const double MAX_DOUBLE = 1e6;
    private const double MIN_DOUBLE = -1e6;

    // Limiti specifici per la tua applicazione
    private const double MAX_Y_AXIS = 100_000;
    private const double MIN_Y_AXIS = -100_000;
    private const int MAX_TIME_WINDOW_SECONDS = 600;   // 10 minuti
    private const int MIN_TIME_WINDOW_SECONDS = 1;
    private const int MAX_X_AXIS_LABEL_INTERVAL = 1000;
    private const int MIN_X_AXIS_LABEL_INTERVAL = 1;
    private const double MAX_SAMPLING_RATE = 1000;
    private const double MIN_SAMPLING_RATE = 1;




    // Valori di backup per il ripristino in caso di input non validi
    private double _lastValidYAxisMin = 0;
    private double _lastValidYAxisMax = 1;
    private int _lastValidTimeWindowSeconds = 20;
    private int _lastValidXAxisLabelInterval = 5;
    private double _lastValidSamplingRate = 51.2;


    // Backing fields for input validation
    private string _yAxisMinText = "0";
    private string _yAxisMaxText = "1";
    private string _timeWindowSecondsText = "20";
    private string _xAxisLabelIntervalText = "5";

    // Campi per memorizzare i valori automatici
    private double _autoYAxisMin = 0;
    private double _autoYAxisMax = 1;


    [ObservableProperty]
    private string selectedParameter = "Low-Noise AccelerometerX";

    [ObservableProperty]
    private double yAxisMin = 0;

    [ObservableProperty]
    private double yAxisMax = 1;

    [ObservableProperty]
    private int timeWindowSeconds = 20;

    [ObservableProperty]
    private string yAxisLabel = "Value";

    [ObservableProperty]
    private string yAxisUnit = "Unit";

    [ObservableProperty]
    private string chartTitle = "Real-time Data";

    [ObservableProperty]
    private int xAxisLabelInterval = 5;

    [ObservableProperty]
    private string validationMessage = "";

    [ObservableProperty]
    private bool isXAxisLabelIntervalEnabled = true;

    [ObservableProperty]
    private double samplingRateDisplay;


    [ObservableProperty]
    private bool showGrid = true;

    [ObservableProperty]
    private bool autoYAxis = false;

    // Proprietà per abilitare/disabilitare i campi manuali Y
    [ObservableProperty]
    private bool isYAxisManualEnabled = true;

    [ObservableProperty]
    private ChartDisplayMode chartDisplayMode = ChartDisplayMode.Single;


    /// <summary>
    /// Tempo corrente in secondi dall'inizio della raccolta dati
    /// </summary>
    public double CurrentTimeInSeconds => sampleCounter / shimmer.SamplingRate;







    private string _samplingRateText = "51.2";
    public string SamplingRateText
    {
        get => _samplingRateText;
        set
        {
            if (SetProperty(ref _samplingRateText, value))
            {
                ValidateAndUpdateSamplingRate(value);
            }
        }
    }



    // Text properties for input fields
    public string YAxisMinText
    {
        get => _yAxisMinText;
        set
        {
            if (SetProperty(ref _yAxisMinText, value))
            {
                ValidateAndUpdateYAxisMin(value);
            }
        }
    }

    public string YAxisMaxText
    {
        get => _yAxisMaxText;
        set
        {
            if (SetProperty(ref _yAxisMaxText, value))
            {
                ValidateAndUpdateYAxisMax(value);
            }
        }
    }

    public string TimeWindowSecondsText
    {
        get => _timeWindowSecondsText;
        set
        {
            if (SetProperty(ref _timeWindowSecondsText, value))
            {
                ValidateAndUpdateTimeWindow(value);
            }
        }
    }

    public string XAxisLabelIntervalText
    {
        get => _xAxisLabelIntervalText;
        set
        {
            if (SetProperty(ref _xAxisLabelIntervalText, value))
            {
                ValidateAndUpdateXAxisInterval(value);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            StopTimer();
            ChartUpdateRequested = null; // Pulisci gli event handlers
            ClearAllDataCollections();
        }
        _disposed = true;
    }



public ObservableCollection<string> AvailableParameters { get; } = new();

    public event EventHandler? ChartUpdateRequested;

    public DataPageViewModel(XR2Learn_ShimmerIMU shimmerDevice, ShimmerDevice config)
    {
        shimmer = shimmerDevice;
        enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
        enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;
        enablePressureTemperature = config.EnablePressureTemperature;
        enableBattery = config.EnableBattery;
        enableExtA6 = config.EnableExtA6;
        enableExtA7 = config.EnableExtA7;
        enableExtA15 = config.EnableExtA15;

        samplingRateDisplay = shimmer.SamplingRate;

        InitializeAvailableParameters();

        if (!AvailableParameters.Contains(SelectedParameter))
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

        // CORREZIONE: Crea collezioni solo per parametri che hanno dati reali
        InitializeDataCollections();

        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
            IsXAxisLabelIntervalEnabled = SelectedParameter != "HeartRate";
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _samplingRateText = shimmer.SamplingRate.ToString(CultureInfo.InvariantCulture);
        _lastValidSamplingRate = shimmer.SamplingRate;
        OnPropertyChanged(nameof(SamplingRateText));
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        UpdateTextProperties();

    }

    private void InitializeDataCollections()
    {
        // Lista di TUTTI i parametri che hanno dati reali (non gruppi)
        var dataParameters = new List<string>();

        if (enableLowNoiseAccelerometer)
        {
            dataParameters.AddRange(new[] { "Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ" });
        }

        if (enableWideRangeAccelerometer)
        {
            dataParameters.AddRange(new[] { "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" });
        }

        if (enableGyroscope)
        {
            dataParameters.AddRange(new[] { "GyroscopeX", "GyroscopeY", "GyroscopeZ" });
        }

        if (enableMagnetometer)
        {
            dataParameters.AddRange(new[] { "MagnetometerX", "MagnetometerY", "MagnetometerZ" });
        }

        if (enableBattery)
        {
            dataParameters.AddRange(new[] { "BatteryVoltage", "BatteryPercent" });
        }

        if (enablePressureTemperature)
        {
            dataParameters.AddRange(new[] { "Temperature_BMP180", "Pressure_BMP180" });
        }



        if (enableExtA6)
            dataParameters.Add("ExtADC_A6");
        if (enableExtA7)
            dataParameters.Add("ExtADC_A7");
        if (enableExtA15)
            dataParameters.Add("ExtADC_A15");

        // Crea collezioni solo per parametri con dati
        foreach (var parameter in dataParameters)
        {
            if (!dataPointsCollections.ContainsKey(parameter))
            {
                dataPointsCollections[parameter] = new List<float>();
                timeStampsCollections[parameter] = new List<int>();
            }
        }
    }

    partial void OnAutoYAxisChanged(bool value)
    {
        // Aggiorna lo stato dei campi manuali
        IsYAxisManualEnabled = !value;

        if (value)
        {
            // Passa alla modalità automatica
            // Salva i valori manuali correnti come backup
            _lastValidYAxisMin = YAxisMin;
            _lastValidYAxisMax = YAxisMax;

            // Calcola i valori automatici basati sui dati attuali
            CalculateAutoYAxisRange();

            // Applica i valori automatici
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }
        else
        {
            // Passa alla modalità manuale
            // Ripristina i valori manuali salvati
            YAxisMin = _lastValidYAxisMin;
            YAxisMax = _lastValidYAxisMax;
        }

        // Aggiorna i testi per riflettere i nuovi valori
        UpdateTextProperties();

        // Pulisci eventuali messaggi di validazione
        ValidationMessage = "";

        UpdateChart();
    }


    private void CalculateAutoYAxisRange()
    {
        string cleanParam = CleanParameterName(SelectedParameter);

        if (IsMultiChart(cleanParam))
        {
            var subParams = GetSubParameters(cleanParam);
            var allValues = new List<float>();

            foreach (var param in subParams)
            {
                if (dataPointsCollections.ContainsKey(param) && dataPointsCollections[param].Count > 0)
                    allValues.AddRange(dataPointsCollections[param]);
            }

            if (allValues.Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(cleanParam);
                _autoYAxisMax = GetDefaultYAxisMax(cleanParam);
                return;
            }

            var min = allValues.Min();
            var max = allValues.Max();
            var range = max - min;

            if (Math.Abs(range) < 0.001)
            {
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }
        else
        {
            if (!dataPointsCollections.ContainsKey(cleanParam) || dataPointsCollections[cleanParam].Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(cleanParam);
                _autoYAxisMax = GetDefaultYAxisMax(cleanParam);
                return;
            }

            var data = dataPointsCollections[cleanParam];
            var min = data.Min();
            var max = data.Max();
            var range = max - min;

            if (Math.Abs(range) < 0.001)
            {
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }

        // Alla fine del metodo, PRIMA di uscire dalla funzione:
        _autoYAxisMin = Math.Round(_autoYAxisMin, 3);
        _autoYAxisMax = Math.Round(_autoYAxisMax, 3);

    }





    // Aggiorna i testi dei campi di input in base ai valori numerici correnti.
    private void UpdateTextProperties()
    {
        _yAxisMinText = YAxisMin.ToString(CultureInfo.InvariantCulture);
        _yAxisMaxText = YAxisMax.ToString(CultureInfo.InvariantCulture);
        _timeWindowSecondsText = TimeWindowSeconds.ToString();
        _xAxisLabelIntervalText = XAxisLabelInterval.ToString();

        OnPropertyChanged(nameof(YAxisMinText));
        OnPropertyChanged(nameof(YAxisMaxText));
        OnPropertyChanged(nameof(TimeWindowSecondsText));
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }


    // Valida e aggiorna il valore minimo dell'asse Y.




    private void ValidateAndUpdateYAxisMin(string value)
    {
        if (AutoYAxis)
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMin = GetDefaultYAxisMin(SelectedParameter);
            ValidationMessage = "";
            YAxisMin = defaultMin;
            _lastValidYAxisMin = defaultMin;
            UpdateChart();
            return;
        }

        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        if (TryParseDouble(value, out double result))
        {
            // *** AGGIUNGI QUESTO CONTROLLO ***
            if (result < MIN_Y_AXIS || result > MAX_Y_AXIS)
            {
                ValidationMessage = $"Y Min out of range ({MIN_Y_AXIS} to {MAX_Y_AXIS}).";
                ResetYAxisMinText();
                return;
            }
            if (result >= YAxisMax)
            {
                ValidationMessage = "Y Min cannot be greater than or equal to Y Max.";
                ResetYAxisMinText();
                return;
            }

            ValidationMessage = "";
            YAxisMin = result;
            _lastValidYAxisMin = result;
            UpdateChart();
        }
        else
        {
            ValidationMessage = "Y Min must be a valid number (no letters or special characters allowed).";
            ResetYAxisMinText();
        }
    }

    private void ValidateAndUpdateYAxisMax(string value)
    {
        if (AutoYAxis)
            return;

        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMax = GetDefaultYAxisMax(SelectedParameter);
            ValidationMessage = "";
            YAxisMax = defaultMax;
            _lastValidYAxisMax = defaultMax;
            UpdateChart();
            return;
        }

        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        if (TryParseDouble(value, out double result))
        {
            // *** AGGIUNGI QUESTO CONTROLLO ***
            if (result < MIN_Y_AXIS || result > MAX_Y_AXIS)
            {
                ValidationMessage = $"Y Max out of range ({MIN_Y_AXIS} to {MAX_Y_AXIS}).";
                ResetYAxisMaxText();
                return;
            }
            if (result <= YAxisMin)
            {
                ValidationMessage = "Y Max cannot be less than or equal to Y Min.";
                ResetYAxisMaxText();
                return;
            }

            ValidationMessage = "";
            YAxisMax = result;
            _lastValidYAxisMax = result;
            UpdateChart();
        }
        else
        {
            ValidationMessage = "Y Max must be a valid number (no letters or special characters allowed).";
            ResetYAxisMaxText();
        }
    }



    private void ValidateAndUpdateSamplingRate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            const double defaultRate = 51.2;
            UpdateSamplingRateAndRestart(defaultRate);
            ValidationMessage = "";
            return;
        }

        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        if (TryParseDouble(value, out double result))
        {
            // *** AGGIUNGI QUESTO CONTROLLO ***
            if (result > MAX_SAMPLING_RATE)
            {
                ValidationMessage = $"Sampling rate too high. Maximum {MAX_SAMPLING_RATE} Hz.";
                ResetSamplingRateText();
                return;
            }
            if (result < MIN_SAMPLING_RATE)
            {
                ValidationMessage = $"Sampling rate too low. Minimum {MIN_SAMPLING_RATE} Hz.";
                ResetSamplingRateText();
                return;
            }

            UpdateSamplingRateAndRestart(result);
            ValidationMessage = "";
        }
        else
        {
            ValidationMessage = "Sampling rate must be a valid number (no letters or special characters allowed).";
            ResetSamplingRateText();
        }
    }


    private void UpdateSamplingRateAndRestart(double newRate)
    {
        // Ferma il timer esistente
        StopTimer();

        // Aggiorna il sampling rate
        shimmer.SamplingRate = newRate;
        SamplingRateDisplay = newRate;
        _lastValidSamplingRate = newRate;

        // IMPORTANTE: Pulisci TUTTI i dati esistenti per evitare problemi di mapping
        ClearAllDataCollections();

        // Resetta completamente i contatori
        ResetAllCounters();

        // Riavvia il timer con il nuovo intervallo
        StartTimer();

        // Aggiorna il grafico (mostrerà "Waiting for data...")
        UpdateChart();

        System.Diagnostics.Debug.WriteLine($"Sampling rate changed to {newRate}Hz - All data cleared");
    }


    private void ResetAllCounters()
    {
        sampleCounter = 0;
    }



    private void ResetSamplingRateText()
    {
        _samplingRateText = _lastValidSamplingRate.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SamplingRateText));
    }




    private void ValidateAndUpdateTimeWindow(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultTimeWindow = 20;
            ValidationMessage = "";
            TimeWindowSeconds = defaultTimeWindow;
            _lastValidTimeWindowSeconds = defaultTimeWindow;

            ClearAllDataCollections();
            ResetAllTimestamps();
            ResetAllCounters();

            UpdateChart();
            return;
        }

        if (TryParseInt(value, out int result))
        {
            // *** AGGIUNGI QUESTO CONTROLLO ***
            if (result > MAX_TIME_WINDOW_SECONDS)
            {
                ValidationMessage = $"Time Window too large. Maximum {MAX_TIME_WINDOW_SECONDS} s.";
                ResetTimeWindowText();
                return;
            }
            if (result < MIN_TIME_WINDOW_SECONDS)
            {
                ValidationMessage = $"Time Window too small. Minimum {MIN_TIME_WINDOW_SECONDS} s.";
                ResetTimeWindowText();
                return;
            }

            ValidationMessage = "";
            TimeWindowSeconds = result;
            _lastValidTimeWindowSeconds = result;

            ClearAllDataCollections();
            ResetAllTimestamps();
            ResetAllCounters();

            UpdateChart();
        }
        else
        {
            ValidationMessage = "Time Window must be a valid positive number.";
            ResetTimeWindowText();
        }
    }




    // Valida e aggiorna l’intervallo tra le etichette sull’asse X.
    private void ValidateAndUpdateXAxisInterval(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultInterval = 5;
            ValidationMessage = "";
            XAxisLabelInterval = defaultInterval;
            _lastValidXAxisLabelInterval = defaultInterval;
            UpdateChart();
            return;
        }

        if (TryParseInt(value, out int result))
        {
            // *** AGGIUNGI QUESTO CONTROLLO ***
            if (result > MAX_X_AXIS_LABEL_INTERVAL)
            {
                ValidationMessage = $"X Labels interval too high. Maximum {MAX_X_AXIS_LABEL_INTERVAL}.";
                ResetXAxisIntervalText();
                return;
            }
            if (result < MIN_X_AXIS_LABEL_INTERVAL)
            {
                ValidationMessage = $"X Labels interval too low. Minimum {MIN_X_AXIS_LABEL_INTERVAL}.";
                ResetXAxisIntervalText();
                return;
            }

            ValidationMessage = "";
            XAxisLabelInterval = result;
            _lastValidXAxisLabelInterval = result;
            UpdateChart();
        }
        else
        {
            ValidationMessage = "X Labels interval must be a valid positive number (no letters or special characters allowed).";
            ResetXAxisIntervalText();
        }
    }


    // Metodi helper per ottenere i valori di default
    // Restituisce il valore minimo di default per l’asse Y in base al parametro selezionato.
    private double GetDefaultYAxisMin(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" => -5,
            "Low-Noise AccelerometerY" => -5,
            "Low-Noise AccelerometerZ" => -15,
            "Wide-Range AccelerometerX" => -5,
            "Wide-Range AccelerometerY" => -5,
            "Wide-Range AccelerometerZ" => -15,
            "GyroscopeX" => -250,
            "GyroscopeY" => -250,
            "GyroscopeZ" => -250,
            "MagnetometerX" => -5,
            "MagnetometerY" => -5,
            "MagnetometerZ" => -5,
            "Temperature_BMP180" => 15,
            "Pressure_BMP180" => 90,
            "BatteryVoltage" => 3.3,
            "BatteryPercent" => 0,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 0,
            _ => 0
        };
    }

    private double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" => 5,
            "Low-Noise AccelerometerY" => 5,
            "Low-Noise AccelerometerZ" => 15,
            "Wide-Range AccelerometerX" => 5,
            "Wide-Range AccelerometerY" => 5,
            "Wide-Range AccelerometerZ" => 15,
            "GyroscopeX" => 250,
            "GyroscopeY" => 250,
            "GyroscopeZ" => 250,
            "MagnetometerX" => 5,
            "MagnetometerY" => 5,
            "MagnetometerZ" => 5,
            "Temperature_BMP180" => 40,
            "Pressure_BMP180" => 110,
            "BatteryVoltage" => 4.2,
            "BatteryPercent" => 100,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 3.3,
            _ => 1
        };
    }



    // Ripristina il testo del campo YMin all’ultimo valore valido.
    private void ResetYAxisMinText()
    {
        _yAxisMinText = _lastValidYAxisMin.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
    }

    // Ripristina il testo del campo YMax all’ultimo valore valido.
    private void ResetYAxisMaxText()
    {
        _yAxisMaxText = _lastValidYAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMaxText));
    }

    // Ripristina il testo della finestra temporale all’ultimo valore valido.
    private void ResetTimeWindowText()
    {
        _timeWindowSecondsText = _lastValidTimeWindowSeconds.ToString();
        OnPropertyChanged(nameof(TimeWindowSecondsText));
    }

    // Ripristina il testo dell’intervallo X all’ultimo valore valido.
    private void ResetXAxisIntervalText()
    {
        _xAxisLabelIntervalText = _lastValidXAxisLabelInterval.ToString();
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }

    // Popola la lista dei parametri disponibili in base ai sensori attivi.
    private void InitializeAvailableParameters()
    {
        AvailableParameters.Clear();

        if (enableLowNoiseAccelerometer)
        {
            AvailableParameters.Add("Low-Noise Accelerometer"); // Gruppo principale
            AvailableParameters.Add("    → Low-Noise AccelerometerX"); // Sub-parametri con indentazione
            AvailableParameters.Add("    → Low-Noise AccelerometerY");
            AvailableParameters.Add("    → Low-Noise AccelerometerZ");
        }

        if (enableWideRangeAccelerometer)
        {
            AvailableParameters.Add("Wide-Range Accelerometer"); // Gruppo principale
            AvailableParameters.Add("    → Wide-Range AccelerometerX"); // Sub-parametri con indentazione
            AvailableParameters.Add("    → Wide-Range AccelerometerY");
            AvailableParameters.Add("    → Wide-Range AccelerometerZ");
        }

        if (enableGyroscope)
        {
            AvailableParameters.Add("Gyroscope"); // Gruppo principale
            AvailableParameters.Add("    → GyroscopeX"); // Sub-parametri con indentazione
            AvailableParameters.Add("    → GyroscopeY");
            AvailableParameters.Add("    → GyroscopeZ");
        }

        if (enableMagnetometer)
        {
            AvailableParameters.Add("Magnetometer"); // Gruppo principale
            AvailableParameters.Add("    → MagnetometerX"); // Sub-parametri con indentazione
            AvailableParameters.Add("    → MagnetometerY");
            AvailableParameters.Add("    → MagnetometerZ");
        }

        // Batteria: solo parametri singoli, NO gruppo
        if (enableBattery)
        {
            AvailableParameters.Add("BatteryVoltage");
            AvailableParameters.Add("BatteryPercent");
        }

        if (enablePressureTemperature)
        {
            AvailableParameters.Add("Temperature_BMP180");
            AvailableParameters.Add("Pressure_BMP180");
        }

        if (enableExtA6)
            AvailableParameters.Add("ExtADC_A6");
        if (enableExtA7)
            AvailableParameters.Add("ExtADC_A7");
        if (enableExtA15)
            AvailableParameters.Add("ExtADC_A15");

        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }
    }

    public string CleanParameterName(string displayName)
    {
        if (displayName.StartsWith("    → "))
        {
            return displayName.Substring(6); // Rimuove "    → "
        }
        return displayName;
    }





    private bool IsMultiChart(string parameter)
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(parameter);
        return cleanName is "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
                          "Gyroscope" or "Magnetometer";
    }


    public List<string> GetSubParameters(string groupParameter)
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(groupParameter);
        return cleanName switch
        {
            "Low-Noise Accelerometer" => new List<string> { "Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ" },
            "Wide-Range Accelerometer" => new List<string> { "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" },
            "Gyroscope" => new List<string> { "GyroscopeX", "GyroscopeY", "GyroscopeZ" },
            "Magnetometer" => new List<string> { "MagnetometerX", "MagnetometerY", "MagnetometerZ" },
            _ => new List<string>()
        };
    }






    private void ClearAllDataCollections()
    {
        // Itera direttamente sulle chiavi delle collezioni (nomi puliti)
        foreach (var key in dataPointsCollections.Keys.ToList())
        {
            dataPointsCollections[key].Clear();
        }
        foreach (var key in timeStampsCollections.Keys.ToList())
        {
            timeStampsCollections[key].Clear();
        }
    }

    private void TrimCollection(string parameter, int maxPoints)
    {
        if (dataPointsCollections.TryGetValue(parameter, out var dataList) &&
            timeStampsCollections.TryGetValue(parameter, out var timeList))
        {
            while (dataList.Count > maxPoints && timeList.Count > 0)
            {
                if (dataList.Count > 0)
                    dataList.RemoveAt(0);
                if (timeList.Count > 0)
                    timeList.RemoveAt(0);
            }
        }
    }





    public List<float> GetDataPoints(string parameter)
    {
        // Pulisce il nome del parametro prima di accedere ai dati
        string cleanName = CleanParameterName(parameter);
        return dataPointsCollections.ContainsKey(cleanName) ? dataPointsCollections[cleanName] : new List<float>();
    }

    public List<int> GetTimeStamps(string parameter)
    {
        // Pulisce il nome del parametro prima di accedere ai dati
        string cleanName = CleanParameterName(parameter);
        return timeStampsCollections.ContainsKey(cleanName) ? timeStampsCollections[cleanName] : new List<int>();
    }

    public List<string> GetCurrentSubParameters()
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(SelectedParameter);
        return IsMultiChart(cleanName) ? GetSubParameters(cleanName) : new List<string> { cleanName };
    }


    // Avvia il timer che legge e aggiorna i dati a intervalli regolari.
    public void StartTimer()
    {
        // Ferma eventuali timer esistenti
        StopTimer();

        // Calcola l'intervallo basato sul sampling rate
        double intervalMs = 1000.0 / shimmer.SamplingRate;

        // Assicurati che l'intervallo sia ragionevole
        intervalMs = Math.Max(intervalMs, 10);   // Minimo 10ms (100Hz max)
        intervalMs = Math.Min(intervalMs, 1000); // Massimo 1000ms (1Hz min)

        timer = new System.Timers.Timer(intervalMs);
        timer.Elapsed += OnTimerElapsed;
        timer.Start();

        System.Diagnostics.Debug.WriteLine($"Timer started with interval: {intervalMs}ms for sampling rate: {shimmer.SamplingRate}Hz");
    }





    // Metodo chiamato ogni volta che scatta il timer: aggiorna i dati e il grafico.
    // 1. First, modify the OnTimerElapsed method to handle battery data safely
    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var sample = shimmer.LatestData;
            if (sample == null) return;

            sampleCounter++;

            double currentTimeSeconds = sampleCounter / shimmer.SamplingRate;

            UpdateDataCollectionsWithSingleSample(sample, currentTimeSeconds);

            UpdateChart();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnTimerElapsed: {ex.Message}");
        }
    }


    private void UpdateDataCollectionsWithSingleSample(dynamic sample, double currentTimeSeconds)
    {
        var values = new Dictionary<string, float>();


        try
        {
            if (enableLowNoiseAccelerometer)
            {
                values["Low-Noise AccelerometerX"] = (float)sample.LowNoiseAccelerometerX.Data;
                values["Low-Noise AccelerometerY"] = (float)sample.LowNoiseAccelerometerY.Data;
                values["Low-Noise AccelerometerZ"] = (float)sample.LowNoiseAccelerometerZ.Data;
            }

            if (enableWideRangeAccelerometer)
            {
                values["Wide-Range AccelerometerX"] = (float)sample.WideRangeAccelerometerX.Data;
                values["Wide-Range AccelerometerY"] = (float)sample.WideRangeAccelerometerY.Data;
                values["Wide-Range AccelerometerZ"] = (float)sample.WideRangeAccelerometerZ.Data;
            }

            if (enableGyroscope)
            {
                values["GyroscopeX"] = (float)sample.GyroscopeX.Data;
                values["GyroscopeY"] = (float)sample.GyroscopeY.Data;
                values["GyroscopeZ"] = (float)sample.GyroscopeZ.Data;
            }

            if (enableMagnetometer)
            {
                values["MagnetometerX"] = (float)sample.MagnetometerX.Data;
                values["MagnetometerY"] = (float)sample.MagnetometerY.Data;
                values["MagnetometerZ"] = (float)sample.MagnetometerZ.Data;
            }

            if (enablePressureTemperature)
            {
                values["Temperature_BMP180"] = (float)sample.Temperature_BMP180.Data;
                values["Pressure_BMP180"] = (float)sample.Pressure_BMP180.Data;
            }

            if (enableBattery && sample.BatteryVoltage != null)
            {
                values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data / 1000f; // mV → V
                float batteryV = values["BatteryVoltage"];
                float percent;
                if (batteryV <= 3.3f)
                    percent = 0;
                else if (batteryV >= 4.2f)
                    percent = 100;
                else if (batteryV <= 4.10f)
                    percent = (batteryV - 3.3f) / (4.10f - 3.3f) * 97f;
                else
                    percent = 97f + (batteryV - 4.10f) / (4.20f - 4.10f) * 3f;
                values["BatteryPercent"] = Math.Clamp(percent, 0, 100);
            }


            if (enableExtA6)
                values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data / 1000f;
            if (enableExtA7)
                values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data / 1000f;
            if (enableExtA15)
                values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data / 1000f;


        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
            return;
        }

        lock (_dataLock)
        {

            // Convert current time to milliseconds for timestamp
            int timestampMs = (int)Math.Round(currentTimeSeconds * 1000);

            // Calculate max points based on current sampling rate
            var maxPoints = (int)(TimeWindowSeconds * shimmer.SamplingRate);

            // Update each collection with the new sample
            var parametersSnapshot = AvailableParameters.ToList();
            foreach (var parameter in parametersSnapshot)
            {
                string cleanName = CleanParameterName(parameter);
                if (values.ContainsKey(cleanName))
                {
                    dataPointsCollections[cleanName].Add(values[cleanName]);
                    timeStampsCollections[cleanName].Add(timestampMs);

                    // Mantieni solo TimeWindowSeconds * samplingRate punti
                    TrimCollection(cleanName, maxPoints);
                }
            }
        }


        // Se l'asse Y è in modalità automatica, ricalcola il range
        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();

            // Aggiorna solo se i valori sono cambiati significativamente
            if (Math.Abs(YAxisMin - _autoYAxisMin) > 0.01 || Math.Abs(YAxisMax - _autoYAxisMax) > 0.01)
            {
                YAxisMin = _autoYAxisMin;
                YAxisMax = _autoYAxisMax;

                // Aggiorna i testi solo se necessario
                UpdateTextProperties();
            }
        }
    }

    public (List<float> data, List<int> time) GetSeriesSnapshot(string parameter)
    {
        lock (_dataLock)
        {
            string cleanName = CleanParameterName(parameter);
            return (
                dataPointsCollections.ContainsKey(cleanName) ? new List<float>(dataPointsCollections[cleanName]) : new List<float>(),
                timeStampsCollections.ContainsKey(cleanName) ? new List<int>(timeStampsCollections[cleanName]) : new List<int>()
            );
        }
    }




    // Genera un evento per aggiornare il grafico.
    private void UpdateChart()
    {
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    // Gestisce il cambio di parametro selezionato e aggiorna le impostazioni asse Y.
    partial void OnSelectedParameterChanged(string value)
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(value);

        // Determina la modalità di visualizzazione
        ChartDisplayMode = IsMultiChart(cleanName) ? ChartDisplayMode.Multi : ChartDisplayMode.Single;

        UpdateYAxisSettings(value); // Passa il valore originale che viene pulito dentro il metodo

        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;

        UpdateTextProperties();
        IsXAxisLabelIntervalEnabled = cleanName != "HeartRate";
        ValidationMessage = "";

        UpdateChart();
    }



    // Imposta etichette e limiti asse Y in base al parametro selezionato.
    private void UpdateYAxisSettings(string parameter)
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(parameter);

        switch (cleanName)
        {
            case "Low-Noise Accelerometer":
                YAxisLabel = "Low-Noise Accelerometer";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer (X,Y,Z)";
                YAxisMin = -20;
                YAxisMax = 20;
                break;
            case "Wide-Range Accelerometer":
                YAxisLabel = "Wide-Range Accelerometer";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer (X,Y,Z)";
                YAxisMin = -20;
                YAxisMax = 20;
                break;
            case "Gyroscope":
                YAxisLabel = "Gyroscope";
                YAxisUnit = "deg/s";
                ChartTitle = "Real-time Gyroscope (X,Y,Z)";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "Magnetometer":
                YAxisLabel = "Magnetometer";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer (X,Y,Z)";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Low-Noise AccelerometerX":
                YAxisLabel = "Low-Noise Accelerometer X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer X";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Low-Noise AccelerometerY":
                YAxisLabel = "Low-Noise Accelerometer Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Low-Noise AccelerometerZ":
                YAxisLabel = "Low-Noise Accelerometer Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer Z";
                YAxisMin = -15;
                YAxisMax = 15;
                break;
            case "Wide-Range AccelerometerX":
                YAxisLabel = "Wide-Range Accelerometer X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer X";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Wide-Range AccelerometerY":
                YAxisLabel = "Wide-Range Accelerometer Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Wide-Range AccelerometerZ":
                YAxisLabel = "Wide-Range Accelerometer Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Z";
                YAxisMin = -15;
                YAxisMax = 15;
                break;
            case "GyroscopeX":
                YAxisLabel = "Gyroscope X";
                YAxisUnit = "deg/s";
                ChartTitle = "Real-time Gyroscope X";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "GyroscopeY":
                YAxisLabel = "Gyroscope Y";
                YAxisUnit = "deg/s";
                ChartTitle = "Real-time Gyroscope Y";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "GyroscopeZ":
                YAxisLabel = "Gyroscope Z";
                YAxisUnit = "deg/s";
                ChartTitle = "Real-time Gyroscope Z";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "MagnetometerX":
                YAxisLabel = "Magnetometer X";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer X";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "MagnetometerY":
                YAxisLabel = "Magnetometer Y";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "MagnetometerZ":
                YAxisLabel = "Magnetometer Z";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Z";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Temperature_BMP180":
                YAxisLabel = "Temperature";
                YAxisUnit = "°C";
                ChartTitle = "BMP180 Temperature";
                YAxisMin = 15;
                YAxisMax = 40;
                break;
            case "Pressure_BMP180":
                YAxisLabel = "Pressure";
                YAxisUnit = "kPa";
                ChartTitle = "BMP180 Pressure";
                YAxisMin = 90;
                YAxisMax = 110;
                break;
            case "BatteryVoltage":
                YAxisLabel = "Battery Voltage";
                YAxisUnit = "V";
                ChartTitle = "Real-time Battery Voltage";
                YAxisMin = 3.3;
                YAxisMax = 4.2;
                break;
            case "BatteryPercent":
                YAxisLabel = "Battery Percent";
                YAxisUnit = "%";
                ChartTitle = "Real-time Battery Percentage";
                YAxisMin = 0;
                YAxisMax = 100;
                break;
            case "ExtADC_A6":
                YAxisLabel = "External ADC A6";
                YAxisUnit = "V";
                ChartTitle = "External ADC A6";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
            case "ExtADC_A7":
                YAxisLabel = "External ADC A7";
                YAxisUnit = "V";
                ChartTitle = "External ADC A7";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
            case "ExtADC_A15":
                YAxisLabel = "External ADC A15";
                YAxisUnit = "V";
                ChartTitle = "External ADC A15";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
        }
    }




    // Cerca di convertire una stringa in un numero double valido.
    public static bool TryParseDouble(string input, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleanInput = input.Trim();
        if (string.IsNullOrEmpty(cleanInput))
            return false;

        // Casi speciali: permettere solo "-" o "+" come input parziale
        if (cleanInput == "-" || cleanInput == "+")
        {
            result = 0; // Valore temporaneo
            return true; // Consideriamo valido per permettere la digitazione
        }

        // Controlla caratteri validi e posizione del segno
        for (int i = 0; i < cleanInput.Length; i++)
        {
            char c = cleanInput[i];

            // Il segno meno o più può essere solo al primo carattere
            if (c == '-' || c == '+')
            {
                if (i != 0) // Se non è il primo carattere, non è valido
                    return false;
            }
            else if (c == '.' || c == ',')
            {
                // Il separatore decimale è valido
                continue;
            }
            else if (!char.IsDigit(c))
            {
                // Carattere non valido
                return false;
            }
        }

        // Prova a parsare usando sia il punto che la virgola come separatore decimale
        return double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
               double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }

    // Cerca di convertire una stringa in un numero intero valido.
    public static bool TryParseInt(string input, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleanInput = input.Trim();
        if (string.IsNullOrEmpty(cleanInput))
            return false;

        // Controlla caratteri validi per interi
        foreach (char c in cleanInput)
        {
            if (!char.IsDigit(c) && c != '-' && c != '+')
                return false;
        }

        return int.TryParse(cleanInput, out result);
    }

    

 


    // Metodo per ottenere la configurazione corrente
    // Restituisce un oggetto che rappresenta la configurazione attuale dei sensori.
    public ShimmerDevice GetCurrentSensorConfiguration()
    {
        return new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = enableLowNoiseAccelerometer,
            EnableWideRangeAccelerometer = enableWideRangeAccelerometer,
            EnableGyroscope = enableGyroscope,
            EnableMagnetometer = enableMagnetometer,
            EnablePressureTemperature = enablePressureTemperature,
            EnableBattery = enableBattery,
            EnableExtA6 = enableExtA6,
            EnableExtA7 = enableExtA7,
            EnableExtA15 = enableExtA15

        };
    }

    public void ResetAllTimestamps()
    {
        lock (_dataLock)
        {
            foreach (var param in timeStampsCollections.Keys.ToList())
            {
                int count = timeStampsCollections[param].Count;
                for (int i = 0; i < count; i++)
                {
                    // Timestamp in millisecondi, come in acquisizione regolare
                    timeStampsCollections[param][i] = (int)(i * (1000.0 / shimmer.SamplingRate));
                }
            }
        }
    }


    public void StopTimer()
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Elapsed -= OnTimerElapsed;
            timer.Dispose();
            timer = null;
        }
    }


    


}
