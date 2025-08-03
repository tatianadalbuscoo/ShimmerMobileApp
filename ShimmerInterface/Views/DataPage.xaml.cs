using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;
using XR2Learn_ShimmerAPI.IMU;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using SkiaSharp;
using ShimmerInterface.Models;

namespace ShimmerInterface.Views;

public partial class DataPage : ContentPage
{
    private readonly DataPageViewModel viewModel;

    public DataPage(XR2Learn_ShimmerIMU shimmer, ShimmerDevice sensorConfig)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

        // Subscribe to chart update events
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    #region Canvas Rendering Logic

    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(SKColors.White);

        // Check if sensor is disabled
        if (!IsSensorEnabled())
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        // Check if parameter is available
        if (!viewModel.AvailableParameters.Contains(viewModel.SelectedParameter))
        {
            DrawDisabledSensorMessage(canvas, info);
            return;
        }

        var margin = 40f;
        var bottomMargin = 65f;
        var leftMargin = 65f;
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

        // Draw grid if enabled
        if (viewModel.ShowGrid)
        {
            DrawOscilloscopeGrid(canvas, leftMargin, margin, graphWidth, graphHeight);
        }

        var yRange = viewModel.YAxisMax - viewModel.YAxisMin;
        var bottomY = margin + graphHeight;
        var topY = margin;

        // Calculate time range for X-axis mapping
        double currentTime = GetCurrentTimeInSeconds();
        double timeStart = Math.Max(0, currentTime - viewModel.TimeWindowSeconds);
        double timeRange = viewModel.TimeWindowSeconds;

        // Draw data based on chart display mode
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

        // Draw axes labels and title
        DrawAxesAndTitle(canvas, info, leftMargin, margin, graphWidth, graphHeight,
                        yRange, bottomY, timeStart);
    }

    private bool IsSensorEnabled()
    {
        string cleanName = CleanParameterName(viewModel.SelectedParameter);
        var config = viewModel.GetCurrentSensorConfiguration();

        return cleanName switch
        {
            "Low-Noise Accelerometer" or
            "Low-Noise AccelerometerX" or "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ"
                => config.EnableLowNoiseAccelerometer,

            "Wide-Range Accelerometer" or
            "Wide-Range AccelerometerX" or "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ"
                => config.EnableWideRangeAccelerometer,

            "Gyroscope" or
            "GyroscopeX" or "GyroscopeY" or "GyroscopeZ"
                => config.EnableGyroscope,

            "Magnetometer" or
            "MagnetometerX" or "MagnetometerY" or "MagnetometerZ"
                => config.EnableMagnetometer,

            "Temperature_BMP180" or "Pressure_BMP180"
                => config.EnablePressureTemperature,

            "BatteryVoltage" or "BatteryPercent"
                => config.EnableBattery,

            "ExtADC_A6" => config.EnableExtA6,
            "ExtADC_A7" => config.EnableExtA7,
            "ExtADC_A15" => config.EnableExtA15,
            _ => false
        };
    }

    private double GetCurrentTimeInSeconds()
    {
        return viewModel.CurrentTimeInSeconds;
    }

    private void DrawSingleParameter(SKCanvas canvas, float leftMargin, float margin,
                                    float graphWidth, float graphHeight, double yRange,
                                    float bottomY, float topY, double timeStart, double timeRange)
    {
        string cleanParameterName = CleanParameterName(viewModel.SelectedParameter);
        var currentDataPoints = viewModel.GetDataPoints(cleanParameterName);
        var currentTimeStamps = viewModel.GetTimeStamps(cleanParameterName);

        if (currentDataPoints.Count == 0 || currentDataPoints.All(v => v == -1 || v == 0))
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + 40),
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

    private void DrawMultipleParameters(SKCanvas canvas, float leftMargin, float margin,
                                       float graphWidth, float graphHeight, double yRange,
                                       float bottomY, float topY, double timeStart, double timeRange)
    {
        var subParameters = viewModel.GetCurrentSubParameters();
        var colors = GetParameterColors(CleanParameterName(viewModel.SelectedParameter));

        bool hasData = false;

        for (int paramIndex = 0; paramIndex < subParameters.Count; paramIndex++)
        {
            var parameter = subParameters[paramIndex];
            var currentDataPoints = viewModel.GetDataPoints(parameter);
            var currentTimeStamps = viewModel.GetTimeStamps(parameter);

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
                double normalizedX = (sampleTime - timeStart) / timeRange;
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

        if (!hasData)
        {
            DrawNoDataMessage(canvas, new SKImageInfo((int)(leftMargin + graphWidth + 40),
                                                     (int)(margin + graphHeight + 65)));
        }
    }

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

        // Horizontal grid lines (Y-axis)
        for (int i = 0; i <= horizontalDivisions; i++)
        {
            float y = bottom - (i * graphHeight / horizontalDivisions);
            canvas.DrawLine(leftMargin, y, right, y, majorGridPaint);
        }

        // Vertical grid lines (X-axis)
        for (int i = 0; i <= verticalDivisions; i++)
        {
            float x = leftMargin + (i * graphWidth / verticalDivisions);
            canvas.DrawLine(x, topMargin, x, bottom, majorGridPaint);
        }
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

        // X-axis labels
        int numDivisions = viewModel.TimeWindowSeconds;
        int labelInterval = viewModel.IsXAxisLabelIntervalEnabled ? viewModel.XAxisLabelInterval : 1;

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

        // Y-axis labels
        for (int i = 0; i <= 4; i++)
        {
            var value = viewModel.YAxisMin + (yRange * i / 4);
            var y = bottomY - (float)((value - viewModel.YAxisMin) / yRange * graphHeight);
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

        var yAxisLabelText = $"{viewModel.YAxisLabel} [{viewModel.YAxisUnit}]";
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

        var titleWidth = titlePaint.MeasureText(viewModel.ChartTitle);
        canvas.DrawText(viewModel.ChartTitle, (info.Width - titleWidth) / 2, 25, titlePaint);
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

        string cleanParameterName = CleanParameterName(viewModel.SelectedParameter);
        string sensorName = GetSensorDisplayName(cleanParameterName);

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

        DrawTitle(canvas, info);
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

        DrawTitle(canvas, info);
    }

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

    #endregion

    #region Helper Methods

    private string CleanParameterName(string displayName)
    {
        if (displayName.StartsWith("    → "))
        {
            return displayName.Substring(6);
        }
        return displayName;
    }

    private SKColor[] GetParameterColors(string groupParameter)
    {
        return groupParameter switch
        {
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
            "Gyroscope" or "Magnetometer" => new[] { SKColors.Red, SKColors.Green, SKColors.Blue },
            _ => new[] { SKColors.Blue }
        };
    }

    private string GetSensorDisplayName(string cleanParameterName)
    {
        return cleanParameterName switch
        {
            "Low-Noise Accelerometer" or "Low-Noise AccelerometerX" or
            "Low-Noise AccelerometerY" or "Low-Noise AccelerometerZ" => "Low-Noise Accelerometer",
            "Wide-Range Accelerometer" or "Wide-Range AccelerometerX" or
            "Wide-Range AccelerometerY" or "Wide-Range AccelerometerZ" => "Wide-Range Accelerometer",
            "Gyroscope" or "GyroscopeX" or "GyroscopeY" or "GyroscopeZ" => "Gyroscope",
            "Magnetometer" or "MagnetometerX" or "MagnetometerY" or "MagnetometerZ" => "Magnetometer",
            "Temperature_BMP180" => "Temperature (BMP180)",
            "Pressure_BMP180" => "Pressure (BMP180)",
            "BatteryVoltage" or "BatteryPercent" => "Battery",
            "ExtADC_A6" => "External ADC A6",
            "ExtADC_A7" => "External ADC A7",
            "ExtADC_A15" => "External ADC A15",
            _ => "Sensor"
        };
    }

    private string FormatTimeLabel(int timeValue)
    {
        return viewModel.TimeDisplayMode switch
        {
            TimeDisplayMode.Clock => DateTime.Now.AddSeconds(timeValue).ToString("HH:mm:ss"),
            _ => timeValue.ToString() + "s"
        };
    }

    #endregion

    #region Event Handlers

    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            canvasView.InvalidateSurface();
        });
    }

    #endregion

    #region Lifecycle Methods

    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.ResetStartTime();
        viewModel.StartTimer();
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    protected override void OnDisappearing()
    {
        viewModel.StopTimer();
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        viewModel?.Dispose();
        base.OnDisappearing();
    }

    #endregion
}