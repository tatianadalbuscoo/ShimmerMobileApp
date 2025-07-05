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
    private readonly List<float> dataPoints = new();
    private int secondsElapsed = 0;

    [ObservableProperty]
    private string sensorText = "Waiting for data...";

    // Evento per notificare quando il grafico deve essere ridisegnato
    public event EventHandler? ChartUpdateRequested;

    public DataPageViewModel(XR2Learn_ShimmerGSR shimmerDevice)
    {
        shimmer = shimmerDevice;
        StartTimer();
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

            // Aggiorna il grafico
            UpdateChart();
        };
        timer.Start();
    }

    private void UpdateChart()
    {
        secondsElapsed++;

        // Alterna tra 0 e 1 ogni 5 secondi
        float value = ((secondsElapsed / 5) % 2 == 0) ? 0f : 1f;

        // Aggiungi sempre il nuovo punto
        dataPoints.Add(value);

        // Mantieni solo gli ultimi 20 punti (finestra scorrevole)
        if (dataPoints.Count > 20)
        {
            dataPoints.RemoveAt(0);
        }

        // Notifica che il grafico deve essere ridisegnato
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    // Metodo per disegnare il grafico
    public void OnCanvasViewPaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        // Pulisce il canvas
        canvas.Clear(SKColors.White);

        if (dataPoints.Count == 0) return;

        // Definisce i margini e l'area del grafico (ridotti proporzionalmente)
        var margin = 40f;
        var bottomMargin = 65f; // Margine inferiore per l'etichetta dell'asse X
        var leftMargin = 65f;   // Margine sinistro per l'etichetta dell'asse Y
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

        // Disegna le linee di riferimento (Y = 0 e Y = 1)
        using var gridLinePaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
        };

        var centerY = margin + graphHeight * 0.75f; // Y = 0
        var topY = margin + graphHeight * 0.25f;    // Y = 1

        canvas.DrawLine(leftMargin, centerY, leftMargin + graphWidth, centerY, gridLinePaint);
        canvas.DrawLine(leftMargin, topY, leftMargin + graphWidth, topY, gridLinePaint);

        // Disegna i punti dati
        using var linePaint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        using var path = new SKPath();
        for (int i = 0; i < dataPoints.Count; i++)
        {
            var x = leftMargin + (i * graphWidth / Math.Max(dataPoints.Count - 1, 1));
            // Scala i dati: 0 -> centerY, 1 -> topY
            var y = centerY - (dataPoints[i] * (centerY - topY));

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
            TextSize = 18, // Ridotto per grafico più piccolo
            IsAntialias = true
        };

        // Etichette ogni 1 secondo
        for (int i = 0; i < dataPoints.Count; i++)
        {
            var x = leftMargin + (i * graphWidth / Math.Max(dataPoints.Count - 1, 1));

            // Calcola il tempo basato sulla posizione nella finestra scorrevole
            int timeValue = Math.Max(1, secondsElapsed - dataPoints.Count + i + 1);

            var displayTime = timeValue.ToString() + "s";
            var textWidth = textPaint.MeasureText(displayTime);
            canvas.DrawText(displayTime, x - textWidth / 2, margin + graphHeight + 20, textPaint);
        }

        // Etichette Y
        canvas.DrawText("0", leftMargin - 20, centerY + 6, textPaint);
        canvas.DrawText("1", leftMargin - 20, topY + 6, textPaint);

        // Etichette degli assi
        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Etichetta asse X (Time[seconds])
        var xAxisLabel = "Time[seconds]";
        var xAxisLabelWidth = axisLabelPaint.MeasureText(xAxisLabel);
        canvas.DrawText(xAxisLabel, (info.Width - xAxisLabelWidth) / 2, info.Height - 5, axisLabelPaint);

        // Etichetta asse Y (undefined[?]) - ruotata di 90 gradi
        var yAxisLabel = "undefined[?]";
        canvas.Save();
        canvas.Translate(15, (info.Height + axisLabelPaint.MeasureText(yAxisLabel)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabel, 0, 0, axisLabelPaint);
        canvas.Restore();

        // Titolo del grafico
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20, // Ridotto ulteriormente
            IsAntialias = true,
            FakeBoldText = true
        };
        var title = "Real-time Data";
        var titleWidth = titlePaint.MeasureText(title);
        canvas.DrawText(title, (info.Width - titleWidth) / 2, 25, titlePaint);
    }
}