using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI.IMU;
using SkiaSharp.Views.Maui;
using SkiaSharp;
using ShimmerInterface.Models;

namespace ShimmerInterface.Views;

/// <summary>
/// Code-behind for the DataPage view, responsible for real-time rendering of sensor data using SkiaSharp.
/// Handles layout, axis drawing, sensor enablement checks, and visualization of both single and multiple parameters.
/// </summary>
public partial class DataPage : ContentPage
{

    // The ViewModel associated with this page
    private readonly DataPageViewModel viewModel;


    /// <summary>
    /// Initializes a new instance of the <see cref="DataPage"/> class.
    /// Sets up the UI, disables the navigation back button, binds the ViewModel,
    /// and subscribes to chart update events triggered by the ViewModel.
    /// </summary>
    /// <param name="shimmer">An instance of <see cref="XR2Learn_ShimmerIMU"/> representing the connected sensor device.</param>
    /// <param name="sensorConfig">A <see cref="ShimmerDevice"/> object containing the current sensor configuration flags.</param>
    public DataPage(XR2Learn_ShimmerIMU shimmer, ShimmerDevice sensorConfig)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

        // Subscribe to chart update events
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }


    /// <summary>
    /// Renders the chart surface using SkiaSharp, including background, grid, sensor data,
    /// axes labels, and chart title. Called automatically when the canvas needs to be redrawn.
    /// </summary>
    /// <param name="sender">The source of the paint event.</param>
    /// <param name="e">Contains the drawing surface and image information for rendering.</param>
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
    /// Checks whether the currently selected sensor parameter is enabled
    /// in the current sensor configuration.
    /// </summary>
    /// <returns>True if the sensor is enabled; otherwise, false.</returns>
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

            // Any other unknown or unsupported parameter
            _ => false
        };
    }


    /// <summary>
    /// Retrieves the current elapsed time in seconds, as tracked by the ViewModel.
    /// </summary>
    /// <returns>The current time in seconds.</returns>
    private double GetCurrentTimeInSeconds()
    {
        return viewModel.CurrentTimeInSeconds;
    }


    /// <summary>
    /// Draws a single data parameter (e.g., AccelerometerX) as a line chart on the canvas.
    /// Converts each data point into screen coordinates based on time and value range,
    /// and plots them as a continuous path.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="leftMargin">Left margin in pixels for chart alignment.</param>
    /// <param name="margin">Top margin in pixels.</param>
    /// <param name="graphWidth">Width of the drawable graph area.</param>
    /// <param name="graphHeight">Height of the drawable graph area.</param>
    /// <param name="yRange">Value range on the Y-axis (max - min).</param>
    /// <param name="bottomY">Bottom Y position for plotting (pixel coordinate).</param>
    /// <param name="topY">Top Y position for plotting (pixel coordinate).</param>
    /// <param name="timeStart">Start time for the X-axis window (in seconds).</param>
    /// <param name="timeRange">Total time span displayed on the X-axis (in seconds).</param>
    private void DrawSingleParameter(SKCanvas canvas, float leftMargin, float margin,
    float graphWidth, float graphHeight, double yRange,
    float bottomY, float topY, double timeStart, double timeRange)
    {
        string cleanParameterName = CleanParameterName(viewModel.SelectedParameter);
        var (currentDataPoints, currentTimeStamps) = viewModel.GetSeriesSnapshot(cleanParameterName);

        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + 40),
                (int)(margin + graphHeight + 65)));
            return;
        }

        // TROVA IL MINIMO tra i timestamp (in secondi)
        var allTimes = currentTimeStamps.Select(t => t / 1000.0).ToList();
        double minSampleTime = allTimes.Count > 0 ? allTimes.Min() : 0;

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
            double normalizedX = (sampleTime - minSampleTime) / timeRange; // SLITTA A SINISTRA!
            var x = leftMargin + (float)(normalizedX * graphWidth);

            var normalizedValue = (currentDataPoints[i] - viewModel.YAxisMin) / yRange;
            var y = bottomY - (float)(normalizedValue * graphHeight);
            y = Math.Clamp(y, topY, bottomY);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);
    }



    /// <summary>
    /// Draws multiple sub-parameters (e.g., X, Y, Z components of a sensor)
    /// as separate colored lines on the same chart.
    /// Each parameter is plotted using its own color for visual distinction.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="leftMargin">Left margin of the chart area.</param>
    /// <param name="margin">Top margin of the chart area.</param>
    /// <param name="graphWidth">Width of the drawable graph area.</param>
    /// <param name="graphHeight">Height of the drawable graph area.</param>
    /// <param name="yRange">Range of Y-axis values (max - min).</param>
    /// <param name="bottomY">Bottom pixel coordinate of the chart area.</param>
    /// <param name="topY">Top pixel coordinate of the chart area.</param>
    /// <param name="timeStart">Start of the visible time window (in seconds).</param>
    /// <param name="timeRange">Total duration of the visible time window (in seconds).</param>
    private void DrawMultipleParameters(
    SKCanvas canvas,
    float leftMargin, float margin,
    float graphWidth, float graphHeight,
    double yRange,
    float bottomY, float topY,
    double timeStart, double timeRange)
    {
        // Ottieni tutti i sub-parametri da visualizzare (es: X,Y,Z)
        var subParameters = viewModel.GetCurrentSubParameters();

        // === TROVA IL MINIMO ASSOLUTO DI TEMPO TRA TUTTI I SUB-PARAMETER ===
        // In modo che tutte le linee partano dalla stessa X=0
        double minSampleTime = subParameters
            .Select(p => viewModel.GetSeriesSnapshot(p).time)
            .Where(t => t.Count > 0)
            .Select(t => t.Min() / 1000.0)
            .DefaultIfEmpty(0)
            .Min();

        // Colori per ogni curva
        var colors = GetParameterColors(CleanParameterName(viewModel.SelectedParameter));
        bool hasData = false;

        // Ciclo su tutti i sub-parametri (X, Y, Z ecc.)
        for (int paramIndex = 0; paramIndex < subParameters.Count; paramIndex++)
        {
            var parameter = subParameters[paramIndex];
            var (currentDataPoints, currentTimeStamps) = viewModel.GetSeriesSnapshot(parameter);

            // Salta se non ci sono dati validi
            if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
                continue;

            hasData = true;

            using var linePaint = new SKPaint
            {
                Color = colors[paramIndex % colors.Length],
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };
            using var path = new SKPath();

            for (int i = 0; i < currentDataPoints.Count; i++)
            {
                double sampleTime = currentTimeStamps[i] / 1000.0;
                double normalizedX = (sampleTime - minSampleTime) / timeRange; // SLITTAMENTO!
                var x = leftMargin + (float)(normalizedX * graphWidth);

                var normalizedValue = (currentDataPoints[i] - viewModel.YAxisMin) / yRange;
                var y = bottomY - (float)(normalizedValue * graphHeight);
                y = Math.Clamp(y, topY, bottomY);

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }

            canvas.DrawPath(path, linePaint);
        }

        // Nessun dato valido: mostra messaggio di errore
        if (!hasData)
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + 40),
                (int)(margin + graphHeight + 65)));
        }
    }



    /// <summary>
    /// Draws a dashed oscilloscope-style grid on the chart area,
    /// with horizontal divisions for Y-axis and vertical divisions for the X-axis
    /// based on the configured time window.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="leftMargin">The left margin of the drawing area.</param>
    /// <param name="topMargin">The top margin of the drawing area.</param>
    /// <param name="graphWidth">The width of the drawable graph area.</param>
    /// <param name="graphHeight">The height of the drawable graph area.</param>
    private void DrawOscilloscopeGrid(SKCanvas canvas, float leftMargin, float topMargin,
                                     float graphWidth, float graphHeight)
    {

        // Calculate right and bottom boundaries of the grid
        float right = leftMargin + graphWidth;
        float bottom = topMargin + graphHeight;

        // Define number of divisions on each axis
        int horizontalDivisions = 4;
        int verticalDivisions = viewModel.TimeWindowSeconds;

        // Create dashed line style for the grid
        using var majorGridPaint = new SKPaint
        {
            Color = SKColors.LightSlateGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
        };

        // Draw horizontal grid lines (Y-axis)
        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float y = bottom - (i * graphHeight / horizontalDivisions);
            canvas.DrawLine(leftMargin, y, right, y, majorGridPaint);
        }

        // Draw vertical grid lines (X-axis)
        for (int i = 0; i <= verticalDivisions; i++)
        {
            float x = leftMargin + (i * graphWidth / verticalDivisions);
            canvas.DrawLine(x, topMargin, x, bottom, majorGridPaint);
        }
    }


    /// <summary>
    /// Draws the X and Y axis labels, axis units, and the chart title on the canvas.
    /// X-axis is labeled with time values; Y-axis with measurement units.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw.</param>
    /// <param name="info">The surface size and pixel information.</param>
    /// <param name="leftMargin">Left margin of the chart area.</param>
    /// <param name="margin">Top margin of the chart area.</param>
    /// <param name="graphWidth">Width of the graph area.</param>
    /// <param name="graphHeight">Height of the graph area.</param>
    /// <param name="yRange">The full range of values on the Y-axis.</param>
    /// <param name="bottomY">The bottom Y coordinate of the chart drawing area.</param>
    /// <param name="timeStart">The starting time (in seconds) for the X-axis.</param>
    private void DrawAxesAndTitle(SKCanvas canvas, SKImageInfo info, float leftMargin,
                                 float margin, float graphWidth, float graphHeight,
                                 double yRange, float bottomY, double timeStart)
    {

        // Define general-purpose paint for axis tick labels
        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 18,
            IsAntialias = true
        };

        // === Draw X-axis tick labels ===
        int numDivisions = viewModel.TimeWindowSeconds;
        int labelInterval = viewModel.IsXAxisLabelIntervalEnabled ? viewModel.XAxisLabelInterval : 1;

        for (int i = 0; i <= numDivisions; i++)
        {

            // Compute actual time value at this division
            double actualTime = timeStart + (i * viewModel.TimeWindowSeconds / (double)numDivisions);
            int timeValueForLabel = (int)Math.Floor(actualTime);

            // Skip labels not matching the configured interval
            if (timeValueForLabel < 0 || (timeValueForLabel % labelInterval != 0)) continue;

            // Compute X coordinate and draw the label centered
            float x = leftMargin + (i * graphWidth / numDivisions);
            string label = FormatTimeLabel((int)actualTime);
            var textWidth = textPaint.MeasureText(label);
            canvas.DrawText(label, x - textWidth / 2, bottomY + 20, textPaint);
        }

        // === Draw Y-axis tick labels ===
        for (int i = 0; i <= 4; i++)
        {

            // Compute Y value and corresponding canvas coordinate
            var value = viewModel.YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - viewModel.YAxisMin) / yRange * graphHeight);
            var label = value.ToString("F3");
            canvas.DrawText(label, leftMargin - 72, y + 6, textPaint);
        }

        // === Draw axis labels (X and Y titles) ===
        using var axisLabelPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Draw X-axis label centered horizontally at the bottom
        string xAxisLabel = "Time [s]";
        var labelX = (info.Width - axisLabelPaint.MeasureText(xAxisLabel)) / 2;
        var labelY = info.Height - 8;
        canvas.DrawText(xAxisLabel, labelX, labelY, axisLabelPaint);

        // Draw Y-axis label rotated vertically on the left
        var yAxisLabelText = $"{viewModel.YAxisLabel} [{viewModel.YAxisUnit}]";
        canvas.Save();
        canvas.Translate(30, (info.Height + axisLabelPaint.MeasureText(yAxisLabelText)) / 2);
        canvas.RotateDegrees(-90);
        canvas.DrawText(yAxisLabelText, 0, 0, axisLabelPaint);
        canvas.Restore();

        // === Draw chart title ===
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


    /// <summary>
    /// Draws a placeholder message on the canvas indicating that no valid data is available
    /// to render.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="info">Information about the drawing surface, such as width and height.</param>
    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {

        // Define graph boundaries and margins
        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 100f;
        var graphWidth = info.Width - leftMargin - margin;
        var graphHeight = info.Height - margin - bottomMargin;

        // Draw border around the chart area
        using var borderPaint = new SKPaint
        {
            Color = SKColors.LightGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, borderPaint);

        // Draw semi-transparent background inside the chart area
        using var backgroundPaint = new SKPaint
        {
            Color = SKColors.LightGray.WithAlpha(100),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(leftMargin, margin, graphWidth, graphHeight, backgroundPaint);

        // Draw main message in the center of the chart
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

        // Draw the chart title above the message
        DrawTitle(canvas, info);
    }


    /// <summary>
    /// Draws the chart title centered at the top of the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw the title on.</param>
    /// <param name="info">Provides the size of the drawing surface.</param>
    private void DrawTitle(SKCanvas canvas, SKImageInfo info)
    {

        // Define paint style for the title text
        using var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true,
            FakeBoldText = true
        };

        // Measure text width to center it horizontally
        var titleWidth = titlePaint.MeasureText(viewModel.ChartTitle);

        // Draw title at fixed Y-position (25px from top), horizontally centered
        canvas.DrawText(viewModel.ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
    }


    /// <summary>
    /// Removes leading formatting symbols (e.g., arrows or indentation)
    /// from a parameter display name to extract its raw name.
    /// </summary>
    /// <param name="displayName">The display name shown in the UI (possibly formatted).</param>
    /// <returns>The cleaned parameter name without formatting.</returns>
    private string CleanParameterName(string displayName)
    {

        // Remove leading arrow and indentation (used for visual nesting in dropdowns)
        if (displayName.StartsWith("    → "))
        {
            return displayName.Substring(6);
        }

        // Return the name unchanged if no formatting is found
        return displayName;
    }


    /// <summary>
    /// Returns a predefined set of colors for visualizing grouped sensor parameters,
    /// such as X/Y/Z components of an accelerometer or gyroscope.
    /// </summary>
    /// <param name="groupParameter">The name of the sensor group (e.g., "Accelerometer").</param>
    /// <returns>An array of SKColor values to use when rendering each sub-parameter.</returns>
    private SKColor[] GetParameterColors(string groupParameter)
    {

        // Assign RGB colors for known grouped sensors (e.g., X = red, Y = green, Z = blue)
        return groupParameter switch
        {
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
            "Gyroscope" or "Magnetometer" => new[] { SKColors.Red, SKColors.Green, SKColors.Blue },

            // Default to blue if the parameter is not part of a known group
            _ => new[] { SKColors.Blue }
        };
    }


    /// <summary>
    /// Formats a time value as seconds with "s" suffix for X-axis display.
    /// </summary>
    /// <param name="timeValue">The time value in seconds to format.</param>
    /// <returns>A string representing the time in seconds (e.g., "12s").</returns>
    private string FormatTimeLabel(int timeValue)
    {
        return timeValue.ToString() + "s";
    }


    /// <summary>
    /// Handles the chart update request event triggered by the ViewModel.
    /// Forces the canvas to redraw on the main UI thread.
    /// </summary>
    /// <param name="sender">The source of the event (typically the ViewModel).</param>
    /// <param name="e">The event arguments (unused).</param>
    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            canvasView.InvalidateSurface();
        });
    }


    /// <summary>
    /// Called when the page becomes visible. Initializes timing and subscribes to
    /// chart update events to enable real-time rendering of sensor data.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reset the time reference used for the X-axis
        //viewModel.ResetStartTime();

        // Start the internal timer that triggers chart updates
        viewModel.StartTimer();

        // Subscribe to chart update requests
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }


    /// <summary>
    /// Called when the page is about to disappear. Stops timers,
    /// unsubscribes from events, and disposes resources to prevent leaks.
    /// </summary>
    protected override void OnDisappearing()
    {

        // Stop the chart update timer
        viewModel.StopTimer();

        // Unsubscribe from chart update events to prevent memory leaks
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;

        // Dispose of the ViewModel if it holds unmanaged resources or background tasks
        //viewModel?.Dispose();

        base.OnDisappearing();
    }

}
