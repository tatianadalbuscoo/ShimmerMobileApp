using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI.IMU;
using XR2Learn_ShimmerAPI.GSR;
using SkiaSharp.Views.Maui;
using SkiaSharp;
using ShimmerInterface.Models;
using System.Linq; // <— necessario per Select/Where/Min/All/ToArray
using Microsoft.Maui.ApplicationModel; // per MainThread
using System.Threading.Tasks;

namespace ShimmerInterface.Views;

/// <summary>
/// Code-behind for the DataPage view, responsible for real-time rendering of sensor data using SkiaSharp.
/// Handles layout, axis drawing, sensor enablement checks, and visualization of both single and multiple parameters.
/// </summary>
public partial class DataPage : ContentPage
{
    // The ViewModel associated with this page
    private readonly DataPageViewModel viewModel;

    private bool _firstOpen = true;
    private XR2Learn_ShimmerIMU? _imu;




private readonly XR2Learn_ShimmerEXG? _exg;



    /// <summary>
    /// Initializes the DataPage, sets up the UI, binds the ViewModel,
    /// and subscribes to chart update events.
    /// </summary>
    /// <param name="shimmer">An instance of <see cref="XR2Learn_ShimmerIMU"/> representing the connected sensor device.</param>
    /// <param name="sensorConfig">A <see cref="ShimmerDevice"/> object containing the current sensor configuration flags.</param>
    public DataPage(XR2Learn_ShimmerIMU shimmer, ShimmerDevice sensorConfig)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        _imu = shimmer;
        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

        // Subscribe to chart update events
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;

        // Subscribe to busy/alert events (overlay + OK dialog)
        viewModel.ShowBusyRequested += OnShowBusyRequested;
        viewModel.HideBusyRequested += OnHideBusyRequested;
        viewModel.ShowAlertRequested += OnShowAlertRequested;
    }

    public DataPage(XR2Learn_ShimmerAPI.GSR.XR2Learn_ShimmerEXG shimmer, ShimmerDevice sensorConfig)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        _exg = shimmer;


        // 🟢 iOS/MacCatalyst: assicura che i flag EXG risultino “accesi”
#if IOS || MACCATALYST
    // Identifica la board come EXG (questo fa scattare i computed WantsExg/WantExgCh1/2)
    sensorConfig.IsExg = true;

    // Abilita proprio lo streaming EXG
    sensorConfig.EnableExg = true;

    // Se non è stata scelta nessuna modalità, metti ECG di default
    if (!(sensorConfig.IsExgModeECG || sensorConfig.IsExgModeEMG || sensorConfig.IsExgModeTest || sensorConfig.IsExgModeRespiration))
        sensorConfig.IsExgModeECG = true;
            // 👉 QUI sotto incolla il blocco con gli Enable IMU
    sensorConfig.EnableLowNoiseAccelerometer  = true;
    sensorConfig.EnableWideRangeAccelerometer = true;
    sensorConfig.EnableGyroscope              = true;
    sensorConfig.EnableMagnetometer           = true;
    sensorConfig.EnablePressureTemperature    = true;
    sensorConfig.EnableBattery                = true;
    sensorConfig.EnableExtA6 = true;
    sensorConfig.EnableExtA7 = true;
    sensorConfig.EnableExtA15 = true;

    // (opzionale) se vuoi andare subito sul gruppo EXG in grafico:
    // sensorConfig.SelectedExgMode = "ECG"; // è collegata ai radio
#endif

        viewModel = new DataPageViewModel(shimmer, sensorConfig); // usa il ctor EXG del VM
        BindingContext = viewModel;

        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
        viewModel.ShowBusyRequested += OnShowBusyRequested;
        viewModel.HideBusyRequested += OnHideBusyRequested;
        viewModel.ShowAlertRequested += OnShowAlertRequested;
    }


    /// <summary>
    /// Renders the chart surface using SkiaSharp, including background, grid, sensor data,
    /// axes labels, and chart title. Called automatically when the canvas needs to be redrawn.
    /// </summary>
    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Clear the entire canvas with a white background
        canvas.Clear(SKColors.White);

        // Check if sensor is disabled
        if (!IsSensorEnabled())
        {
            return;
        }

        // Check if parameter is available
        if (!viewModel.AvailableParameters.Contains(viewModel.SelectedParameter))
        {
            return;
        }

        // Define margins and calculate drawable area
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 120f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        // Draw oscilloscope-style grid if enabled by the user
        if (viewModel.ShowGrid)
        {
            DrawOscilloscopeGrid(canvas, leftMargin, margin, graphWidth, graphHeight);
        }

        // Calculate Y-axis range and vertical drawing bounds
        var yRange = viewModel.YAxisMax - viewModel.YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        // Calculate X-axis time window based on current time and configured duration
        double currentTime = GetCurrentTimeInSeconds();
        double timeStart = Math.Max(0, currentTime - viewModel.TimeWindowSeconds);
        double timeRange = viewModel.TimeWindowSeconds;

        // Draw sensor data: either as a single parameter or multiple sub-parameters (e.g., X/Y/Z)
        if (viewModel.ChartDisplayMode == ChartDisplayMode.Multi)
        {
            DrawMultipleParameters(canvas, leftMargin, margin, graphWidth, graphHeight,
                                  yRange, bottomY, topY, timeStart, timeRange);
        }
        else
        {
            DrawSingleParameter(canvas, leftMargin, margin, graphWidth, graphHeight,
                               yRange, bottomY, topY, timeStart, timeRange);
        }

        // Draw axis labels (time and units), Y-axis ticks, and chart title
        DrawAxesAndTitle(canvas, info, leftMargin, margin, graphWidth, graphHeight,
                        yRange, bottomY, timeStart);
    }

    /// <summary>
    /// Checks whether the currently selected sensor parameter is enabled in the current sensor configuration.
    /// </summary>
    private bool IsSensorEnabled()
    {
        // Remove any formatting from the parameter name (e.g., "→ ")
        string cleanName = CleanParameterName(viewModel.SelectedParameter);

        // Get the current sensor configuration object
        var config = viewModel.GetCurrentSensorConfiguration();

        // Return true if the sensor associated with the selected parameter is enabled
        return cleanName switch
        {
            // Low-noise accelerometer (full group or individual axis)
            "Low-Noise Accelerometer" or
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ"
                => config.EnableLowNoiseAccelerometer,

            // Wide-range accelerometer (full group or individual axis)
            "Wide-Range Accelerometer" or
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ"
                => config.EnableWideRangeAccelerometer,

            // Gyroscope (full group or individual axis)
            "Gyroscope" or
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ"
                => config.EnableGyroscope,

            // Magnetometer (full group or individual axis)
            "Magnetometer" or
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ"
                => config.EnableMagnetometer,

            // Pressure and temperature sensor (BMP180)
            "Temperature_BMP180" or "Pressure_BMP180"
                => config.EnablePressureTemperature,

            // Battery voltage or percentage
            "BatteryVoltage" or "BatteryPercent"
                => config.EnableBattery,

            // External ADC inputs
            "ExtADC_A6" => config.EnableExtA6,
            "ExtADC_A7" => config.EnableExtA7,
            "ExtADC_A15" => config.EnableExtA15,

            // EXG singoli canali (abilitati dal ViewModel/Config)
            "ExgCh1" => config.WantsExg && config.WantExgCh1,
            "ExgCh2" => config.WantsExg && config.WantExgCh2,
            "ExgRespiration" => config.WantsExg && config.WantRespiration,

                        // Gruppi EXG a 2 canali (mostriamo EXG1/EXG2 dentro ECG/EMG/EXG Test/Respiration)
            "EXG" or "ECG" or "EMG" or "EXG Test"
                            => viewModel.GetCurrentSensorConfiguration().EnableExg,

            // Respiration (solo se EXG attivo e modalità respiration)
            "Respiration" or "ExgRespiration"
                => viewModel.GetCurrentSensorConfiguration().EnableExg
                   && viewModel.GetCurrentSensorConfiguration().IsExgModeRespiration,



            // Any other unknown or unsupported parameter
            _ => false
        };
    }

    /// <summary>Retrieves the current elapsed time in seconds, as tracked by the ViewModel.</summary>
    private double GetCurrentTimeInSeconds() => viewModel.CurrentTimeInSeconds;

    /// <summary>Draws a single sensor parameter as a line chart.</summary>
    private void DrawSingleParameter(
        SKCanvas canvas, float leftMargin, float margin,
        float graphWidth, float graphHeight, double yRange,
        float bottomY, float topY, double timeStart, double timeRange)
    {
        string cleanParameterName = CleanParameterName(viewModel.SelectedParameter);
        var (currentDataPoints, currentTimeStamps) = viewModel.GetSeriesSnapshot(cleanParameterName);

        // Snapshot + guard-rails
        var values = currentDataPoints.ToArray();
        var times = currentTimeStamps.ToArray(); // ms

        int count = Math.Min(values.Length, times.Length);
        if (count == 0 || values.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + 40),
                (int)(margin + graphHeight + 65)));
            return;
        }

        double minSampleTime = times.Length > 0 ? (times.Min() / 1000.0) : 0;
        double safeYRange = yRange > 0 ? yRange : 1e-9;
        double safeTimeRange = timeRange > 0 ? timeRange : 1e-9;

        using var linePaint = new SKPaint
        {
            Color = SKColors.Blue,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        using var path = new SKPath();

        for (int i = 0; i < count; i++)
        {
            double sampleTime = times[i] / 1000.0;
            double normalizedX = (sampleTime - minSampleTime) / safeTimeRange;
            var x = leftMargin + (float)(normalizedX * graphWidth);

            var normalizedValue = (values[i] - viewModel.YAxisMin) / safeYRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);
            y = Math.Clamp(y, topY, bottomY);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);
    }

    /// <summary>Draws multiple sub-parameters (e.g., X/Y/Z) as colored line charts.</summary>
    private void DrawMultipleParameters(
        SKCanvas canvas,
        float leftMargin, float margin,
        float graphWidth, float graphHeight,
        double yRange,
        float bottomY, float topY,
        double timeStart, double timeRange)
    {
        var subParameters = viewModel.GetCurrentSubParameters();

        double minSampleTime = subParameters
            .Select(p => viewModel.GetSeriesSnapshot(p).time)
            .Where(t => t.Count > 0)
            .Select(t => t.Min() / 1000.0)
            .DefaultIfEmpty(0)
            .Min();

        var colors = GetParameterColors(CleanParameterName(viewModel.SelectedParameter));
        bool hasData = false;

        double safeYRange = yRange > 0 ? yRange : 1e-9;
        double safeTimeRange = timeRange > 0 ? timeRange : 1e-9;

        for (int paramIndex = 0; paramIndex < subParameters.Count; paramIndex++)
        {
            var parameter = subParameters[paramIndex];
            var (currentDataPoints, currentTimeStamps) = viewModel.GetSeriesSnapshot(parameter);

            var values = currentDataPoints.ToArray();
            var times = currentTimeStamps.ToArray(); // ms

            int count = Math.Min(values.Length, times.Length);
            if (count == 0 || values.All(v => v == -1 || v == 0))
                continue;

            using var linePaint = new SKPaint
            {
                Color = colors[paramIndex % colors.Length],
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };
            using var path = new SKPath();

            for (int i = 0; i < count; i++)
            {
                double sampleTime = times[i] / 1000.0; // s
                double normalizedX = (sampleTime - minSampleTime) / safeTimeRange;
                var x = leftMargin + (float)(normalizedX * graphWidth);

                double normalizedValue = (values[i] - viewModel.YAxisMin) / safeYRange;
                var y = bottomY - (float)(normalizedValue * graphHeight);
                y = Math.Clamp(y, topY, bottomY);

                if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }

            canvas.DrawPath(path, linePaint);
            hasData = true;
        }

        if (!hasData)
        {
            DrawNoDataMessage(canvas, new SKImageInfo(
                (int)(leftMargin + graphWidth + 40),
                (int)(margin + graphHeight + 65)));
        }
    }

    /// <summary>Draws a dashed oscilloscope-style grid on the chart area.</summary>
    private void DrawOscilloscopeGrid(SKCanvas canvas, float leftMargin, float topMargin,
                                     float graphWidth, float graphHeight)
    {
        float right = leftMargin + graphWidth;
        float bottom = topMargin + graphHeight;

        int horizontalDivisions = 4;
        int verticalDivisions = viewModel.TimeWindowSeconds;

        using var majorGridPaint = new SKPaint
        {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
        };

        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float y = bottom - (i * graphHeight / horizontalDivisions);
            canvas.DrawLine(leftMargin, y, right, y, majorGridPaint);
        }

        for (int i = 0; i <= verticalDivisions; i++)
        {
            float x = leftMargin + (i * graphWidth / verticalDivisions);
            canvas.DrawLine(x, topMargin, x, bottom, majorGridPaint);
        }
    }

    /// <summary>Draws the X and Y axis labels, axis units, and the chart title on the canvas.</summary>
    private void DrawAxesAndTitle(
     SKCanvas canvas, SKImageInfo info, float leftMargin,
     float margin, float graphWidth, float graphHeight,
     double yRange, float bottomY, double timeStart,
     string? titleOverride = null) // <— nuovo parametro opzionale
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // Etichette asse X (tempo)
        int numDivisions = Math.Max(1, viewModel.TimeWindowSeconds);
        int labelInterval = viewModel.IsXAxisLabelIntervalEnabled
            ? Math.Max(1, viewModel.XAxisLabelInterval)
            : 1;

        for (int i = 0; i <= numDivisions; i++)
        {
            double actualTime = timeStart + (i * viewModel.TimeWindowSeconds / (double)numDivisions);
            int timeValueForLabel = (int)Math.Floor(actualTime);
            if (timeValueForLabel < 0 || (timeValueForLabel % labelInterval != 0)) continue;

            float x = leftMargin + (i * graphWidth / numDivisions);
            string label = FormatTimeLabel((int)actualTime);
            var textWidth = textPaint.MeasureText(label);
            canvas.DrawText(label, x - textWidth / 2, bottomY + 20, textPaint);
        }

        // Etichette numeriche asse Y
        double safeYRange = Math.Abs(yRange) < 1e-12 ? 1e-12 : yRange;
        for (int i = 0; i <= 4; i++)
        {
            var value = viewModel.YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)(((value - viewModel.YAxisMin) / safeYRange) * graphHeight);
            var label = value.ToString("F3");
            canvas.DrawText(label, leftMargin - 72, y + 6, textPaint);
        }

        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Nomi degli assi
        string xAxisLabel = "Time [s]";
        var labelX = (info.Width - axisLabelPaint.MeasureText(xAxisLabel)) / 2;
        var labelY = info.Height - 8;
        canvas.DrawText(xAxisLabel, labelX, labelY, axisLabelPaint);

        var yAxisLabelText = $"{viewModel.YAxisLabel} [{viewModel.YAxisUnit}]";
        canvas.Save();
        canvas.Translate(30, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

        // Titolo (con possibile override, es. "… — Axis X")
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };

        var chartTitle = titleOverride ?? viewModel.ChartTitle;
        var titleWidth = titlePaint.MeasureText(chartTitle);
        canvas.DrawText(chartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }


    /// <summary>Draws a placeholder message indicating no valid data is available.</summary>
    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 100f;
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

        DrawTitle(canvas, info);
    }

    /// <summary>Draws the chart title centered at the top of the canvas.</summary>
    private void DrawTitle(SKCanvas canvas, SKImageInfo info)
    {
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };
        var titleWidth = titlePaint.MeasureText(viewModel.ChartTitle);
        canvas.DrawText(viewModel.ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }

    /// <summary>Removes formatting prefix from a parameter display name.</summary>
    private static string CleanParameterName(string displayName)
        => DataPageViewModel.CleanParameterName(displayName);

    /// <summary>Returns colors for grouped sensor parameters (X/Y/Z).</summary>
    private SKColor[] GetParameterColors(string groupParameter)
    {
        return groupParameter switch
        {
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
            "Gyroscope" or "Magnetometer" => new[] { SKColors.Red, SKColors.Green, SKColors.Blue },


            // EXG-based groups: 2 canali (CH1/CH2) in tutte le modalità
            "EXG" or "ECG" or "EMG" or "EXG Test" or "Respiration" => new[] { SKColors.Red, SKColors.Blue },

            _ => new[] { SKColors.Blue }
        };
    }


    /// <summary>Formats a time value for the X-axis.</summary>
    private string FormatTimeLabel(int timeValue) => timeValue + "s";

    /// <summary>Handles chart update requests.</summary>
    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (viewModel.ChartDisplayMode == ChartDisplayMode.Split)
            {
                canvasX?.InvalidateSurface();
                canvasY?.InvalidateSurface();
                canvasZ?.InvalidateSurface();
            }
            else
            {
                canvasView?.InvalidateSurface();
            }
        });
    }


    // ===== Split view paint handlers (one canvas per axis) =====
    private void OnCanvasXPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 0); // X
    private void OnCanvasYPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 1); // Y
    private void OnCanvasZPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 2); // Z

    // Draw a single axis (X=0, Y=1, Z=2) of the current group in Split mode
    private void DrawSplitAxis(SKPaintSurfaceEventArgs e, int axisIndex)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.White);

        // Guard: if Split is not active, nothing to draw here
        if (viewModel.ChartDisplayMode != ChartDisplayMode.Split)
            return;

        // Resolve current group (e.g., Gyroscope, Magnetometer, ...)
        var group = CleanParameterName(viewModel.SelectedParameter);
        if (!IsGroupEnabled(group))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Chart layout (same margins as the unified chart)
        float margin = 40f, bottomMargin = 65f, leftMargin = 120f;
        float w = info.Width - leftMargin - margin;
        float h = info.Height - margin - bottomMargin;

        using (var border = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
            canvas.DrawRect(leftMargin, margin, w, h, border);

        if (viewModel.ShowGrid)
            DrawOscilloscopeGrid(canvas, leftMargin, margin, w, h);

        // Ranges and time window
        double yRange = viewModel.YAxisMax - viewModel.YAxisMin;
        float bottomY = margin + h, topY = margin;
        double currentTime = GetCurrentTimeInSeconds();
        double timeStart = Math.Max(0, currentTime - viewModel.TimeWindowSeconds);
        double timeRange = viewModel.TimeWindowSeconds;

        // === Supporto EXG split: EXG1/EXG2 separati (Z nascosto dal XAML) ===
        string key;
        SKColor strokeColor;

        if (viewModel.IsExgSplit)
        {
            // 0 -> EXG1 (rosso), 1 -> EXG2 (blu), 2 -> nessun grafico
            key = axisIndex == 0 ? "ExgCh1" :
                  axisIndex == 1 ? "ExgCh2" : "";
            if (string.IsNullOrEmpty(key)) return; // niente terzo grafico in split EXG
            strokeColor = axisIndex == 0 ? SKColors.Red : SKColors.Blue;
        }
        else
        {
            // Caso IMU XYZ standard
            var trio = DataPageViewModel.GetSubParameters(group);
            if (trio.Count < 3) { DrawNoDataMessage(canvas, info); return; }
            key = trio[axisIndex];
            strokeColor = new[] { SKColors.Red, SKColors.Green, SKColors.Blue }[axisIndex];
        }

        var (data, timeMs) = viewModel.GetSeriesSnapshot(key);
        int count = Math.Min(data.Count, timeMs.Count);
        if (count == 0 || data.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        double minSampleTime = timeMs.Min() / 1000.0;
        double safeY = yRange > 0 ? yRange : 1e-9;
        double safeT = timeRange > 0 ? timeRange : 1e-9;

        using var paint = new SKPaint
        {
            Color = strokeColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        using var path = new SKPath();

        for (int i = 0; i < count; i++)
        {
            double t = timeMs[i] / 1000.0;
            float x = leftMargin + (float)(((t - minSampleTime) / safeT) * w);

            float y = bottomY - (float)(((data[i] - viewModel.YAxisMin) / safeY) * h);
            y = Math.Clamp(y, topY, bottomY);

            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }

        canvas.DrawPath(path, paint);

        // Titolo corretto (EXG1/EXG2 in split EXG)
        string splitTitle;
        if (viewModel.IsExgSplit)
        {
            string exgLabel = axisIndex == 0 ? "EXG1" : "EXG2";
            splitTitle = $"Real-time {group} — {exgLabel}";
        }
        else
        {
            string axisLetter = axisIndex == 0 ? "X" : axisIndex == 1 ? "Y" : "Z";
            splitTitle = $"Real-time {group} — Axis {axisLetter}";
        }

        DrawAxesAndTitle(canvas, info, leftMargin, margin, w, h, yRange, bottomY, timeStart, splitTitle);


    }

    // Helper: check if the whole group is enabled
    private bool IsGroupEnabled(string group)
    {
        var cfg = viewModel.GetCurrentSensorConfiguration();
        return group switch
        {
            "Low-Noise Accelerometer" => cfg.EnableLowNoiseAccelerometer,
            "Wide-Range Accelerometer" => cfg.EnableWideRangeAccelerometer,
            "Gyroscope" => cfg.EnableGyroscope,
            "Magnetometer" => cfg.EnableMagnetometer,

            // EXG (gruppi a due canali)
            "EXG" => cfg.EnableExg,
            "ECG" => cfg.EnableExg && cfg.IsExgModeECG,
            "EMG" => cfg.EnableExg && cfg.IsExgModeEMG,
            "EXG Test" => cfg.EnableExg && cfg.IsExgModeTest,
            "Respiration" => cfg.EnableExg && cfg.IsExgModeRespiration,

            _ => false
        };
    }




    // ============== NUOVI HANDLER PER WARNING OVERLAY + ALERT ==============

    private void OnShowBusyRequested(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BusyLabel.Text = string.IsNullOrWhiteSpace(message)
                ? "Working… Please wait."
                : message;
            BusyOverlay.IsVisible = true;
        });
    }

    private void OnHideBusyRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BusyOverlay.IsVisible = false;
        });
    }

    private async void OnShowAlertRequested(object? sender, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Sampling Rate", message ?? "Done.", "OK");
        });
    }

    // =======================================================================

    protected override void OnDisappearing()
    {
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;

        viewModel.ShowBusyRequested -= OnShowBusyRequested;
        viewModel.HideBusyRequested -= OnHideBusyRequested;
        viewModel.ShowAlertRequested -= OnShowAlertRequested;

        viewModel.DetachFromDevice();

        base.OnDisappearing();
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if IOS || MACCATALYST
    try
    {
        // IMU (già presente)
        if (_imu != null && !_imu.IsConnected())
        {
            _imu.Connect();
            await Task.Delay(100);
        }
        _imu?.StartStreaming();

        // ⬇️ INCOLLA QUI IL BLOCCO EXG ⬇️
        if (_exg != null && !_exg.IsConnected())
        {
            _exg.Connect();
            await Task.Delay(100);
        }
        _exg?.StartStreaming();
        // ⬆️ FINE BLOCCO EXG ⬆️
    }
    catch (Exception ex)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            DisplayAlert("Bridge", $"Connection to bridge failed:\n{ex.Message}", "OK"));
    }
#endif



        // (ri)aggancia eventi UI se necessario, come già fai:
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;

        viewModel.ShowBusyRequested -= OnShowBusyRequested;
        viewModel.ShowBusyRequested += OnShowBusyRequested;

        viewModel.HideBusyRequested -= OnHideBusyRequested;
        viewModel.HideBusyRequested += OnHideBusyRequested;

        viewModel.ShowAlertRequested -= OnShowAlertRequested;
        viewModel.ShowAlertRequested += OnShowAlertRequested;

        // riattacca al device
        viewModel.AttachToDevice();

        // SOLO alla primissima apertura di questa pagina:
        if (_firstOpen)
        {
            // true = pulisco i buffer e riparto da 0 con il grafico vuoto
            // usa false se vuoi tenere eventuali dati ma far partire l’asse X da 0
            viewModel.MarkFirstOpenBaseline(clearBuffers: true);
            _firstOpen = false;
        }

        // Invalidate the proper canvases based on the current display mode
        if (viewModel.ChartDisplayMode == ChartDisplayMode.Split)
        {
            canvasX?.InvalidateSurface();
            canvasY?.InvalidateSurface();
            canvasZ?.InvalidateSurface();
        }
        else
        {
            canvasView?.InvalidateSurface();
        }

    }



}
