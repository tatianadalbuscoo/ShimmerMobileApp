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

    private DateTime startTime = DateTime.Now;

    // Sensor enable flags (set from SensorConfiguration passed in constructor)
    private bool enableAccelerometer;
    private bool enableWideRangeAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;
    private bool enableBattery;

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
        enableBattery = config.EnableBattery;

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
            // Non aggiornare YAxisMin, mantieni il valore precedente
            // Non chiamare UpdateChart() per evitare refresh continui
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
            // Non aggiornare YAxisMax, mantieni il valore precedente
            // Non chiamare UpdateChart() per evitare refresh continui
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

    private void ValidateAndUpdateSamplingRate(string value)
    {

        if (string.IsNullOrWhiteSpace(value))
        {
            const double defaultRate = 51.2;
            shimmer.SamplingRate = defaultRate;
            SamplingRateDisplay = defaultRate;
            _lastValidSamplingRate = defaultRate;
            ValidationMessage = "";
            RestartTimer();
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

            shimmer.SamplingRate = result;
            SamplingRateDisplay = result;
            _lastValidSamplingRate = result;
            ValidationMessage = "";
            RestartTimer();
        }
        else
        {
            ValidationMessage = "Sampling rate must be a valid number (no letters or special characters allowed).";
            ResetSamplingRateText();
        }
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
            "Battery Voltage" => 3300,
            "Battery Percent" => 0,
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
            "Battery Voltage" => 4200,
            "Battery Percent" => 100,
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
            AvailableParameters.Add("Low-Noise AccelerometerX");
            AvailableParameters.Add("Low-Noise AccelerometerY");
            AvailableParameters.Add("Low-Noise AccelerometerZ");
        }

        if (enableWideRangeAccelerometer)
        {
            AvailableParameters.Add("Wide-Range AccelerometerX");
            AvailableParameters.Add("Wide-Range AccelerometerY");
            AvailableParameters.Add("Wide-Range AccelerometerZ");
        }


        if (enableGyroscope)
        {
            AvailableParameters.Add("GyroscopeX");
            AvailableParameters.Add("GyroscopeY");
            AvailableParameters.Add("GyroscopeZ");
        }

        if (enableMagnetometer)
        {
            AvailableParameters.Add("MagnetometerX");
            AvailableParameters.Add("MagnetometerY");
            AvailableParameters.Add("MagnetometerZ");
        }

        if(enableBattery)
        {
            AvailableParameters.Add("BatteryVoltage");
            AvailableParameters.Add("BatteryPercent");
        }

        // Se PPG è disabilitato, HeartRate NON dovrebbe essere nella lista
        // Forza un parametro valido se quello selezionato non lo è più
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }
    }

    // Restituisce true se il sensore associato al parametro è abilitato.
    private bool IsSensorEnabled(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => enableAccelerometer,
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => enableWideRangeAccelerometer,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => enableGyroscope,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => enableMagnetometer,
            "BatteryVoltage" or "BatteryPercent" => enableBattery,
            _ => false
        };
    }


    // Avvia il timer che legge e aggiorna i dati a intervalli regolari.

    public void StartTimer()
    {
        // Timer fisso a 1 secondo per l'aggiornamento della visualizzazione
        int intervalMs = 1000;

        timer?.Stop();
        timer?.Dispose();

        timer = new System.Timers.Timer(intervalMs);
        timer.Elapsed += OnTimerElapsed;
        timer.Start();
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
        // Raccogli tutti i campioni disponibili nell'ultimo secondo
        var samples = CollectSamplesForLastSecond();
        if (samples.Count == 0) return;

        secondsElapsed++;
        var currentTime = DateTime.Now;

        // Usa l'ultimo campione per il display del testo
        var lastSample = samples.Last();

        // Battery info senza reflection
        string batteryText = "";
        if (enableBattery && lastSample.BatteryVoltage != null)
        {
            batteryText = $"\nBattery: {lastSample.BatteryVoltage.Data} [{lastSample.BatteryVoltage.Unit}]";
        }

        // Update sensor text display
        // Calcola e costruisci batteryText PRIMA di usarlo
        float batteryPercent = (float)Math.Clamp(
          ((lastSample.BatteryVoltage.Data - 3300) / 900) * 100, 0, 100);

        batteryText = $"\nBattery: {lastSample.BatteryVoltage.Data} [{lastSample.BatteryVoltage.Unit}] " +
                      $"({batteryPercent:F1}%)";

        // Ora costruisci tutta la stringa
        SensorText =
          $"[{lastSample.TimeStamp.Data}]\n" +
          $"Low-Noise Accelerometer: {lastSample.AccelerometerX.Data} [{lastSample.AccelerometerX.Unit}] | " +
          $"{lastSample.AccelerometerY.Data} [{lastSample.AccelerometerY.Unit}] | " +
          $"{lastSample.AccelerometerZ.Data} [{lastSample.AccelerometerZ.Unit}]\n" +
          $"Wide-Range Accel: {lastSample.WideRangeAccelerometerX.Data} [{lastSample.WideRangeAccelerometerX.Unit}] | " +
          $"{lastSample.WideRangeAccelerometerY.Data} [{lastSample.WideRangeAccelerometerY.Unit}] | " +
          $"{lastSample.WideRangeAccelerometerZ.Data} [{lastSample.WideRangeAccelerometerZ.Unit}]\n" +
          $"Gyroscope: {lastSample.GyroscopeX.Data} [{lastSample.GyroscopeX.Unit}] | " +
          $"{lastSample.GyroscopeY.Data} [{lastSample.GyroscopeY.Unit}] | " +
          $"{lastSample.GyroscopeZ.Data} [{lastSample.GyroscopeZ.Unit}]\n" +
          $"Magnetometer: {lastSample.MagnetometerX.Data} [{lastSample.MagnetometerX.Unit}] | " +
          $"{lastSample.MagnetometerY.Data} [{lastSample.MagnetometerY.Unit}] | " +
          $"{lastSample.MagnetometerZ.Data} [{lastSample.MagnetometerZ.Unit}]" +
          batteryText;

        // Aggiungi tutti i campioni individuali alle collezioni dati
        UpdateAllDataCollectionsWithAllSamples(samples);

        UpdateChart();
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

                if (enableBattery && sample.BatteryVoltage != null)
                {
                    values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data;
                    values["BatteryPercent"] = (float)Math.Clamp(
                        ((sample.BatteryVoltage.Data - 3300) / 900) * 100, 0, 100);
                }
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

        var avgWideAccX = samples.Average(s => (double)s.WideRangeAccelerometerX.Data);
        var avgWideAccY = samples.Average(s => (double)s.WideRangeAccelerometerY.Data);
        var avgWideAccZ = samples.Average(s => (double)s.WideRangeAccelerometerZ.Data);

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
            BatteryVoltage = new { Data = avgBatteryVoltage, Unit = batteryUnit },
            BatteryPercent = new { Data = avgBatteryPercent, Unit = "%" }
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

            if (enableBattery && data.BatteryVoltage != null)
            {
                values["BatteryVoltage"] = (float)data.BatteryVoltage.Data;
            }
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
        UpdateYAxisSettings(value);

        // Aggiorna i valori di backup dopo il cambio di parametro
        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;

        // Aggiorna i testi per riflettere i nuovi valori
        UpdateTextProperties();

        // Disabilita l'intervallo X per Heart Rate
        IsXAxisLabelIntervalEnabled = value != "HeartRate";

        // Pulisci eventuali messaggi di validazione
        ValidationMessage = "";

        UpdateChart();
    }

    // Imposta etichette e limiti asse Y in base al parametro selezionato.
    private void UpdateYAxisSettings(string parameter)
    {
        switch (parameter)
        {
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
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Wide-Range AccelerometerX":
                YAxisLabel = "Wide-Range Accelerometer X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer X";
                YAxisMin = -20;
                YAxisMax = 20;
                break;
            case "Wide-Range AccelerometerY":
                YAxisLabel = "Wide-Range Accelerometer Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Y";
                YAxisMin = -20;
                YAxisMax = 20;
                break;
            case "Wide-Range AccelerometerZ":
                YAxisLabel = "Wide-Range Accelerometer Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Z";
                YAxisMin = -20;
                YAxisMax = 20;
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
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "MagnetometerY":
                YAxisLabel = "Magnetometer Y";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Y";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "MagnetometerZ":
                YAxisLabel = "Magnetometer Z";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Z";
                YAxisMin = -2;
                YAxisMax = 2;
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

        // PRIMO CONTROLLO: Verifica se il sensore è disabilitato
        if (!IsSensorEnabled(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        // SECONDO CONTROLLO: Verifica se il parametro è disponibile
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        var currentDataPoints = dataPointsCollections[SelectedParameter];
        var currentTimeStamps = timeStampsCollections[SelectedParameter];

        // Nessun dato o tutti -1/0
        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, info);
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

        // *** CORREZIONE: Chiamare DrawOscilloscopeGrid ***
        DrawOscilloscopeGrid(canvas, leftMargin, margin, graphWidth, graphHeight);

        var yRange = YAxisMax - YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        // Linea del segnale
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
            var x = leftMargin + (i * graphWidth / Math.Max(currentDataPoints.Count - 1, 1));
            var normalizedValue = (currentDataPoints[i] - YAxisMin) / yRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);
            y = Math.Clamp(y, topY, bottomY);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // Etichette X sincronizzate con la griglia e filtrate ogni XAxisLabelInterval secondi
        int numDivisions = TimeWindowSeconds;
        int labelInterval = IsXAxisLabelIntervalEnabled ? XAxisLabelInterval : 1;

        for (int i = 0; i <= numDivisions; i++)
        {
            int timeValue = secondsElapsed - TimeWindowSeconds + i;
            if (timeValue < 0 || (timeValue % labelInterval != 0)) continue;

            float x = leftMargin + (i * graphWidth / numDivisions);

            string label = FormatTimeLabel(timeValue);
            var textWidth = textPaint.MeasureText(label);
            canvas.DrawText(label, x - textWidth / 2, bottomY + 20, textPaint);
        }


        // Etichette Y
        for (int i = 0; i <= 4; i++)
        {
            var value = YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - YAxisMin) / yRange * graphHeight);
            var label = value.ToString("F1");
            canvas.DrawText(label, leftMargin - 45, y + 6, textPaint);
        }

        // Etichette assi
        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        string xAxisLabel = "Time [s]";




        var labelX = (info.Width - axisLabelPaint.MeasureText(xAxisLabel)) / 2;
        var labelY = info.Height - 8; // più in alto di "5" per evitare taglio in basso

        canvas.DrawText(xAxisLabel, labelX, labelY, axisLabelPaint);


        var yAxisLabelText = $"{YAxisLabel} [{YAxisUnit}]";
        canvas.Save();
        canvas.Translate(15, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

        // Titolo
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
            "Battery" => "Battery",
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
    public void UpdateSensorConfiguration(bool enableAccelerometer, bool enableWideRangeAccelerometer, bool enableGyroscope, bool enableMagnetometer)
    {
        // Salva il parametro attualmente selezionato
        string currentParameter = SelectedParameter;

        // Aggiorna i flag dei sensori (aggiungi questi campi come non-readonly)
        this.enableAccelerometer = enableAccelerometer;
        this.enableWideRangeAccelerometer = enableWideRangeAccelerometer;
        this.enableGyroscope = enableGyroscope;
        this.enableMagnetometer = enableMagnetometer;
        this.enableBattery = enableBattery;

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
            EnableBattery = enableBattery
        };
    }

    // Ferma il timer che aggiorna i dati.
    public void StopTimer()
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Dispose();
            timer = null;
        }
    }

    public void ResetStartTime()
    {
        startTime = DateTime.Now;
    }

    



}