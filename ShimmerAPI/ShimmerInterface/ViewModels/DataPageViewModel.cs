using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.ObjectModel;

namespace ShimmerInterface.ViewModels;

public partial class DataPageViewModel : ObservableObject
{
    private readonly XR2Learn_ShimmerGSR shimmer;
    private readonly System.Timers.Timer timer = new(1000);
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private int secondsElapsed = 0;

    // Stato dei sensori
    private readonly bool enableAccelerometer;
    private readonly bool enableGSR;
    private readonly bool enablePPG;

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
    private int xAxisLabelInterval = 5; // Ogni quanti secondi mostrare le etichette X

    [ObservableProperty]
    private string validationMessage = ""; // Messaggio di errore per la validazione

    // Lista dei parametri disponibili
    public ObservableCollection<string> AvailableParameters { get; } = new();

    // Evento per notificare quando il grafico deve essere ridisegnato
    public event EventHandler? ChartUpdateRequested;

    public DataPageViewModel(XR2Learn_ShimmerGSR shimmerDevice, bool enableAccelerometer = true, bool enableGSR = true, bool enablePPG = true)
    {
        shimmer = shimmerDevice;
        this.enableAccelerometer = enableAccelerometer;
        this.enableGSR = enableGSR;
        this.enablePPG = enablePPG;

        // Inizializza i parametri disponibili basati sui sensori abilitati
        InitializeAvailableParameters();

        // Inizializza le collezioni di dati per ogni parametro
        foreach (var parameter in AvailableParameters)
        {
            dataPointsCollections[parameter] = new List<float>();
        }

        // Imposta il primo parametro disponibile come selezionato
        if (AvailableParameters.Count > 0)
        {
            SelectedParameter = AvailableParameters[0];
        }

        StartTimer();
    }

    private void InitializeAvailableParameters()
    {
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
    }

    private bool IsSensorEnabled(string parameter)
    {
        return parameter switch
        {
            "AcceleratorX" or "AcceleratorY" or "AcceleratorZ" => enableAccelerometer,
            "GalvanicSkinResponse" => enableGSR,
            "PhotoPlethysmoGram" or "HeartRate" => enablePPG,
            _ => false
        };
    }

    private void StartTimer()
    {
        timer.Elapsed += (s, e) =>
        {
            var data = shimmer.LatestData;
            if (data == null) return;

            // Aggiorna il testo del sensore
            SensorText = $"[{data.TimeStamp.Data}] {data.AcceleratorX.Data} [{data.AcceleratorX.Unit}] | " +
                         $"{data.AcceleratorY.Data} [{data.AcceleratorY.Unit}] | {data.AcceleratorZ.Data} [{data.AcceleratorZ.Unit}]\n" +
                         $"{data.GalvanicSkinResponse.Data} [{data.GalvanicSkinResponse.Unit}] | " +
                         $"{data.PhotoPlethysmoGram.Data} [{data.PhotoPlethysmoGram.Unit}] | {data.HeartRate} [BPM]";

            // Aggiorna tutte le collezioni di dati
            UpdateAllDataCollections(data);

            // Aggiorna il grafico
            UpdateChart();
        };
        timer.Start();
    }

    private void UpdateAllDataCollections(dynamic data)
    {
        secondsElapsed++;

        // Estrai i valori per ogni parametro
        var values = new Dictionary<string, float>
        {
            ["AcceleratorX"] = (float)data.AcceleratorX.Data,
            ["AcceleratorY"] = (float)data.AcceleratorY.Data,
            ["AcceleratorZ"] = (float)data.AcceleratorZ.Data,
            ["GalvanicSkinResponse"] = (float)data.GalvanicSkinResponse.Data,
            ["PhotoPlethysmoGram"] = (float)data.PhotoPlethysmoGram.Data,
            ["HeartRate"] = (float)data.HeartRate
        };

        // Aggiorna ogni collezione
        foreach (var parameter in AvailableParameters)
        {
            if (values.ContainsKey(parameter))
            {
                dataPointsCollections[parameter].Add(values[parameter]);

                // Mantieni solo gli ultimi punti secondo la finestra temporale
                while (dataPointsCollections[parameter].Count > TimeWindowSeconds)
                {
                    dataPointsCollections[parameter].RemoveAt(0);
                }
            }
        }
    }

    private void UpdateChart()
    {
        // Notifica che il grafico deve essere ridisegnato
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    // Metodo chiamato quando cambia il parametro selezionato
    partial void OnSelectedParameterChanged(string value)
    {
        UpdateYAxisSettings(value);
        UpdateChart();
    }

    // Metodo chiamato quando cambia la finestra temporale
    partial void OnTimeWindowSecondsChanged(int value)
    {
        if (value <= 0)
        {
            ValidationMessage = "Time Window must be greater than 0 seconds for chart functionality.";
            TimeWindowSeconds = 1; // Valore minimo accettabile
            return;
        }

        ValidationMessage = ""; // Pulisce il messaggio di errore

        // Taglia tutte le collezioni alla nuova dimensione
        foreach (var parameter in AvailableParameters)
        {
            while (dataPointsCollections[parameter].Count > value)
            {
                dataPointsCollections[parameter].RemoveAt(0);
            }
        }
        UpdateChart();
    }

    // Metodo chiamato quando cambiano i valori degli assi Y
    partial void OnYAxisMinChanged(double value)
    {
        // Controlla che YMin non sia maggiore di YMax
        if (value >= YAxisMax)
        {
            ValidationMessage = "Y Min cannot be greater than or equal to Y Max.";
            YAxisMin = YAxisMax - 0.1; // Imposta un valore valido
            return;
        }

        ValidationMessage = ""; // Pulisce il messaggio di errore
        UpdateChart();
    }

    partial void OnYAxisMaxChanged(double value)
    {
        // Controlla che YMax non sia minore di YMin
        if (value <= YAxisMin)
        {
            ValidationMessage = "Y Max cannot be less than or equal to Y Min.";
            YAxisMax = YAxisMin + 0.1; // Imposta un valore valido
            return;
        }

        ValidationMessage = ""; // Pulisce il messaggio di errore
        UpdateChart();
    }

    // Metodo chiamato quando cambia l'intervallo delle etichette X
    partial void OnXAxisLabelIntervalChanged(int value)
    {
        if (value <= 0)
        {
            ValidationMessage = "X Labels interval must be greater than 0 for chart functionality.";
            XAxisLabelInterval = 1; // Valore minimo accettabile
            return;
        }

        ValidationMessage = ""; // Pulisce il messaggio di errore
        UpdateChart();
    }

    private void UpdateYAxisSettings(string parameter)
    {
        // Aggiorna le impostazioni dell'asse Y basate sul parametro selezionato
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
                YAxisMin = 50;
                YAxisMax = 150;
                break;
        }
    }

    // Metodo per aggiornare manualmente le scale (mantenuto per compatibilità)
    public void UpdateAxisScales(double minY, double maxY, int timeWindow)
    {
        // Validazione dei parametri
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

        ValidationMessage = ""; // Pulisce il messaggio di errore
        YAxisMin = minY;
        YAxisMax = maxY;
        TimeWindowSeconds = timeWindow;
    }

    // Metodo per disegnare il grafico
    public void OnCanvasViewPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // Pulisce il canvas
        canvas.Clear(SKColors.White);

        // Controlla se il sensore è abilitato
        if (!IsSensorEnabled(SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        var currentDataPoints = dataPointsCollections[SelectedParameter];
        if (currentDataPoints.Count == 0) return;

        // Definisce i margini e l'area del grafico
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        // Disegna il bordo del grafico
        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        // Calcola le posizioni Y per i valori min e max
        var yRange = YAxisMax - YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        // Disegna le linee di riferimento
        using var gridLinePaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
        };

        // Linee di riferimento ogni 25% dell'intervallo
        for (int i = 0; i <= 4; i++)
        {
            var value = YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - YAxisMin) / yRange * graphHeight);
            canvas.DrawLine(leftMargin, y, leftMargin + graphWidth, y, gridLinePaint);
        }

        // Disegna i punti dati
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
            // Scala i dati secondo l'intervallo Y
            var normalizedValue = (currentDataPoints[i] - YAxisMin) / yRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);

            // Limita y ai bordi del grafico
            y = Math.Max(topY, Math.Min(bottomY, y));

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);

        // Disegna le etichette dell'asse X (tempo)
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // Etichette secondo l'intervallo impostato
        for (int i = 0; i < currentDataPoints.Count; i++)
        {
            // Calcola il tempo basato sulla posizione nella finestra scorrevole
            int timeValue = Math.Max(1, secondsElapsed - currentDataPoints.Count + i + 1);

            // Mostra l'etichetta solo se il tempo è multiplo dell'intervallo
            if (timeValue % XAxisLabelInterval == 0 || i == 0 || i == currentDataPoints.Count - 1)
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

        // Etichette degli assi
        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Etichetta asse X
        var xAxisLabel = "Time[seconds]";
        var xAxisLabelWidth = axisLabelPaint.MeasureText(xAxisLabel);
        canvas.DrawText(xAxisLabel, (info.Width - xAxisLabelWidth) / 2, info.Height - 5, axisLabelPaint);

        // Etichetta asse Y - ruotata di 90 gradi
        var yAxisLabelText = $"{YAxisLabel}[{YAxisUnit}]";
        canvas.Save();
        canvas.Translate(15, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

        // Titolo del grafico
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

    private void DrawDisabledSensorMessage(SKCanvas canvas, SKImageInfo info)
    {
        // Disegna il bordo del grafico
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

        // Sfondo grigio per indicare che è disabilitato
        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.LightGray.WithAlpha(100),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, backgroundPaint);

        // Messaggio di sensore disabilitato
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

        // Sottotitolo
        using var subtitlePaint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 16,
            IsAntialias = true
        };

        var subtitleMessage = "Enable this sensor to view data";
        var subtitleWidth = subtitlePaint.MeasureText(subtitleMessage);
        canvas.DrawText(subtitleMessage, centerX - subtitleWidth / 2, centerY + 35, subtitlePaint);

        // Titolo del grafico
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
}