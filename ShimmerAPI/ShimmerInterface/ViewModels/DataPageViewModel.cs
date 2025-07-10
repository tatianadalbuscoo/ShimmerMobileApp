using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;

namespace ShimmerInterface.ViewModels;

public partial class DataPageViewModel : ObservableObject
{
    private readonly XR2Learn_ShimmerGSR shimmer;
    private readonly System.Timers.Timer timer = new(1000);
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();
    private int secondsElapsed = 0;

    // Stato dei sensori
    private bool enableAccelerometer;
    private bool enableGSR;
    private bool enablePPG;

    // Valori di backup per il ripristino in caso di input non validi
    private double _lastValidYAxisMin = 0;
    private double _lastValidYAxisMax = 1;
    private int _lastValidTimeWindowSeconds = 20;
    private int _lastValidXAxisLabelInterval = 5;

    // Backing fields for input validation
    private string _yAxisMinText = "0";
    private string _yAxisMaxText = "1";
    private string _timeWindowSecondsText = "20";
    private string _xAxisLabelIntervalText = "5";

    [ObservableProperty]
    private string sensorText = "Waiting for data...";

    [ObservableProperty]
    private string selectedParameter = "AcceleratorX";

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

    public DataPageViewModel(XR2Learn_ShimmerGSR shimmerDevice, SensorConfiguration config)
    {
        shimmer = shimmerDevice;
        enableAccelerometer = config.EnableAccelerometer;
        enableGSR = config.EnableGSR;
        enablePPG = config.EnablePPG;

        InitializeAvailableParameters();

        // IMPORTANTE: cambia prima il parametro se non è valido
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }

        foreach (var parameter in AvailableParameters)
        {
            dataPointsCollections[parameter] = new List<float>();
            timeStampsCollections[parameter] = new List<int>();
        }

        // ✅ Imposta le proprietà di visualizzazione
        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
            IsXAxisLabelIntervalEnabled = SelectedParameter != "HeartRate";
        }


        // Inizializza i valori di backup e testi
        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        // Sincronizza i testi con i valori iniziali
        UpdateTextProperties();

        StartTimer();

        DebugSensorStatus();
    }

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
            // Controllo specifico per Heart Rate - minimo 50
            if (SelectedParameter == "HeartRate" && result < 50)
            {
                ValidationMessage = "Heart Rate Y Min cannot be less than 50 BPM.";
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
    private double GetDefaultYAxisMin(string parameter)
    {
        return parameter switch
        {
            "AcceleratorX" or "AcceleratorY" or "AcceleratorZ" => -2,
            "GalvanicSkinResponse" => 0,
            "PhotoPlethysmoGram" => 0,
            "HeartRate" => 50,
            _ => 0
        };
    }

    private double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            "AcceleratorX" or "AcceleratorY" or "AcceleratorZ" => 2,
            "GalvanicSkinResponse" => 100,
            "PhotoPlethysmoGram" => 3300,
            "HeartRate" => 150,
            _ => 1
        };
    }


    private void ResetYAxisMinText()
    {
        _yAxisMinText = _lastValidYAxisMin.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
    }

    private void ResetYAxisMaxText()
    {
        _yAxisMaxText = _lastValidYAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMaxText));
    }

    private void ResetTimeWindowText()
    {
        _timeWindowSecondsText = _lastValidTimeWindowSeconds.ToString();
        OnPropertyChanged(nameof(TimeWindowSecondsText));
    }

    private void ResetXAxisIntervalText()
    {
        _xAxisLabelIntervalText = _lastValidXAxisLabelInterval.ToString();
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }

    private void InitializeAvailableParameters()
    {
        AvailableParameters.Clear(); // 👈 importante se ricarichi il ViewModel

        if (enableAccelerometer)
        {
            AvailableParameters.Add("AcceleratorX");
            AvailableParameters.Add("AcceleratorY");
            AvailableParameters.Add("AcceleratorZ");
        }

        if (enableGSR)
        {
            AvailableParameters.Add("GalvanicSkinResponse");
        }

        if (enablePPG)
        {
            AvailableParameters.Add("PhotoPlethysmoGram");
            AvailableParameters.Add("HeartRate");
        }

        // ⚠️ IMPORTANTE: Se PPG è disabilitato, HeartRate NON dovrebbe essere nella lista
        // Forza un parametro valido se quello selezionato non lo è più
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }
    }


    private bool IsSensorEnabled(string parameter)
    {
        return parameter switch
        {
            "AcceleratorX" or "AcceleratorY" or "AcceleratorZ" => enableAccelerometer,
            "GalvanicSkinResponse" => enableGSR,
            "PhotoPlethysmoGram" => enablePPG,
            "HeartRate" => enablePPG, // HeartRate dipende da PPG - QUESTO È CORRETTO
            _ => false
        };
    }


    /* private void StartTimer()
     {
         timer.Elapsed += (s, e) =>
         {
             var data = shimmer.LatestData;
             if (data == null) return;

             secondsElapsed++;

             SensorText = $"[{data.TimeStamp.Data}] {data.AcceleratorX.Data} [{data.AcceleratorX.Unit}] | " +
                          $"{data.AcceleratorY.Data} [{data.AcceleratorY.Unit}] | {data.AcceleratorZ.Data} [{data.AcceleratorZ.Unit}]\n" +
                          $"{data.GalvanicSkinResponse.Data} [{data.GalvanicSkinResponse.Unit}] | " +
                          $"{data.PhotoPlethysmoGram.Data} [{data.PhotoPlethysmoGram.Unit}] | {data.HeartRate} [BPM]";

             UpdateAllDataCollections(data);
             UpdateChart();
         };
         timer.Start();
     }*/

    public void StartTimer()
    {
        timer.Elapsed -= OnTimerElapsed; // prevenzione doppia iscrizione
        timer.Elapsed += OnTimerElapsed;
        timer.Start();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var data = shimmer.LatestData;
        if (data == null) return;

        secondsElapsed++;

        SensorText = $"[{data.TimeStamp.Data}] {data.AcceleratorX.Data} [{data.AcceleratorX.Unit}] | " +
                     $"{data.AcceleratorY.Data} [{data.AcceleratorY.Unit}] | {data.AcceleratorZ.Data} [{data.AcceleratorZ.Unit}]\n" +
                     $"{data.GalvanicSkinResponse.Data} [{data.GalvanicSkinResponse.Unit}] | " +
                     $"{data.PhotoPlethysmoGram.Data} [{data.PhotoPlethysmoGram.Unit}] | {data.HeartRate} [BPM]";

        UpdateAllDataCollections(data);
        UpdateChart();
    }


    private void UpdateAllDataCollections(dynamic data)
    {
        // Estrai i valori per ogni parametro
        var values = new Dictionary<string, float>();

        if (enableAccelerometer)
        {
            values["AcceleratorX"] = (float)data.AcceleratorX.Data;
            values["AcceleratorY"] = (float)data.AcceleratorY.Data;
            values["AcceleratorZ"] = (float)data.AcceleratorZ.Data;
        }
        if (enableGSR)
        {
            values["GalvanicSkinResponse"] = (float)data.GalvanicSkinResponse.Data;
        }
        if (enablePPG)
        {
            values["PhotoPlethysmoGram"] = (float)data.PhotoPlethysmoGram.Data;

            // Aggiungi HeartRate solo se è valido
            if (data.HeartRate > 0 && data.HeartRate < 250)
            {
                values["HeartRate"] = (float)data.HeartRate;
            }
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

    private void UpdateChart()
    {
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

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

    private void UpdateYAxisSettings(string parameter)
    {
        switch (parameter)
        {
            case "AcceleratorX":
                YAxisLabel = "Acceleration X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration X";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "AcceleratorY":
                YAxisLabel = "Acceleration Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration Y";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "AcceleratorZ":
                YAxisLabel = "Acceleration Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Acceleration Z";
                YAxisMin = -2;
                YAxisMax = 2;
                break;
            case "GalvanicSkinResponse":
                YAxisLabel = "Galvanic Skin Response";
                YAxisUnit = "kΩ";
                ChartTitle = "Real-time GSR";
                YAxisMin = 0;
                YAxisMax = 100;
                break;
            case "PhotoPlethysmoGram":
                YAxisLabel = "PhotoPlethysmoGram";
                YAxisUnit = "mV";
                ChartTitle = "Real-time PPG";
                YAxisMin = 0;
                YAxisMax = 3300;
                break;
            case "HeartRate":
                YAxisLabel = "Heart Rate";
                YAxisUnit = "BPM";
                ChartTitle = "Real-time Heart Rate";
                YAxisMin = 50; // Minimo fissato a 50 per Heart Rate
                YAxisMax = 150;
                break;
        }
    }

    public void UpdateAxisScales(double minY, double maxY, int timeWindow)
    {
        // Controllo specifico per Heart Rate
        if (SelectedParameter == "HeartRate" && minY < 50)
        {
            ValidationMessage = "Heart Rate Y Min cannot be less than 50 BPM.";
            return;
        }

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

    public void OnCanvasViewPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);
        System.Diagnostics.Debug.WriteLine($"enablePPG: {enablePPG}");

        // ⚠️ PRIMA CONTROLLO: Verifica se il sensore è disabilitato
        if (!IsSensorEnabled(SelectedParameter))
        { 
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        // ⚠️ SECONDO CONTROLLO: Verifica se il parametro è disponibile
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        var currentDataPoints = dataPointsCollections[SelectedParameter];
        var currentTimeStamps = timeStampsCollections[SelectedParameter];

        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            if (SelectedParameter == "HeartRate")
            {
                // ⚠️ TERZO CONTROLLO: Per HeartRate, controlla nuovamente se PPG è abilitato
                if (enablePPG)
                    DrawCalibratingMessage(canvas, info);
                else
                    DrawDisabledSensorMessage(canvas, info);
            }
            else
            {
                DrawNoDataMessage(canvas, info);
            }
            return;
        }

        // Resto del codice rimane invariato...
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

    // Metodi di validazione numerica migliorati
    private static bool IsValidNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value != double.MinValue && value != double.MaxValue;
    }

    private static bool IsValidPositiveInteger(int value)
    {
        return value > 0 && value < int.MaxValue;
    }

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
            "AcceleratorX" or "AcceleratorY" or "AcceleratorZ" => "Accelerometer",
            "GalvanicSkinResponse" => "GSR",
            "PhotoPlethysmoGram" or "HeartRate" => "PPG",
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

    private void DebugSensorStatus()
    {
        System.Diagnostics.Debug.WriteLine($"=== SENSOR STATUS DEBUG ===");
        System.Diagnostics.Debug.WriteLine($"enablePPG: {enableAccelerometer}");
        System.Diagnostics.Debug.WriteLine($"SelectedParameter: {SelectedParameter}");
        System.Diagnostics.Debug.WriteLine($"IsSensorEnabled(HeartRate): {IsSensorEnabled("HeartRate")}");
        System.Diagnostics.Debug.WriteLine($"AvailableParameters contains HeartRate: {AvailableParameters.Contains("HeartRate")}");
        System.Diagnostics.Debug.WriteLine($"AvailableParameters: {string.Join(", ", AvailableParameters)}");
        System.Diagnostics.Debug.WriteLine($"============================");
    }

    public void UpdateSensorConfiguration(bool enableAccelerometer, bool enableGSR, bool enablePPG)
    {
        // Salva il parametro attualmente selezionato
        string currentParameter = SelectedParameter;

        // Aggiorna i flag dei sensori (aggiungi questi campi come non-readonly)
        this.enableAccelerometer = enableAccelerometer;
        this.enableGSR = enableGSR;
        this.enablePPG = enablePPG;

        // Rigenera la lista dei parametri disponibili
        InitializeAvailableParameters();

        // Se il parametro precedente non è più disponibile, seleziona il primo disponibile
        if (!AvailableParameters.Contains(currentParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }

        // Aggiorna le collezioni di dati
        UpdateDataCollections();

        // Debug aggiornato
        DebugSensorStatus();
    }

    // Metodo per aggiornare le collezioni di dati dopo cambio configurazione
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
    public SensorConfiguration GetCurrentSensorConfiguration()
    {
        return new SensorConfiguration
        {
            EnableAccelerometer = enableAccelerometer,
            EnableGSR = enableGSR,
            EnablePPG = enablePPG
        };
    }


    public void StopTimer()
    {
        timer.Stop();
    }


}

