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

    // Sensor enable flags (set from SensorConfiguration passed in constructor)
    private bool enableAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;

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
    private string selectedParameter = "AccelerometerX";

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
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;

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
        // Se il campo è vuoto, usa il valore di default ma lascia il campo vuoto
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultTimeWindow = 20;
            ValidationMessage = "";
            TimeWindowSeconds = defaultTimeWindow;
            _lastValidTimeWindowSeconds = defaultTimeWindow;

            // Non aggiornare il testo - lascia il campo vuoto

            // Taglia tutte le collezioni alla nuova dimensione
            foreach (var parameter in AvailableParameters)
            {
                while (dataPointsCollections[parameter].Count > defaultTimeWindow)
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

            // Taglia tutte le collezioni alla nuova dimensione
            foreach (var parameter in AvailableParameters)
            {
                while (dataPointsCollections[parameter].Count > result)
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
            "AccelerometerX" or "AccelerometerY" or "AccelerometerZ" => -5,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => -250,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => -2,
            _ => 0
        };
    }

    // Restituisce il valore massimo di default per l’asse Y in base al parametro selezionato.
    private double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            "AccelerometerX" or "AccelerometerY" or "AccelerometerZ" => 5,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => 250,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => 2,
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
            AvailableParameters.Add("AccelerometerX");
            AvailableParameters.Add("AccelerometerY");
            AvailableParameters.Add("AccelerometerZ");
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
            "AccelerometerX" or "AccelerometerY" or "AccelerometerZ" => enableAccelerometer,
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => enableGyroscope,
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => enableMagnetometer,
            _ => false
        };
    }

    // Avvia il timer che legge e aggiorna i dati a intervalli regolari.

    public void StartTimer()
    {
        double rateHz = shimmer.SamplingRate;
        int intervalMs = (int)(1000.0 / rateHz);

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
    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var data = shimmer.LatestData;
        if (data == null) return;

        secondsElapsed++;

        SensorText = $"[{data.TimeStamp.Data}] {data.AccelerometerX.Data} [{data.AccelerometerX.Unit}] | " +
                     $"{data.AccelerometerY.Data} [{data.AccelerometerY.Unit}] | {data.AccelerometerZ.Data} [{data.AccelerometerZ.Unit}]\n" +
                     $"{data.GyroscopeX.Data} [{data.GyroscopeX.Unit}] | " +
                     $"{data.GyroscopeY.Data} [{data.GyroscopeY.Unit}] | {data.GyroscopeZ.Data} [{data.GyroscopeZ.Unit}]\n" +
                     $"{data.MagnetometerX.Data} [{data.MagnetometerX.Unit}] | " +
                     $"{data.MagnetometerY.Data} [{data.MagnetometerY.Unit}] | {data.MagnetometerZ.Data} [{data.MagnetometerZ.Unit}]\n";

        UpdateAllDataCollections(data);
        UpdateChart();
    }

    // Inserisce i nuovi dati raccolti nelle collezioni dei parametri abilitati.
    private void UpdateAllDataCollections(dynamic data)
    {
        // Estrai i valori per ogni parametro
        var values = new Dictionary<string, float>();

        if (enableAccelerometer)
        {
            values["AccelerometerX"] = (float)data.AccelerometerX.Data;
            values["AccelerometerY"] = (float)data.AccelerometerY.Data;
            values["AccelerometerZ"] = (float)data.AccelerometerZ.Data;
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
            case "AccelerometerX":
                YAxisLabel = "Acceleration X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration X";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "AccelerometerY":
                YAxisLabel = "Acceleration Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "AccelerometerZ":
                YAxisLabel = "Acceleration Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration Z";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "GyroscopeX":
                YAxisLabel = "Angular Velocity X";
                YAxisUnit = "°/s";
                ChartTitle = "Real-time Gyroscope X";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "GyroscopeY":
                YAxisLabel = "Angular Velocity Y";
                YAxisUnit = "°/s";
                ChartTitle = "Real-time Gyroscope Y";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "GyroscopeZ":
                YAxisLabel = "Angular Velocity Z";
                YAxisUnit = "°/s";
                ChartTitle = "Real-time Gyroscope Z";
                YAxisMin = -250;
                YAxisMax = 250;
                break;
            case "MagnetometerX":
                YAxisLabel = "Magnetic Field X";
                YAxisUnit = "µT";
                ChartTitle = "Real-time Magnetometer X";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "MagnetometerY":
                YAxisLabel = "Magnetic Field Y";
                YAxisUnit = "µT";
                ChartTitle = "Real-time Magnetometer Y";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "MagnetometerZ":
                YAxisLabel = "Magnetic Field Z";
                YAxisUnit = "µT";
                ChartTitle = "Real-time Magnetometer Z";
                YAxisMin = -2;
                YAxisMax = 2;
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

        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        if (currentDataPoints.All(v => v == 0 || v == -1))
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

        var yRange = YAxisMax - YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        using var gridLinePaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
        };

        for (int i = 0; i <= 4; i++)
        {
            var value = YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - YAxisMin) / yRange * graphHeight);
            canvas.DrawLine(leftMargin, y, leftMargin + graphWidth, y, gridLinePaint);
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
            var x = leftMargin + (i * graphWidth / Math.Max(currentDataPoints.Count - 1, 1));
            var normalizedValue = (currentDataPoints[i] - YAxisMin) / yRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);

            y = Math.Max(topY, Math.Min(bottomY, y));

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

        // Usa i timestamp effettivi, ma solo se l'intervallo è abilitato
        for (int i = 0; i < currentDataPoints.Count; i++)
        {
            var timeValue = currentTimeStamps[i];

            // Per Heart Rate, mostra sempre tutte le etichette (intervallo disabilitato)
            // Per altri sensori, usa l'intervallo impostato
            bool shouldShowLabel = !IsXAxisLabelIntervalEnabled ||
                                  timeValue % XAxisLabelInterval == 0 ||
                                  i == 0 ||
                                  i == currentDataPoints.Count - 1;

            if (shouldShowLabel)
            {
                var x = leftMargin + (i * graphWidth / Math.Max(currentDataPoints.Count - 1, 1));
                var displayTime = timeValue.ToString() + "s";
                var textWidth = textPaint.MeasureText(displayTime);
                canvas.DrawText(displayTime, x - textWidth / 2, margin + graphHeight + 20, textPaint);
            }
        }

        // Etichette Y
        for (int i = 0; i <= 4; i++)
        {
            var value = YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - YAxisMin) / yRange * graphHeight);
            var label = value.ToString("F1");
            canvas.DrawText(label, leftMargin - 45, y + 6, textPaint);
        }

        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        var xAxisLabel = "Time[seconds]";
        var xAxisLabelWidth = axisLabelPaint.MeasureText(xAxisLabel);
        canvas.DrawText(xAxisLabel, (info.Width - xAxisLabelWidth) / 2, info.Height - 5, axisLabelPaint);

        var yAxisLabelText = $"{YAxisLabel}[{YAxisUnit}]";
        canvas.Save();
        canvas.Translate(15, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

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
            "AccelerometerX" or "AccelerometerY" or "AccelerometerZ" => "Accelerometer",
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => "Gyroscope",
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => "Magnetometer",
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
    public void UpdateSensorConfiguration(bool enableAccelerometer, bool enableGyroscope, bool enableMagnetometer)
    {
        // Salva il parametro attualmente selezionato
        string currentParameter = SelectedParameter;

        // Aggiorna i flag dei sensori (aggiungi questi campi come non-readonly)
        this.enableAccelerometer = enableAccelerometer;
        this.enableGyroscope = enableGyroscope;
        this.enableMagnetometer = enableMagnetometer;

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
            EnableGyroscope = enableGyroscope,
            EnableMagnetometer = enableMagnetometer
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


}

