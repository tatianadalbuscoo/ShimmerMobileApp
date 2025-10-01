/* 
 * DataPage code-behind:
 * wires up DataPageViewModel for IMU/EXG devices and binds page UI;
 * renders real-time charts with SkiaSharp (unified overlay or split panels);
 * manages busy overlay and sampling-rate alerts from ViewModel events;
 * aligns EXG mode on iOS/MacCatalyst and auto-selects EXG group on first sample;
 * handles connection/start/stop and event subscriptions in OnAppearing/OnDisappearing.
 */


using ShimmerInterface.ViewModels;
using ShimmerSDK.IMU;
using ShimmerSDK.EXG;
using SkiaSharp.Views.Maui;
using SkiaSharp;
using ShimmerInterface.Models;


namespace ShimmerInterface.Views;


/// <summary>
/// Code-behind for the DataPage XAML view.
/// Displays real-time IMU/EXG data from a Shimmer device with configurable SkiaSharp charts.
/// Wires up the DataPageViewModel, handles page lifecycle, and responds to chart/busy/alert events.
/// </summary>
public partial class DataPage : ContentPage
{

    // The ViewModel associated with this page
    private readonly DataPageViewModel viewModel;

    // True only on first page open to run one-time init
    private bool _firstOpen = true;

    // Set by the IMU constructor; null when using the EXG constructor.
    private ShimmerSDK_IMU? _imu;

    // Set by the EXG constructor; null when using the IMU constructor.
    private readonly ShimmerSDK_EXG? _exg;


#if IOS || MACCATALYST

    // Tracks if the EXG group has already been auto-selected once.
    private bool _exgGroupAutoSelected = false;

#endif


    /// <summary>
    /// Gets the elapsed time in seconds from the ViewModel’s internal clock (used for charting).
    /// </summary>
    /// <returns>The elapsed time in seconds.</returns>
    private double GetCurrentTimeInSeconds() => viewModel.CurrentTimeInSeconds;


    /// <summary>
    /// Strips any UI-only prefix/symbols from a parameter display name (e.g., trims "→ ").
    /// </summary>
    /// <param name="displayName">The user-facing parameter label to sanitize.</param>
    /// <returns>The clean, canonical parameter name used by the ViewModel.</returns>
    private static string CleanParameterName(string displayName)
        => DataPageViewModel.CleanParameterName(displayName);


    /// <summary>
    /// Formats a tick label for the time axis as seconds (e.g., "3s").
    /// </summary>
    /// <param name="timeValue">Whole seconds to display on the X axis.</param>
    /// <returns>A string with the seconds value followed by "s".</returns>
    private string FormatTimeLabel(int timeValue) => timeValue + "s";


    /// <summary>
    /// Paint handler for the first split canvas. Delegates to <see cref="DrawSplitAxis"/> with index 0:
    /// IMU = Axis X; EXG split = EXG1.
    /// </summary>
    /// <param name="sender">The SKCanvasView that raised the event.</param>
    /// <param name="e">Paint arguments providing the drawing surface and info.</param>
    private void OnCanvasFirstPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 0);


    /// <summary>
    /// Paint handler for the second split canvas. Delegates to <see cref="DrawSplitAxis"/> with index 1:
    /// IMU = Axis Y; EXG split = EXG2.
    /// </summary>
    /// <param name="sender">The SKCanvasView that raised the event.</param>
    /// <param name="e">Paint arguments providing the drawing surface and info.</param>
    private void OnCanvasSecondPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 1);


    /// <summary>
    /// Paint handler for the third split canvas. Delegates to <see cref="DrawSplitAxis"/> with index 2:
    /// IMU = Axis Z; EXG split = not used (third panel hidden).
    /// </summary>
    /// <param name="sender">The SKCanvasView that raised the event.</param>
    /// <param name="e">Paint arguments providing the drawing surface and info.</param>
    private void OnCanvasThirdPaintSurface(object sender, SKPaintSurfaceEventArgs e) => DrawSplitAxis(e, 2);


    /// <summary>
    /// IMU constructor: initializes UI, binds the ViewModel, and hooks page-level events.
    /// </summary>
    /// <param name="shimmer">
    /// An instance of <see cref="ShimmerSDK_IMU"/> representing the connected IMU device.
    /// </param>
    /// <param name="sensorConfig">
    /// A <see cref="ShimmerInterface.Models.ShimmerDevice"/> with the current sensor flags.
    /// </param>
    public DataPage(ShimmerSDK_IMU shimmer, ShimmerDevice sensorConfig)
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


    /// <summary>
    /// EXG constructor: initializes UI, binds the ViewModel, aligns bridge mode on iOS/macOS, and hooks page-level events.
    /// </summary>
    /// <param name="shimmer">
    /// An instance of <see cref="ShimmerSDK.EXG.ShimmerSDK_EXG"/> representing the connected EXG device.
    /// </param>
    /// <param name="sensorConfig">
    /// A <see cref="ShimmerInterface.Models.ShimmerDevice"/> with the current sensor flags.
    /// </param>
    public DataPage(ShimmerSDK_EXG shimmer, ShimmerDevice sensorConfig)
    {

        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        _exg = shimmer;

#if IOS || MACCATALYST

        // Mark this board as EXG so computed flags (WantsExg/WantExg1/2) light up
        sensorConfig.IsExg = true;

        // Enable EXG streaming
        sensorConfig.EnableExg = true;

        // Mirror current bridge mode into radio flags (defaults resolved here)
        var mode = (_exg?.CurrentExgMode ?? "").Trim().ToLowerInvariant();
        sensorConfig.IsExgModeECG         = mode == "ecg";
        sensorConfig.IsExgModeEMG         = mode == "emg";
        sensorConfig.IsExgModeTest        = mode == "test";
        sensorConfig.IsExgModeRespiration = mode == "resp" || mode == "respiration";

#endif

        // Bind EXG ViewModel and wire up page bindings
        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

#if IOS || MACCATALYST

        bool anyImu =
            sensorConfig.EnableLowNoiseAccelerometer ||
            sensorConfig.EnableWideRangeAccelerometer ||
            sensorConfig.EnableGyroscope ||
            sensorConfig.EnableMagnetometer ||
            sensorConfig.EnablePressureTemperature ||
            sensorConfig.EnableBattery ||
            sensorConfig.EnableExtA6 ||
            sensorConfig.EnableExtA7 ||
            sensorConfig.EnableExtA15;

        if (anyImu)
        {

            // Pick the first available IMU group in a sensible order
            if (sensorConfig.EnableLowNoiseAccelerometer)
                viewModel.SelectedParameter = "Low-Noise Accelerometer";
            else if (sensorConfig.EnableWideRangeAccelerometer)
                viewModel.SelectedParameter = "Wide-Range Accelerometer";
            else if (sensorConfig.EnableGyroscope)
                viewModel.SelectedParameter = "Gyroscope";
            else if (sensorConfig.EnableMagnetometer)
                viewModel.SelectedParameter = "Magnetometer";
        }
        else
        {

            // EXG-only: align selection with bridge mode at startup
            var bridgeMode2 = (_exg?.CurrentExgMode ?? "").Trim().ToLowerInvariant();
            if (bridgeMode2 == "resp" || bridgeMode2 == "respiration")
                viewModel.SelectedParameter = "Respiration";
            else if (bridgeMode2 == "ecg")
                viewModel.SelectedParameter = "ECG";
            else if (bridgeMode2 == "emg")
                viewModel.SelectedParameter = "EMG";
            else if (bridgeMode2 == "test")
                viewModel.SelectedParameter = "EXG Test";

            // Subscribe once to pick the correct EXG group after first sample
            if (_exg != null)
                _exg.SampleReceived += OnFirstExgSampleSelectGroupOnce;
        }

#endif

        // Hook ViewModel → View events (chart refresh + busy/alert)
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
        viewModel.ShowBusyRequested += OnShowBusyRequested;
        viewModel.HideBusyRequested += OnHideBusyRequested;
        viewModel.ShowAlertRequested += OnShowAlertRequested;
    }


#if IOS || MACCATALYST

    /// <summary>
    /// On iOS/MacCatalyst, handles the first EXG sample to auto-select the mode (ECG/EMG/Test/Respiration),
    /// request a chart refresh, and then unsubscribes so it runs only once.
    /// </summary>
    /// <param name="sender">The EXG device instance that raised the event.</param>
    /// <param name="e">The incoming sample payload (dynamic); not directly used, only signals the first sample.</param>
    private void OnFirstExgSampleSelectGroupOnce(object? sender, dynamic e)
    {
        if (_exgGroupAutoSelected) return;  // Do nothing if already auto-selected
        _exgGroupAutoSelected = true;       // Mark as done to prevent future runs

        var mode = (_exg?.CurrentExgMode ?? "").Trim().ToLowerInvariant();

        // Switch UI selection on the main thread based on the reported EXG mode
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (mode == "resp" || mode == "respiration")
                viewModel.SelectedParameter = "Respiration";
            else if (mode == "ecg")
                viewModel.SelectedParameter = "ECG";
            else if (mode == "emg")
                viewModel.SelectedParameter = "EMG";
            else if (mode == "test")
                viewModel.SelectedParameter = "EXG Test";

            OnChartUpdateRequested(this, EventArgs.Empty);
        });

        // Unsubscribe: this handler must run only once
        if (_exg != null)
            _exg.SampleReceived -= OnFirstExgSampleSelectGroupOnce;
    }

#endif


    /// <summary>
    /// Paint handler for the unified (non-split) chart: clears the canvas, draws border/grid,
    /// renders the overlaid multi-series for the current time window (no-op when Split),
    /// then draws axes, tick labels, and title. Invoked on each SkiaSharp redraw.
    /// </summary>
    /// <param name="sender">The <see cref="SkiaSharp.Views.Maui.Controls.SKCanvasView"/> that raised the event.</param>
    /// <param name="e">Event args providing the surface info and drawing canvas.</param>
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

        // Render data series (overlaid multi-series when unified chart is in Multi mode)
        if (viewModel.ChartDisplayMode == ChartDisplayMode.Multi)
        {
            DrawMultipleParameters(canvas, leftMargin, margin, graphWidth, graphHeight,
                                  yRange, bottomY, topY, timeStart, timeRange);
        }

        // Draw axes, tick labels, units, and chart title
        DrawAxesAndTitle(canvas, info, leftMargin, margin, graphWidth, graphHeight,
                        yRange, bottomY, timeStart);
    }


    /// <summary>
    /// Returns true if the selected parameter maps to a sensor that is enabled
    /// in the current device configuration.
    /// </summary>
    /// <returns><c>true</c> if the selected parameter's sensor is enabled; otherwise, <c>false</c>.</returns>
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

            // EXG single channels
            "Exg1" => config.WantsExg && config.WantExg1,
            "Exg2" => config.WantsExg && config.WantExg2,

            // EXG 2-channel groups (EXG1/EXG2 shown under these modes)
            "ECG" or "EMG" or "EXG Test" or "Respiration"
                => viewModel.GetCurrentSensorConfiguration().EnableExg,

            _ => false
        };
    }


    /// <summary>
    /// Draws multiple sub-parameters (e.g., X/Y/Z or EXG1/EXG2) as colored line charts
    /// within the provided plotting area.
    /// </summary>
    /// <param name="canvas">The Skia canvas to draw on.</param>
    /// <param name="leftMargin">Left pixel margin of the plot area.</param>
    /// <param name="margin">Top pixel margin of the plot area.</param>
    /// <param name="graphWidth">Width (in pixels) of the drawable plot area.</param>
    /// <param name="graphHeight">Height (in pixels) of the drawable plot area.</param>
    /// <param name="yRange">Y-axis span (YAxisMax − YAxisMin) used for value scaling.</param>
    /// <param name="bottomY">Bottom Y pixel of the plot area (lower bound).</param>
    /// <param name="topY">Top Y pixel of the plot area (upper bound).</param>
    /// <param name="timeStart">Start time (in seconds) of the visible window.</param>
    /// <param name="timeRange">Duration (in seconds) of the visible window.</param>
    private void DrawMultipleParameters(
        SKCanvas canvas,
        float leftMargin, float margin,
        float graphWidth, float graphHeight,
        double yRange,
        float bottomY, float topY,
        double timeStart, double timeRange)
    {

        // Get the series keys to plot (e.g., X/Y/Z or EXG1/EXG2)
        var subParameters = viewModel.GetCurrentSubParameters();

        // Earliest visible timestamp across all sub-series (in seconds)
        double minSampleTime = subParameters
            .Select(p => viewModel.GetSeriesSnapshot(p).time)
            .Where(t => t.Count > 0)
            .Select(t => t.Min() / 1000.0)
            .DefaultIfEmpty(0)
            .Min();

        // Color palette per sub-series (consistent by parameter group)
        var colors = GetParameterColors(CleanParameterName(viewModel.SelectedParameter));
        bool hasData = false;

        // Guard against zero ranges to avoid division by zero
        double safeYRange = yRange > 0 ? yRange : 1e-9;
        double safeTimeRange = timeRange > 0 ? timeRange : 1e-9;

        for (int paramIndex = 0; paramIndex < subParameters.Count; paramIndex++)
        {
            var parameter = subParameters[paramIndex];
            var (currentDataPoints, currentTimeStamps) = viewModel.GetSeriesSnapshot(parameter);

            // Snapshot arrays (values + matching timestamps in ms)
            var values = currentDataPoints.ToArray();
            var times = currentTimeStamps.ToArray();

            // Keep pairs aligned; skip empty or all invalid series
            int count = Math.Min(values.Length, times.Length);
            if (count == 0 || values.All(v => v == -1 || v == 0))
                continue;

            // Style for this series
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

                // Map time (s) to X within current window
                double sampleTime = times[i] / 1000.0;
                double normalizedX = (sampleTime - minSampleTime) / safeTimeRange;
                var x = leftMargin + (float)(normalizedX * graphWidth);

                // Map value to Y within current Y range
                double normalizedValue = (values[i] - viewModel.YAxisMin) / safeYRange;
                var y = bottomY - (float)(normalizedValue * graphHeight);
                y = Math.Clamp(y, topY, bottomY);

                // Build polyline path
                if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }

            // Render this sub-series
            canvas.DrawPath(path, linePaint);
            hasData = true;
        }

        // If nothing valid was drawn, show a placeholder message
        if (!hasData)
        {
            DrawNoDataMessage(canvas, new SKImageInfo(
                (int)(leftMargin + graphWidth + 40),
                (int)(margin + graphHeight + 65)));
        }
    }


    /// <summary>
    /// Renders one split chart panel (X=0, Y=1, Z=2 or EXG1/EXG2) using SkiaSharp:
    /// clears, draws frame/grid, plots the requested series, and writes axes/title.
    /// </summary>
    /// <param name="e">Skia paint args (surface, canvas, size).</param>
    /// <param name="axisIndex">
    /// Index of the split panel to draw: 0/1/2 for IMU X/Y/Z or 0/1 for EXG1/EXG2.
    /// </param>
    private void DrawSplitAxis(SKPaintSurfaceEventArgs e, int axisIndex)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.White);

        // Only draw in Split mode (otherwise this canvas is inactive)
        if (viewModel.ChartDisplayMode != ChartDisplayMode.Split)
            return;

        // Resolve and validate the current sensor group
        var group = CleanParameterName(viewModel.SelectedParameter);
        if (!IsGroupEnabled(group))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Compute plot area (match unified margins)
        float margin = 40f, bottomMargin = 65f, leftMargin = 120f;
        float w = info.Width - leftMargin - margin;
        float h = info.Height - margin - bottomMargin;

        // Draw plot border
        using (var border = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
            canvas.DrawRect(leftMargin, margin, w, h, border);

        // Optional grid
        if (viewModel.ShowGrid)
            DrawOscilloscopeGrid(canvas, leftMargin, margin, w, h);

        // Scale/time window
        double yRange = viewModel.YAxisMax - viewModel.YAxisMin;
        float bottomY = margin + h, topY = margin;
        double currentTime = GetCurrentTimeInSeconds();
        double timeStart = Math.Max(0, currentTime - viewModel.TimeWindowSeconds);
        double timeRange = viewModel.TimeWindowSeconds;

        // Pick the series key and color for this panel (EXG split or IMU XYZ)
        string key;
        SKColor strokeColor;

        if (viewModel.IsExgSplit)
        {
            
            // axisIndex: 0→EXG1, 1→EXG2 (third panel unused)
            key = axisIndex == 0 ? "Exg1" :
                  axisIndex == 1 ? "Exg2" : "";
            if (string.IsNullOrEmpty(key)) return; // no third EXG panel
            strokeColor = axisIndex == 0 ? SKColors.Red : SKColors.Blue;
        }
        else
        {

            // Standard IMU split (X/Y/Z)
            var trio = DataPageViewModel.GetSubParameters(group);
            if (trio.Count < 3) { DrawNoDataMessage(canvas, info); return; }
            key = trio[axisIndex];
            strokeColor = new[] { SKColors.Red, SKColors.Green, SKColors.Blue }[axisIndex];
        }

        // Snapshot series data
        var (data, timeMs) = viewModel.GetSeriesSnapshot(key);
        int count = Math.Min(data.Count, timeMs.Count);
        if (count == 0 || data.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Safe scaling
        double minSampleTime = timeMs.Min() / 1000.0;
        double safeY = yRange > 0 ? yRange : 1e-9;
        double safeT = timeRange > 0 ? timeRange : 1e-9;

        // Plot line
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

        // Title suffix per panel
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

        // Axes + title
        DrawAxesAndTitle(canvas, info, leftMargin, margin, w, h, yRange, bottomY, timeStart, splitTitle);

    }


    /// <summary>
    /// Determines whether the specified sensor group is currently enabled
    /// according to the active sensor configuration (IMU groups and EXG modes).
    /// </summary>
    /// <param name="group">Clean group name (e.g., "Low-Noise Accelerometer", "Wide-Range Accelerometer" "Gyroscope", 
    /// "Magnetometer", "ECG", "EMG", "EXG Test", "Respiration").</param>
    /// <returns><c>true</c> if the group is enabled in the current configuration; otherwise <c>false</c>.</returns>
    private bool IsGroupEnabled(string group)
    {
        var cfg = viewModel.GetCurrentSensorConfiguration();
        return group switch
        {

            // IMU groups
            "Low-Noise Accelerometer" => cfg.EnableLowNoiseAccelerometer,
            "Wide-Range Accelerometer" => cfg.EnableWideRangeAccelerometer,
            "Gyroscope" => cfg.EnableGyroscope,
            "Magnetometer" => cfg.EnableMagnetometer,

            // EXG (two-channel groups)
            "ECG" => cfg.EnableExg && cfg.IsExgModeECG,
            "EMG" => cfg.EnableExg && cfg.IsExgModeEMG,
            "EXG Test" => cfg.EnableExg && cfg.IsExgModeTest,
            "Respiration" => cfg.EnableExg && cfg.IsExgModeRespiration,

            _ => false
        };
    }


    /// <summary>
    /// Draws a dashed, oscilloscope-style grid within the plotting area.
    /// Horizontal divisions are fixed; vertical divisions follow the time window (seconds).
    /// </summary>
    /// <param name="canvas">Target Skia canvas.</param>
    /// <param name="leftMargin">Left padding of the plot region.</param>
    /// <param name="topMargin">Top padding of the plot region.</param>
    /// <param name="graphWidth">Drawable width of the plot region.</param>
    /// <param name="graphHeight">Drawable height of the plot region.</param>
    private void DrawOscilloscopeGrid(SKCanvas canvas, float leftMargin, float topMargin,
                                     float graphWidth, float graphHeight)
    {
        float right = leftMargin + graphWidth;
        float bottom = topMargin + graphHeight;

        // 4 fixed horizontal bands (0..4)
        int horizontalDivisions = 4;

        // 1 vertical line per second in the window
        int verticalDivisions = viewModel.TimeWindowSeconds;

        using var majorGridPaint = new SKPaint
        {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)   // dashed style
        };

        // Horizontal lines (top → bottom)
        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float y = bottom - (i * graphHeight / horizontalDivisions);
            canvas.DrawLine(leftMargin, y, right, y, majorGridPaint);
        }

        // Vertical lines (left → right)
        for (int i = 0; i <= verticalDivisions; i++)
        {
            float x = leftMargin + (i * graphWidth / verticalDivisions);
            canvas.DrawLine(x, topMargin, x, bottom, majorGridPaint);
        }
    }


    /// <summary>
    /// Draws time labels (X), numeric ticks (Y), axis captions (with units),
    /// and the chart title. Supports an optional title override.
    /// </summary>
    /// <param name="canvas">Target Skia canvas to draw on.</param>
    /// <param name="info">Surface info (used for centering text).</param>
    /// <param name="leftMargin">Left padding of the plot area.</param>
    /// <param name="margin">Top padding of the plot area.</param>
    /// <param name="graphWidth">Width of the drawable plot area.</param>
    /// <param name="graphHeight">Height of the drawable plot area.</param>
    /// <param name="yRange">Range on Y (max - min) for tick placement.</param>
    /// <param name="bottomY">Canvas Y coordinate of the plot bottom.</param>
    /// <param name="timeStart">Start time (s) for the visible window.</param>
    /// <param name="titleOverride">Optional custom title; falls back to ViewModel.</param>
    private void DrawAxesAndTitle(
     SKCanvas canvas, SKImageInfo info, float leftMargin,
     float margin, float graphWidth, float graphHeight,
     double yRange, float bottomY, double timeStart,
     string? titleOverride = null)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // X-axis tick labels (seconds)
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

        // Y-axis numeric ticks (4 segments)
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

        // Axis captions
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

        // Title (centered at top; use override if provided)
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


    /// <summary>
    /// Draws a framed placeholder over the plot area and a centered
    /// "No valid data available" message, then renders the chart title.
    /// </summary>
    /// <param name="canvas">Target Skia canvas to draw on.</param>
    /// <param name="info">Surface size used to compute plot bounds and centering.</param>
    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {

        // Compute plot rectangle (same margins as normal chart)
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
            Color = SKColors.OrangeRed,     // high-contrast status color
            TextSize = 24,
            IsAntialias = true,
            FakeBoldText = true
        };

        var message = "No valid data available";
        var messageWidth = messagePaint.MeasureText(message);
        var centerX = leftMargin + graphWidth / 2;
        var centerY = margin + graphHeight / 2;

        // Center the message within the plot area
        canvas.DrawText(message, centerX - messageWidth / 2, centerY, messagePaint);

        // Keep the usual title at the top for context
        DrawTitle(canvas, info);

    }


    /// <summary>
    /// Draws the current chart title centered at the top of the canvas.
    /// </summary>
    /// <param name="canvas">Target Skia canvas to render the title on.</param>
    /// <param name="info">Surface info used to compute horizontal centering.</param>
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

        // Center horizontally at y = 25px from the top
        canvas.DrawText(viewModel.ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }


    /// <summary>
    /// Provides the stroke colors used to plot grouped parameters.
    /// IMU groups (Accel/Gyro/Mag) return RGB for X/Y/Z; EXG modes return two colors for EXG1/EXG2.
    /// Falls back to a single blue line for unknown groups.
    /// </summary>
    /// <param name="groupParameter">Canonical group name (e.g., "Gyroscope", "ECG").</param>
    /// <returns>
    /// An array of <see cref="SKColor"/> values to apply per series in the group
    /// (e.g., [Red, Green, Blue] for XYZ; [Red, Blue] for EXG1/EXG2).
    /// </returns>
    private SKColor[] GetParameterColors(string groupParameter)
    {
        return groupParameter switch
        {
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
            "Gyroscope" or "Magnetometer" => new[] { SKColors.Red, SKColors.Green, SKColors.Blue },

            // EXG-based groups: two series (EXG1/EXG2) in all modes
            "EXG" or "ECG" or "EMG" or "EXG Test" or "Respiration" => new[] { SKColors.Red, SKColors.Blue },

            _ => new[] { SKColors.Blue }
        };
    }


    /// <summary>
    /// Handles a ViewModel "refresh chart" request by invalidating the proper canvas.
    /// Ensures the call runs on the UI thread and refreshes either the split canvases
    /// or the unified canvas based on the current display mode.
    /// </summary>
    /// <param name="sender">The ViewModel raising the refresh request.</param>
    /// <param name="e">Event arguments (unused).</param>
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


    /// <summary>
    /// Shows the full-screen busy overlay with a message (runs on UI thread).
    /// </summary>
    /// <param name="sender">The ViewModel raising the request.</param>
    /// <param name="message">Optional custom text; falls back to a default if blank.</param>
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


    /// <summary>
    /// Hides the full-screen busy overlay (runs on UI thread).
    /// </summary>
    /// <param name="sender">The ViewModel raising the request.</param>
    /// <param name="e">Event args (unused).</param>
    private void OnHideBusyRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BusyOverlay.IsVisible = false;
        });
    }


    /// <summary>
    /// Displays a modal alert about the sampling rate on the UI thread.
    /// </summary>
    /// <param name="sender">The ViewModel raising the request.</param>
    /// <param name="message">Alert body text; defaults to "Done." if null.</param>
    private async void OnShowAlertRequested(object? sender, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Sampling Rate", message ?? "Done.", "OK");
        });
    }


    /// <summary>
    /// Page lifecycle: stops streaming (iOS/Mac), unsubscribes event handlers, detaches from the device,
    /// calls the base implementation, and (if present) removes the one-shot EXG sample handler.
    /// </summary>
    protected override async void OnDisappearing()
    {

#if IOS || MACCATALYST

    await viewModel.StopAsync(disconnect: false);

#endif

        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        viewModel.ShowBusyRequested -= OnShowBusyRequested;
        viewModel.HideBusyRequested -= OnHideBusyRequested;
        viewModel.ShowAlertRequested -= OnShowAlertRequested;

        viewModel.DetachFromDevice();

        base.OnDisappearing();

#if IOS || MACCATALYST

        if (_exg != null)
            _exg.SampleReceived -= OnFirstExgSampleSelectGroupOnce;

#endif

    }


    /// <summary>
    /// Page lifecycle: ensures connection/start on iOS/Mac, (re)wires UI/event handlers,
    /// re-attaches to the device, initializes the first-open baseline, and refreshes the
    /// correct canvas based on the current display mode.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if IOS || MACCATALYST

        await viewModel.ConnectAndStartAsync();

#endif

        // (Re)wire UI events defensively to prevent duplicate subscriptions
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;

        viewModel.ShowBusyRequested -= OnShowBusyRequested;
        viewModel.ShowBusyRequested += OnShowBusyRequested;

        viewModel.HideBusyRequested -= OnHideBusyRequested;
        viewModel.HideBusyRequested += OnHideBusyRequested;

        viewModel.ShowAlertRequested -= OnShowAlertRequested;
        viewModel.ShowAlertRequested += OnShowAlertRequested;

        // Re-attach to the current device/session
        viewModel.AttachToDevice();

        // First-time entry: reset buffers/baseline once
        if (_firstOpen)
        {
            viewModel.MarkFirstOpenBaseline(clearBuffers: true);
            _firstOpen = false;
        }

        // Redraw the appropriate canvas(es) based on the active chart mode
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
