// ViewModel for the DataPage. It manages real-time data acquisition from a Shimmer device,
// input validation, and UI chart updates using SkiaSharp.

using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI;
using XR2Learn_ShimmerAPI.IMU;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;



namespace ShimmerInterface.ViewModels;

public enum TimeDisplayMode
{
    Seconds,
    Clock
}

public enum ChartDisplayMode
{
    Single,
    Multi
}

public partial class DataPageViewModel : ObservableObject
{
    // Device reference from the Shimmer API
    private readonly XR2Learn_ShimmerIMU shimmer;

    // Timer that triggers data updates every second
    private System.Timers.Timer? timer;


    // Dizionario che memorizza i dati delle serie temporali per ciascun parametro (X/Y/Z, GSR, PPG...)
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();

    // Elapsed seconds since data collection started
    private int secondsElapsed = 0;
    private int sampleCounter = 0;

    private DateTime startTime = DateTime.Now;

    // Sensor enable flags (set from SensorConfiguration passed in constructor)
    private bool enableAccelerometer;
    private bool enableWideRangeAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;
    private bool enablePressureTemperature;
    private bool enableBattery;
    private bool enableExtA6;
    private bool enableExtA7;
    private bool enableExtA15;


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

    // Testo mostrato sopra il grafico che riassume i valori dei sensori in tempo reale.
    // Viene aggiornato ogni secondo con i nuovi dati letti dal dispositivo Shimmer.
    [ObservableProperty]
    private string sensorText = "Waiting for data...";

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
    private TimeDisplayMode timeDisplayMode = TimeDisplayMode.Seconds;

    [ObservableProperty]
    private bool showGrid = true;

    [ObservableProperty]
    private bool autoYAxis = false;

    // Proprietà per abilitare/disabilitare i campi manuali Y
    [ObservableProperty]
    private bool isYAxisManualEnabled = true;

    [ObservableProperty]
    private ChartDisplayMode chartDisplayMode = ChartDisplayMode.Single;


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

    public ObservableCollection<string> AvailableParameters { get; } = new();

    public event EventHandler? ChartUpdateRequested;

    public DataPageViewModel(XR2Learn_ShimmerIMU shimmerDevice, SensorConfiguration config)
    {
        shimmer = shimmerDevice;
        enableAccelerometer = config.EnableAccelerometer;
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

        foreach (var parameter in AvailableParameters)
        {
            dataPointsCollections[parameter] = new List<float>();
            timeStampsCollections[parameter] = new List<int>();
        }

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

        startTime = DateTime.Now;

        UpdateTextProperties();

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
        if (IsMultiChart(SelectedParameter))
        {
            var subParams = GetSubParameters(SelectedParameter);
            var allValues = new List<float>();

            foreach (var param in subParams)
            {
                if (dataPointsCollections.ContainsKey(param) && dataPointsCollections[param].Count > 0)
                {
                    allValues.AddRange(dataPointsCollections[param]);
                }
            }

            if (allValues.Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(SelectedParameter);
                _autoYAxisMax = GetDefaultYAxisMax(SelectedParameter);
                return;
            }

            var min = allValues.Min();
            var max = allValues.Max();

            if (Math.Abs(max - min) < 0.001)
            {
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {
                var range = max - min;
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }
        else
        {
            // Logica esistente per parametri singoli...
            if (!dataPointsCollections.ContainsKey(SelectedParameter) ||
                dataPointsCollections[SelectedParameter].Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(SelectedParameter);
                _autoYAxisMax = GetDefaultYAxisMax(SelectedParameter);
                return;
            }

            var data = dataPointsCollections[SelectedParameter];
            var min = data.Min();
            var max = data.Max();

            if (Math.Abs(max - min) < 0.001)
            {
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {
                var range = max - min;
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }
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


    // Modifica i metodi di validazione per gestire campi vuoti con valori di default

    // Valida e aggiorna il valore minimo dell'asse Y.
    private void ValidateAndUpdateYAxisMin(string value)
    {
        // Se siamo in modalità automatica, ignora gli input manuali
        if (AutoYAxis)
            return;

        // Resto del metodo rimane uguale...
        // Se il campo è vuoto, usa il valore di default ma lascia il campo vuoto
        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMin = GetDefaultYAxisMin(SelectedParameter);
            ValidationMessage = "";
            YAxisMin = defaultMin;
            _lastValidYAxisMin = defaultMin;
            UpdateChart();
            return;
        }

        // Permetti input parziali come "-" o "+"
        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        if (TryParseDouble(value, out double result))
        {
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



    // Valida e aggiorna il valore massimo dell'asse Y.
    private void ValidateAndUpdateYAxisMax(string value)
    {
        // Se siamo in modalità automatica, ignora gli input manuali
        if (AutoYAxis)
            return;

        // Resto del metodo rimane uguale...
        // Se il campo è vuoto, usa il valore di default ma lascia il campo vuoto
        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMax = GetDefaultYAxisMax(SelectedParameter);
            ValidationMessage = "";
            YAxisMax = defaultMax;
            _lastValidYAxisMax = defaultMax;
            UpdateChart();
            return;
        }

        // Permetti input parziali come "-" o "+"
        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        if (TryParseDouble(value, out double result))
        {
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


    // Modifica il metodo ValidateAndUpdateSamplingRate
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
            if (result <= 0)
            {
                ValidationMessage = "Sampling rate must be greater than 0 Hz.";
                ResetSamplingRateText();
                return;
            }

            // Limita il sampling rate a valori ragionevoli
            if (result > 1000)
            {
                ValidationMessage = "Sampling rate too high. Maximum 1000 Hz.";
                ResetSamplingRateText();
                return;
            }

            if (result < 0.1)
            {
                ValidationMessage = "Sampling rate too low. Minimum 0.1 Hz.";
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

    private void ClearAllDataCollections()
    {
        foreach (var parameter in AvailableParameters)
        {
            if (dataPointsCollections.ContainsKey(parameter))
            {
                dataPointsCollections[parameter].Clear();
            }
            if (timeStampsCollections.ContainsKey(parameter))
            {
                timeStampsCollections[parameter].Clear();
            }
        }
    }

    private void ResetAllCounters()
    {
        sampleCounter = 0;
        secondsElapsed = 0;
        startTime = DateTime.Now;
    }


    private void RestartTimerWithNewSamplingRate()
    {
        // Questo metodo ora è sostituito da UpdateSamplingRateAndRestart
        // Mantienilo per compatibilità se usato altrove
        UpdateSamplingRateAndRestart(shimmer.SamplingRate);
    }


    // Nuovo metodo per ricalcolare i punti massimi
    private void RecalculateMaxPointsForAllCollections()
    {
        var maxPoints = (int)(TimeWindowSeconds * shimmer.SamplingRate);

        foreach (var parameter in AvailableParameters)
        {
            if (dataPointsCollections.ContainsKey(parameter) &&
                timeStampsCollections.ContainsKey(parameter))
            {
                // Taglia la collezione se ha troppi punti
                while (dataPointsCollections[parameter].Count > maxPoints)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                    timeStampsCollections[parameter].RemoveAt(0);
                }
            }
        }
    }


    public void ResetSampleCounter()
    {
        sampleCounter = 0;
        secondsElapsed = 0;
    }


    private void ResetSamplingRateText()
    {
        _samplingRateText = _lastValidSamplingRate.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SamplingRateText));
    }




    // Valida e aggiorna la finestra temporale in secondi.
    private void ValidateAndUpdateTimeWindow(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultTimeWindow = 20;
            ValidationMessage = "";
            TimeWindowSeconds = defaultTimeWindow;
            _lastValidTimeWindowSeconds = defaultTimeWindow;

            // Taglia tutte le collezioni alla nuova dimensione
            var maxPoints = (int)(defaultTimeWindow * shimmer.SamplingRate);
            foreach (var parameter in AvailableParameters)
            {
                while (dataPointsCollections[parameter].Count > maxPoints)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                    timeStampsCollections[parameter].RemoveAt(0);
                }
            }
            UpdateChart();
            return;
        }

        if (TryParseInt(value, out int result))
        {
            if (result <= 0)
            {
                ValidationMessage = "Time Window must be greater than 0 seconds.";
                ResetTimeWindowText();
                return;
            }

            ValidationMessage = "";
            TimeWindowSeconds = result;
            _lastValidTimeWindowSeconds = result;

            // Taglia tutte le collezioni alla nuova dimensione basata sul sampling rate
            var maxPoints = (int)(result * shimmer.SamplingRate);
            foreach (var parameter in AvailableParameters)
            {
                while (dataPointsCollections[parameter].Count > maxPoints)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                    timeStampsCollections[parameter].RemoveAt(0);
                }
            }
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
        // Se il campo è vuoto, usa il valore di default ma lascia il campo vuoto
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultInterval = 5;
            ValidationMessage = "";
            XAxisLabelInterval = defaultInterval;
            _lastValidXAxisLabelInterval = defaultInterval;

            // Non aggiornare il testo - lascia il campo vuoto

            UpdateChart();
            return;
        }

        if (TryParseInt(value, out int result))
        {
            if (result <= 0)
            {
                ValidationMessage = "X Labels interval must be greater than 0.";
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
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => -5,
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => -20,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => -250,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => -2,
            "Temperature_BMP180" => 20,         
            "Pressure_BMP180" => 80,
            "Battery Voltage" => 3300,
            "Battery Percent" => 0,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 0,
            _ => 0
        };
    }


    // Restituisce il valore massimo di default per l’asse Y in base al parametro selezionato.
    private double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => 5,
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => 20,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => 250,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => 2,
            "Temperature_BMP180" => 50,
            "Pressure_BMP180" => 110,
            "Battery Voltage" => 4200,
            "Battery Percent" => 100,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 3000,
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

        if (enableAccelerometer)
        {
            AvailableParameters.Add("Low-Noise Accelerometer"); // Gruppo
            AvailableParameters.Add("Low-Noise AccelerometerX");
            AvailableParameters.Add("Low-Noise AccelerometerY");
            AvailableParameters.Add("Low-Noise AccelerometerZ");
        }

        if (enableWideRangeAccelerometer)
        {
            AvailableParameters.Add("Wide-Range Accelerometer"); // Gruppo
            AvailableParameters.Add("Wide-Range AccelerometerX");
            AvailableParameters.Add("Wide-Range AccelerometerY");
            AvailableParameters.Add("Wide-Range AccelerometerZ");
        }

        if (enableGyroscope)
        {
            AvailableParameters.Add("Gyroscope"); // Gruppo
            AvailableParameters.Add("GyroscopeX");
            AvailableParameters.Add("GyroscopeY");
            AvailableParameters.Add("GyroscopeZ");
        }

        if (enableMagnetometer)
        {
            AvailableParameters.Add("Magnetometer"); // Gruppo
            AvailableParameters.Add("MagnetometerX");
            AvailableParameters.Add("MagnetometerY");
            AvailableParameters.Add("MagnetometerZ");
        }

        if (enableBattery)
        {
            AvailableParameters.Add("Battery"); // Gruppo
            AvailableParameters.Add("BatteryVoltage");
            AvailableParameters.Add("BatteryPercent");
        }

        // Resto invariato...
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

    private bool IsMultiChart(string parameter)
    {
        return parameter is "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
                          "Gyroscope" or "Magnetometer" or "Battery";
    }

    private List<string> GetSubParameters(string groupParameter)
    {
        return groupParameter switch
        {
            "Low-Noise Accelerometer" => new List<string> { "Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ" },
            "Wide-Range Accelerometer" => new List<string> { "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" },
            "Gyroscope" => new List<string> { "GyroscopeX", "GyroscopeY", "GyroscopeZ" },
            "Magnetometer" => new List<string> { "MagnetometerX", "MagnetometerY", "MagnetometerZ" },
            "Battery" => new List<string> { "BatteryVoltage", "BatteryPercent" },
            _ => new List<string>()
        };
    }


    // Restituisce true se il sensore associato al parametro è abilitato.
    private bool IsSensorEnabled(string parameter)
    {
        return parameter switch
        {
            // Gruppi multi-parametro
            "Low-Noise Accelerometer" => enableAccelerometer,
            "Wide-Range Accelerometer" => enableWideRangeAccelerometer,
            "Gyroscope" => enableGyroscope,
            "Magnetometer" => enableMagnetometer,
            "Battery" => enableBattery,

            // Parametri singoli (esistenti)
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => enableAccelerometer,
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => enableWideRangeAccelerometer,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => enableGyroscope,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => enableMagnetometer,
            "Temperature_BMP180" or "Pressure_BMP180" => enablePressureTemperature,
            "BatteryVoltage" or "BatteryPercent" => enableBattery,
            "ExtADC_A6" => enableExtA6,
            "ExtADC_A7" => enableExtA7,
            "ExtADC_A15" => enableExtA15,
            _ => false
        };
    }

    public List<float> GetDataPoints(string parameter)
    {
        return dataPointsCollections.ContainsKey(parameter) ? dataPointsCollections[parameter] : new List<float>();
    }

    public List<int> GetTimeStamps(string parameter)
    {
        return timeStampsCollections.ContainsKey(parameter) ? timeStampsCollections[parameter] : new List<int>();
    }

    public List<string> GetCurrentSubParameters()
    {
        return IsMultiChart(SelectedParameter) ? GetSubParameters(SelectedParameter) : new List<string> { SelectedParameter };
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


    private void RestartTimer()
    {
        StopTimer();
        StartTimer();
    }



    // Metodo chiamato ogni volta che scatta il timer: aggiorna i dati e il grafico.
    // 1. First, modify the OnTimerElapsed method to handle battery data safely
    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            // Get single sample instead of collecting for a full second
            var sample = shimmer.LatestData;
            if (sample == null) return;

            // Increment sample counter
            sampleCounter++;

            // Calculate current time in seconds (fractional)
            double currentTimeSeconds = sampleCounter / shimmer.SamplingRate;

            // Update sensor text display every second (approximately)
            int samplesPerSecond = (int)Math.Round(shimmer.SamplingRate);
            if (samplesPerSecond > 0 && sampleCounter % samplesPerSecond == 0)
            {
                secondsElapsed = (int)Math.Floor(currentTimeSeconds);
                UpdateSensorTextDisplay(sample);
            }

            // Update data collections with single sample
            UpdateDataCollectionsWithSingleSample(sample, currentTimeSeconds);

            // Update chart
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
            if (enableAccelerometer)
            {
                values["Low-Noise AccelerometerX"] = (float)sample.AccelerometerX.Data;
                values["Low-Noise AccelerometerY"] = (float)sample.AccelerometerY.Data;
                values["Low-Noise AccelerometerZ"] = (float)sample.AccelerometerZ.Data;
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
                values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data;
                values["BatteryPercent"] = (float)Math.Clamp(
                    ((sample.BatteryVoltage.Data - 3300) / 900) * 100, 0, 100);
            }

            if (enableExtA6)
                values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data;
            if (enableExtA7)
                values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data;
            if (enableExtA15)
                values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data;

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
            return;
        }

        // Convert current time to milliseconds for timestamp
        int timestampMs = (int)Math.Round(currentTimeSeconds * 1000);

        // Calculate max points based on current sampling rate
        var maxPoints = (int)(TimeWindowSeconds * shimmer.SamplingRate);

        // Update each collection with the new sample
        foreach (var parameter in AvailableParameters)
        {
            if (values.ContainsKey(parameter))
            {
                dataPointsCollections[parameter].Add(values[parameter]);
                timeStampsCollections[parameter].Add(timestampMs);

                // Maintain time window by removing old samples
                while (dataPointsCollections[parameter].Count > maxPoints)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                    timeStampsCollections[parameter].RemoveAt(0);
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






    private void UpdateAllDataCollectionsWithAllSamples(List<dynamic> samples)
    {
        foreach (var sample in samples)
        {
            var values = new Dictionary<string, float>();

            try
            {
                if (enableAccelerometer)
                {
                    values["Low-Noise AccelerometerX"] = (float)sample.AccelerometerX.Data;
                    values["Low-Noise AccelerometerY"] = (float)sample.AccelerometerY.Data;
                    values["Low-Noise AccelerometerZ"] = (float)sample.AccelerometerZ.Data;
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
                    values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data;
                    values["BatteryPercent"] = (float)Math.Clamp(
                        ((sample.BatteryVoltage.Data - 3300) / 900) * 100, 0, 100);
                }
                if (enableExtA6)
                    values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data;
                if (enableExtA7)
                    values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data;
                if (enableExtA15)
                    values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data;

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
                continue;
            }

            // Calcola il timestamp per questo campione specifico
            var sampleIndex = samples.IndexOf(sample);
            var fractionalTime = secondsElapsed + (double)sampleIndex / samples.Count;

            // Aggiorna ogni collezione SOLO se c'è un valore valido
            foreach (var parameter in AvailableParameters)
            {
                if (values.ContainsKey(parameter))
                {
                    dataPointsCollections[parameter].Add(values[parameter]);
                    timeStampsCollections[parameter].Add((int)Math.Round(fractionalTime * 1000));

                    // Mantieni solo i punti degli ultimi TimeWindowSeconds
                    var maxPoints = (int)(TimeWindowSeconds * shimmer.SamplingRate);
                    while (dataPointsCollections[parameter].Count > maxPoints)
                    {
                        dataPointsCollections[parameter].RemoveAt(0);
                        timeStampsCollections[parameter].RemoveAt(0);
                    }
                }
            }
        }
    }


    private void DrawOscilloscopeGrid(SKCanvas canvas, float leftMargin, float topMargin, float graphWidth, float graphHeight)
    {
        if (!ShowGrid) return;

        float right = leftMargin + graphWidth;
        float bottom = topMargin + graphHeight;

        int horizontalDivisions = 4;
        int verticalDivisions = TimeWindowSeconds; // Una divisione ogni secondo

        using var majorGridPaint = new SKPaint
        {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
        };

        using var minorGridPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0)
        };

        using var labelPaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 12,
            IsAntialias = true
        };

        // Griglia orizzontale (asse Y)
        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float y = bottom - (i * graphHeight / horizontalDivisions);
            canvas.DrawLine(leftMargin, y, right, y, majorGridPaint);


        }

        // Griglia verticale (asse X) 
        for (int i = 0; i <= verticalDivisions; i++)
        {
            float x = leftMargin + (i * graphWidth / verticalDivisions);
            canvas.DrawLine(x, topMargin, x, bottom, majorGridPaint);


        }
    }

    private void UpdateSensorTextDisplay(dynamic sample)
    {
        // Battery info
        string batteryText = "";
        if (enableBattery && sample.BatteryVoltage != null)
        {
            float batteryPercent = (float)Math.Clamp(
                ((sample.BatteryVoltage.Data - 3300) / 900) * 100, 0, 100);

            batteryText = $"\nBattery: {sample.BatteryVoltage.Data} [{sample.BatteryVoltage.Unit}] " +
                          $"({batteryPercent:F1}%)";
        }

        // External ADC Info
        string adcText = "";
        if (enableExtA6)
            adcText += $"\nExt A6: {sample.ExtADC_A6.Data} [{sample.ExtADC_A6.Unit}]";
        if (enableExtA7)
            adcText += $"\nExt A7: {sample.ExtADC_A7.Data} [{sample.ExtADC_A7.Unit}]";
        if (enableExtA15)
            adcText += $"\nExt A15: {sample.ExtADC_A15.Data} [{sample.ExtADC_A15.Unit}]";

        string pressureText = "";
        if (enablePressureTemperature)
        {
            pressureText = $"\nTemperature: {sample.Temperature_BMP180.Data} [{sample.Temperature_BMP180.Unit}]" +
                           $"\nPressure: {sample.Pressure_BMP180.Data} [{sample.Pressure_BMP180.Unit}]";
        }

        // Build complete sensor text
        SensorText =
            $"[{sample.TimeStamp.Data}]\n" +
            $"Low-Noise Accelerometer: {sample.AccelerometerX.Data} [{sample.AccelerometerX.Unit}] | " +
            $"{sample.AccelerometerY.Data} [{sample.AccelerometerY.Unit}] | " +
            $"{sample.AccelerometerZ.Data} [{sample.AccelerometerZ.Unit}]\n" +
            $"Wide-Range Accel: {sample.WideRangeAccelerometerX.Data} [{sample.WideRangeAccelerometerX.Unit}] | " +
            $"{sample.WideRangeAccelerometerY.Data} [{sample.WideRangeAccelerometerY.Unit}] | " +
            $"{sample.WideRangeAccelerometerZ.Data} [{sample.WideRangeAccelerometerZ.Unit}]\n" +
            $"Gyroscope: {sample.GyroscopeX.Data} [{sample.GyroscopeX.Unit}] | " +
            $"{sample.GyroscopeY.Data} [{sample.GyroscopeY.Unit}] | " +
            $"{sample.GyroscopeZ.Data} [{sample.GyroscopeZ.Unit}]\n" +
            $"Magnetometer: {sample.MagnetometerX.Data} [{sample.MagnetometerX.Unit}] | " +
            $"{sample.MagnetometerY.Data} [{sample.MagnetometerY.Unit}] | " +
            $"{sample.MagnetometerZ.Data} [{sample.MagnetometerZ.Unit}]" +
            pressureText +
            batteryText +
            adcText;
    }


    // Aggiungi questo nuovo metodo per raccogliere i campioni:
    private List<dynamic> CollectSamplesForLastSecond()
    {
        var samples = new List<dynamic>();
        var samplesPerSecond = (int)Math.Round(shimmer.SamplingRate);

        // Raccogli i campioni per l'ultimo secondo
        for (int i = 0; i < samplesPerSecond; i++)
        {
            var data = shimmer.LatestData;
            if (data != null)
            {
                samples.Add(data);
            }

            // Piccola pausa per rispettare il sampling rate
            System.Threading.Thread.Sleep((int)(1000.0 / shimmer.SamplingRate));
        }

        return samples;
    }

    private string FormatTimeLabel(int timeValue)
    {
        return TimeDisplayMode switch
        {
            TimeDisplayMode.Clock => startTime.AddSeconds(timeValue).ToString("HH:mm:ss"),
            _ => timeValue.ToString() + "s"
        };
    }

    // Aggiungi questo metodo per calcolare i valori mediati:
    // Replace the existing CalculateAveragedData method with this fixed version:
    private dynamic CalculateAveragedData(List<dynamic> samples)
    {
        if (samples.Count == 0) return null;

        // Calculate averages for standard sensors
        var avgAccelX = samples.Average(s => (double)s.AccelerometerX.Data);
        var avgAccelY = samples.Average(s => (double)s.AccelerometerY.Data);
        var avgAccelZ = samples.Average(s => (double)s.AccelerometerZ.Data);

        var avgGyroX = samples.Average(s => (double)s.GyroscopeX.Data);
        var avgGyroY = samples.Average(s => (double)s.GyroscopeY.Data);
        var avgGyroZ = samples.Average(s => (double)s.GyroscopeZ.Data);

        var avgMagX = samples.Average(s => (double)s.MagnetometerX.Data);
        var avgMagY = samples.Average(s => (double)s.MagnetometerY.Data);
        var avgMagZ = samples.Average(s => (double)s.MagnetometerZ.Data);

        var avgTemp = samples.Average(s => (double)s.Temperature_BMP180.Data);
        var avgPress = samples.Average(s => (double)s.Pressure_BMP180.Data);


        var avgWideAccX = samples.Average(s => (double)s.WideRangeAccelerometerX.Data);
        var avgWideAccY = samples.Average(s => (double)s.WideRangeAccelerometerY.Data);
        var avgWideAccZ = samples.Average(s => (double)s.WideRangeAccelerometerZ.Data);

        var avgExtA6 = samples.Average(s => (double)s.ExtADC_A6.Data);
        var avgExtA7 = samples.Average(s => (double)s.ExtADC_A7.Data);
        var avgExtA15 = samples.Average(s => (double)s.ExtADC_A15.Data);


        // 🪫 Battery average (senza reflection)
        double avgBatteryVoltage = 0;
        string batteryUnit = "mV";

        double avgBatteryPercent = 0;

        if (enableBattery && samples.Count > 0)
        {
            avgBatteryVoltage = samples.Average(s => (double)s.BatteryVoltage.Data);
            avgBatteryPercent = Math.Clamp((avgBatteryVoltage - 3300) / 900 * 100, 0, 100);
            batteryUnit = samples.First().BatteryVoltage.Unit;
        }

        // Create the result object
        var result = new
        {
            TimeStamp = samples.Last().TimeStamp,
            AccelerometerX = new { Data = avgAccelX, Unit = samples.First().AccelerometerX.Unit },
            AccelerometerY = new { Data = avgAccelY, Unit = samples.First().AccelerometerY.Unit },
            AccelerometerZ = new { Data = avgAccelZ, Unit = samples.First().AccelerometerZ.Unit },
            GyroscopeX = new { Data = avgGyroX, Unit = samples.First().GyroscopeX.Unit },
            GyroscopeY = new { Data = avgGyroY, Unit = samples.First().GyroscopeY.Unit },
            GyroscopeZ = new { Data = avgGyroZ, Unit = samples.First().GyroscopeZ.Unit },
            MagnetometerX = new { Data = avgMagX, Unit = samples.First().MagnetometerX.Unit },
            MagnetometerY = new { Data = avgMagY, Unit = samples.First().MagnetometerY.Unit },
            MagnetometerZ = new { Data = avgMagZ, Unit = samples.First().MagnetometerZ.Unit },
            WideRangeAccelerometerX = new { Data = avgWideAccX, Unit = samples.First().WideRangeAccelerometerX.Unit },
            WideRangeAccelerometerY = new { Data = avgWideAccY, Unit = samples.First().WideRangeAccelerometerY.Unit },
            WideRangeAccelerometerZ = new { Data = avgWideAccZ, Unit = samples.First().WideRangeAccelerometerZ.Unit },
            Temperature_BMP180 = new { Data = avgTemp, Unit = samples.First().Temperature_BMP180.Unit },
            Pressure_BMP180 = new { Data = avgPress, Unit = samples.First().Pressure_BMP180.Unit },
            BatteryVoltage = new { Data = avgBatteryVoltage, Unit = batteryUnit },
            BatteryPercent = new { Data = avgBatteryPercent, Unit = "%" },
            ExtADC_A6 = new { Data = avgExtA6, Unit = samples.First().ExtADC_A6.Unit },
            ExtADC_A7 = new { Data = avgExtA7, Unit = samples.First().ExtADC_A7.Unit },
            ExtADC_A15 = new { Data = avgExtA15, Unit = samples.First().ExtADC_A15.Unit }
        };

        return result;
    }



    // Inserisce i nuovi dati raccolti nelle collezioni dei parametri abilitati.
    private void UpdateAllDataCollections(dynamic data)
    {
        var values = new Dictionary<string, float>();

        try
        {
            if (enableAccelerometer)
            {
                values["Low-Noise AccelerometerX"] = (float)data.AccelerometerX.Data;
                values["Low-Noise AccelerometerY"] = (float)data.AccelerometerY.Data;
                values["Low-Noise AccelerometerZ"] = (float)data.AccelerometerZ.Data;
            }

            if (enableWideRangeAccelerometer)
            {
                values["Wide-Range AccelerometerX"] = (float)data.WideRangeAccelerometerX.Data;
                values["Wide-Range AccelerometerY"] = (float)data.WideRangeAccelerometerY.Data;
                values["Wide-Range AccelerometerZ"] = (float)data.WideRangeAccelerometerZ.Data;
            }

            if (enableGyroscope)
            {
                values["GyroscopeX"] = (float)data.GyroscopeX.Data;
                values["GyroscopeY"] = (float)data.GyroscopeY.Data;
                values["GyroscopeZ"] = (float)data.GyroscopeZ.Data;
            }

            if (enableMagnetometer)
            {
                values["MagnetometerX"] = (float)data.MagnetometerX.Data;
                values["MagnetometerY"] = (float)data.MagnetometerY.Data;
                values["MagnetometerZ"] = (float)data.MagnetometerZ.Data;
            }
            if (enablePressureTemperature)
            {
                values["Temperature_BMP180"] = (float)data.Temperature_BMP180.Data;
                values["Pressure_BMP180"] = (float)data.Pressure_BMP180.Data;
            }

            if (enableBattery && data.BatteryVoltage != null)
            {
                values["BatteryVoltage"] = (float)data.BatteryVoltage.Data;
            }
            if (enableExtA6)
                values["ExtADC_A6"] = (float)data.ExtADC_A6.Data;
            if (enableExtA7)
                values["ExtADC_A7"] = (float)data.ExtADC_A7.Data;
            if (enableExtA15)
                values["ExtADC_A15"] = (float)data.ExtADC_A15.Data;

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in UpdateAllDataCollections: {ex.Message}");
            return;
        }

        // Aggiorna ogni collezione SOLO se c'è un valore valido
        foreach (var parameter in AvailableParameters)
        {
            if (values.ContainsKey(parameter))
            {
                dataPointsCollections[parameter].Add(values[parameter]);
                timeStampsCollections[parameter].Add(secondsElapsed);

                // Mantieni solo gli ultimi punti secondo la finestra temporale
                while (dataPointsCollections[parameter].Count > TimeWindowSeconds)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                    timeStampsCollections[parameter].RemoveAt(0);
                }
            }
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
        // Determina la modalità di visualizzazione
        ChartDisplayMode = IsMultiChart(value) ? ChartDisplayMode.Multi : ChartDisplayMode.Single;

        UpdateYAxisSettings(value);

        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;

        UpdateTextProperties();
        IsXAxisLabelIntervalEnabled = value != "HeartRate";
        ValidationMessage = "";

        UpdateChart();
    }



    // Imposta etichette e limiti asse Y in base al parametro selezionato.
    private void UpdateYAxisSettings(string parameter)
    {
        switch (parameter)
        {
            case "Low-Noise Accelerometer":
                YAxisLabel = "Low-Noise Accelerometer";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer (X,Y,Z)";
                YAxisMin = -5;
                YAxisMax = 5;
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
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "Temperature_BMP180":
                YAxisLabel = "Temperature";
                YAxisUnit = "°C";
                ChartTitle = "BMP180 Temperature";
                YAxisMin = 20; 
                YAxisMax = 50;
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
                YAxisUnit = "mV";
                ChartTitle = "Real-time Battery";
                YAxisMin = 3000;
                YAxisMax = 4200;
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
                YAxisUnit = "mV";
                ChartTitle = "External ADC A6";
                YAxisMin = 0;
                YAxisMax = 3000;
                break;
            case "ExtADC_A7":
                YAxisLabel = "External ADC A7";
                YAxisUnit = "mV";
                ChartTitle = "External ADC A7";
                YAxisMin = 0;
                YAxisMax = 3000;
                break;
            case "ExtADC_A15":
                YAxisLabel = "External ADC A15";
                YAxisUnit = "mV";
                ChartTitle = "External ADC A15";
                YAxisMin = 0;
                YAxisMax = 3000;
                break;

        }
    }


    // Aggiorna manualmente i limiti degli assi e la finestra temporale.
    public void UpdateAxisScales(double minY, double maxY, int timeWindow)
    {

        if (minY >= maxY)
        {
            ValidationMessage = "Y Min cannot be greater than or equal to Y Max.";
            return;
        }

        if (timeWindow <= 0)
        {
            ValidationMessage = "Time Window must be greater than 0 seconds.";
            return;
        }

        ValidationMessage = "";
        YAxisMin = minY;
        YAxisMax = maxY;
        TimeWindowSeconds = timeWindow;

        // Aggiorna i valori di backup
        _lastValidYAxisMin = minY;
        _lastValidYAxisMax = maxY;
        _lastValidTimeWindowSeconds = timeWindow;

        // Aggiorna i testi
        UpdateTextProperties();
    }

    // Disegna il grafico dei dati sul canvas SkiaSharp.
    public void OnCanvasViewPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        // First check: Verify if sensor is disabled
        if (!IsSensorEnabled(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        // Second check: Verify if parameter is available
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        DrawOscilloscopeGrid(canvas, leftMargin, margin, graphWidth, graphHeight);

        var yRange = YAxisMax - YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        // Calculate time range for X-axis mapping
        double currentTime = sampleCounter / shimmer.SamplingRate;
        double timeStart = Math.Max(0, currentTime - TimeWindowSeconds);
        double timeRange = TimeWindowSeconds;

        // Check if we're in multi-chart mode
        if (ChartDisplayMode == ChartDisplayMode.Multi)
        {
            DrawMultipleParameters(canvas, leftMargin, margin, graphWidth, graphHeight,
                                  yRange, bottomY, topY, timeStart, timeRange);
        }
        else
        {
            DrawSingleParameter(canvas, leftMargin, margin, graphWidth, graphHeight,
                               yRange, bottomY, topY, timeStart, timeRange);
        }

        // Draw axes labels and title (common for both modes)
        DrawAxesAndTitle(canvas, info, leftMargin, margin, graphWidth, graphHeight,
                        yRange, bottomY, timeStart);
    }

    private void DrawSingleParameter(SKCanvas canvas, float leftMargin, float margin,
                                    float graphWidth, float graphHeight, double yRange,
                                    float bottomY, float topY, double timeStart, double timeRange)
    {
        var currentDataPoints = dataPointsCollections[SelectedParameter];
        var currentTimeStamps = timeStampsCollections[SelectedParameter];

        // No data or all invalid values
        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + margin),
                                                     (int)(margin + graphHeight + 65)));
            return;
        }

        using var linePaint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        using var path = new SKPath();

        for (int i = 0; i < currentDataPoints.Count; i++)
        {
            double sampleTime = currentTimeStamps[i] / 1000.0;
            double normalizedX = (sampleTime - timeStart) / timeRange;
            var x = leftMargin + (float)(normalizedX * graphWidth);

            var normalizedValue = (currentDataPoints[i] - YAxisMin) / yRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);
            y = Math.Clamp(y, topY, bottomY);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);
    }

    private void DrawMultipleParameters(SKCanvas canvas, float leftMargin, float margin,
                                       float graphWidth, float graphHeight, double yRange,
                                       float bottomY, float topY, double timeStart, double timeRange)
    {
        var subParameters = GetCurrentSubParameters();
        var colors = GetParameterColors(SelectedParameter);

        bool hasData = false;

        for (int paramIndex = 0; paramIndex < subParameters.Count; paramIndex++)
        {
            var parameter = subParameters[paramIndex];

            if (!dataPointsCollections.ContainsKey(parameter) ||
                dataPointsCollections[parameter].Count == 0)
                continue;

            var currentDataPoints = dataPointsCollections[parameter];
            var currentTimeStamps = timeStampsCollections[parameter];

            // Skip if all values are invalid
            if (currentDataPoints.All(v => v == -1 || v == 0))
                continue;

            hasData = true;

            using var linePaint = new SKPaint
            {
                Color = colors[paramIndex],
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            using var path = new SKPath();

            // Handle special case for Battery with different scales
            double adjustedYMin = YAxisMin;
            double adjustedYMax = YAxisMax;

            if (SelectedParameter == "Battery")
            {
                if (parameter == "BatteryVoltage")
                {
                    // Use voltage range (3000-4200 mV)
                    adjustedYMin = 3000;
                    adjustedYMax = 4200;
                }
                else if (parameter == "BatteryPercent")
                {
                    // Use percentage range (0-100%)
                    adjustedYMin = 0;
                    adjustedYMax = 100;
                }
            }

            double adjustedYRange = adjustedYMax - adjustedYMin;

            for (int i = 0; i < currentDataPoints.Count; i++)
            {
                double sampleTime = currentTimeStamps[i] / 1000.0;
                double normalizedX = (sampleTime - timeStart) / timeRange;
                var x = leftMargin + (float)(normalizedX * graphWidth);

                var normalizedValue = (currentDataPoints[i] - adjustedYMin) / adjustedYRange;
                var y = bottomY - (float)(normalizedValue * graphHeight);
                y = Math.Clamp(y, topY, bottomY);

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }

            canvas.DrawPath(path, linePaint);
        }

        // If no data for any parameter, show no data message
        if (!hasData)
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + margin),
                                                     (int)(margin + graphHeight + 65)));
        }
    }

    private SKColor[] GetParameterColors(string groupParameter)
    {
        return groupParameter switch
        {
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
            "Gyroscope" or "Magnetometer" => new[] { SKColors.Red, SKColors.Green, SKColors.Blue },
            "Battery" => new[] { SKColors.Orange, SKColors.Purple },
            _ => new[] { SKColors.Blue }
        };
    }

    private void DrawAxesAndTitle(SKCanvas canvas, SKImageInfo info, float leftMargin,
                                 float margin, float graphWidth, float graphHeight,
                                 double yRange, float bottomY, double timeStart)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // X-axis labels synchronized with grid and real timestamps
        int numDivisions = TimeWindowSeconds;
        int labelInterval = IsXAxisLabelIntervalEnabled ? XAxisLabelInterval : 1;

        for (int i = 0; i <= numDivisions; i++)
        {
            double actualTime = timeStart + (i * TimeWindowSeconds / (double)numDivisions);
            int timeValueForLabel = (int)Math.Floor(actualTime);

            if (timeValueForLabel < 0 || (timeValueForLabel % labelInterval != 0)) continue;

            float x = leftMargin + (i * graphWidth / numDivisions);

            string label = FormatTimeLabel((int)actualTime);
            var textWidth = textPaint.MeasureText(label);
            canvas.DrawText(label, x - textWidth / 2, bottomY + 20, textPaint);
        }

        // Y-axis labels
        for (int i = 0; i <= 4; i++)
        {
            var value = YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - YAxisMin) / yRange * graphHeight);
            var label = value.ToString("F1");
            canvas.DrawText(label, leftMargin - 45, y + 6, textPaint);
        }

        // Axis labels
        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        string xAxisLabel = "Time [s]";
        var labelX = (info.Width - axisLabelPaint.MeasureText(xAxisLabel)) / 2;
        var labelY = info.Height - 8;
        canvas.DrawText(xAxisLabel, labelX, labelY, axisLabelPaint);

        var yAxisLabelText = $"{YAxisLabel} [{YAxisUnit}]";
        canvas.Save();
        canvas.Translate(15, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

        // Title
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };

        var titleWidth = titlePaint.MeasureText(ChartTitle);
        canvas.DrawText(ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }

    public void Cleanup()
    {
        StopTimer();
        ClearAllDataCollections();
    }

    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        UpdateChart();
    }

    // Metodi di validazione numerica 

    //// Verifica se un numero double è valido (non infinito, non NaN, ecc.).
    private static bool IsValidNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value != double.MinValue && value != double.MaxValue;
    }

    // Verifica se un intero è positivo e valido.
    private static bool IsValidPositiveInteger(int value)
    {
        return value > 0 && value < int.MaxValue;
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

    // Disegna un messaggio che segnala che il sensore è disattivato.
    private void DrawDisabledSensorMessage(SKCanvas canvas, SKImageInfo info)
    {
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.LightGray.WithAlpha(100),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, backgroundPaint);

        using var messagePaint = new SKPaint
        {
            Color = SKColors.Red,
            TextSize = 24,
            IsAntialias = true,
            FakeBoldText = true
        };

        string sensorName = SelectedParameter switch
        {
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => "Low-Noise Accelerometer",
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => "Wide-Range Accelerometer",
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => "Gyroscope",
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => "Magnetometer",
            "Temperature_BMP180" => "Temperature (BMP180)",
            "Pressure_BMP180" => "Pressure (BMP180)",
            "BatteryVoltage" or "BatteryPercent" => "Battery",
            "ExtADC_A6" => "External ADC A6",
            "ExtADC_A7" => "External ADC A7",
            "ExtADC_A15" => "External ADC A15",
            _ => "Sensor"
        };


        var disabledMessage = $"{sensorName} Disabled";
        var messageWidth = messagePaint.MeasureText(disabledMessage);
        var centerX = leftMargin + graphWidth / 2;
        var centerY = margin + graphHeight / 2;

        canvas.DrawText(disabledMessage, centerX - messageWidth / 2, centerY, messagePaint);

        using var subtitlePaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 16,
            IsAntialias = true
        };

        var subtitleMessage = "Enable this sensor to view data";
        var subtitleWidth = subtitlePaint.MeasureText(subtitleMessage);
        canvas.DrawText(subtitleMessage, centerX - subtitleWidth / 2, centerY + 35, subtitlePaint);

        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };
        var titleWidth = titlePaint.MeasureText(ChartTitle);
        canvas.DrawText(ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }

    // Disegna un messaggio quando non ci sono dati validi da mostrare.
    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.LightGray.WithAlpha(100),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, backgroundPaint);

        using var messagePaint = new SKPaint
        {
            Color = SKColors.OrangeRed,
            TextSize = 24,
            IsAntialias = true,
            FakeBoldText = true
        };

        var message = "No valid data available";
        var messageWidth = messagePaint.MeasureText(message);
        var centerX = leftMargin + graphWidth / 2;
        var centerY = margin + graphHeight / 2;

        canvas.DrawText(message, centerX - messageWidth / 2, centerY, messagePaint);

        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };
        var titleWidth = titlePaint.MeasureText(ChartTitle);
        canvas.DrawText(ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }

    // Disegna un messaggio che indica che il sensore è in fase di calibrazione.
    private void DrawCalibratingMessage(SKCanvas canvas, SKImageInfo info)
    {
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.LightYellow,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, backgroundPaint);

        using var messagePaint = new SKPaint
        {
            Color = SKColors.DarkOrange,
            TextSize = 24,
            IsAntialias = true,
            FakeBoldText = true
        };

        var message = "Calibrating...";
        var messageWidth = messagePaint.MeasureText(message);
        var centerX = leftMargin + graphWidth / 2;
        var centerY = margin + graphHeight / 2;

        canvas.DrawText(message, centerX - messageWidth / 2, centerY - 15, messagePaint);

        using var subtitlePaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 16,
            IsAntialias = true
        };

        var subtitleMessage = "Please wait a few seconds";
        var subtitleWidth = subtitlePaint.MeasureText(subtitleMessage);
        canvas.DrawText(subtitleMessage, centerX - subtitleWidth / 2, centerY + 15, subtitlePaint);

        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };
        var titleWidth = titlePaint.MeasureText(ChartTitle);
        canvas.DrawText(ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }

    // Aggiorna la configurazione dei sensori attivi e rigenera la lista dei parametri.
    public void UpdateSensorConfiguration(
       bool enableAccelerometer,
       bool enableWideRangeAccelerometer,
       bool enableGyroscope,
       bool enableMagnetometer,
       bool enablePressureTemperature,
       bool enableBattery,
       bool enableExtA6,
       bool enableExtA7,
       bool enableExtA15)
    {
        // Salva il parametro attualmente selezionato
        string currentParameter = SelectedParameter;

        // Aggiorna i flag dei sensori
        this.enableAccelerometer = enableAccelerometer;
        this.enableWideRangeAccelerometer = enableWideRangeAccelerometer;
        this.enableGyroscope = enableGyroscope;
        this.enableMagnetometer = enableMagnetometer;
        this.enablePressureTemperature = enablePressureTemperature;
        this.enableBattery = enableBattery;
        this.enableExtA6 = enableExtA6;
        this.enableExtA7 = enableExtA7;
        this.enableExtA15 = enableExtA15;

        // Rigenera la lista dei parametri disponibili
        InitializeAvailableParameters();

        // Se il parametro precedente non è più disponibile, seleziona il primo disponibile
        if (!AvailableParameters.Contains(currentParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }

        // Aggiorna le collezioni di dati
        UpdateDataCollections();

    }

    // Metodo per aggiornare le collezioni di dati dopo cambio configurazione
    // Aggiorna le collezioni di dati per riflettere i sensori attivi.
    private void UpdateDataCollections()
    {
        // Rimuovi le collezioni per parametri non più disponibili
        var parametersToRemove = dataPointsCollections.Keys.Where(p => !AvailableParameters.Contains(p)).ToList();
        foreach (var parameter in parametersToRemove)
        {
            dataPointsCollections.Remove(parameter);
            timeStampsCollections.Remove(parameter);
        }

        // Aggiungi collezioni per nuovi parametri
        foreach (var parameter in AvailableParameters)
        {
            if (!dataPointsCollections.ContainsKey(parameter))
            {
                dataPointsCollections[parameter] = new List<float>();
                timeStampsCollections[parameter] = new List<int>();
            }
        }
    }

    // Metodo per ottenere la configurazione corrente
    // Restituisce un oggetto che rappresenta la configurazione attuale dei sensori.
    public SensorConfiguration GetCurrentSensorConfiguration()
    {
        return new SensorConfiguration
        {
            EnableAccelerometer = enableAccelerometer,
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

    public void ResetStartTime()
    {
        startTime = DateTime.Now;
    }

    


}
