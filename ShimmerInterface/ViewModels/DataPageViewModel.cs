using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI.IMU;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;

namespace ShimmerInterface.ViewModels;


/// <summary>
/// Specifies the chart visualization mode: either a single parameter (e.g., only X),
/// or multiple parameters (e.g., X, Y, Z on the same chart).
/// </summary>
public enum ChartDisplayMode
{
    Single,
    Multi
}


/// <summary>
/// ViewModel for the DataPage.
/// Manages real-time data acquisition, sensor configuration, and chart display options for a connected Shimmer device.
/// Exposes observable properties and commands for UI binding, following the MVVM pattern.
/// Implements IDisposable for proper cleanup of timers and resources.
/// </summary>
public partial class DataPageViewModel : ObservableObject, IDisposable
{


    // ==== Application-wide numeric limits for validation ====
    private const double MAX_DOUBLE = 1e6;
    private const double MIN_DOUBLE = -1e6;
    private const double MAX_Y_AXIS = 100_000;
    private const double MIN_Y_AXIS = -100_000;
    private const int MAX_TIME_WINDOW_SECONDS = 600;    // 10 minutes
    private const int MIN_TIME_WINDOW_SECONDS = 1;
    private const int MAX_X_AXIS_LABEL_INTERVAL = 1000;
    private const int MIN_X_AXIS_LABEL_INTERVAL = 1;
    private const double MAX_SAMPLING_RATE = 1000;
    private const double MIN_SAMPLING_RATE = 1;

    // ==== Device references and internal timer for periodic updates ====
    private readonly XR2Learn_ShimmerIMU shimmer; // Reference to the connected Shimmer device
    private System.Timers.Timer? timer;
    private bool _disposed = false;

    // ==== Data storage for real-time series ====
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();
    private readonly object _dataLock = new();
    private int sampleCounter = 0;

    // ==== Sensor enablement flags (from current device config) ====
    // These indicate which sensors are enabled for this device/session
    private bool enableLowNoiseAccelerometer;
    private bool enableWideRangeAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;
    private bool enablePressureTemperature;
    private bool enableBattery;
    private bool enableExtA6;
    private bool enableExtA7;
    private bool enableExtA15;

    // ==== Last valid values for restoring user input ====
    private double _lastValidYAxisMin = 0;
    private double _lastValidYAxisMax = 1;
    private int _lastValidTimeWindowSeconds = 20;
    private int _lastValidXAxisLabelInterval = 5;
    private double _lastValidSamplingRate = 51.2;

    // ==== Backing fields for user input (text entry fields) ====
    private string _yAxisMinText = "0";
    private string _yAxisMaxText = "1";
    private string _timeWindowSecondsText = "20";
    private string _xAxisLabelIntervalText = "5";
    private string _samplingRateText = "51.2";

    // ==== Temporary values for auto-range Y axis calculation ====
    private double _autoYAxisMin = 0;
    private double _autoYAxisMax = 1;

    // ==== MVVM Bindable Properties ====
    // These properties are observable and used for data binding in the UI
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
    private bool showGrid = true;

    [ObservableProperty]
    private bool autoYAxis = false;

    [ObservableProperty]
    private bool isYAxisManualEnabled = true;

    [ObservableProperty]
    private ChartDisplayMode chartDisplayMode = ChartDisplayMode.Single;

    // ==== Public properties and events ====

    /// <summary>
    /// Gets the list of available parameters that can be displayed or selected for charting,
    /// based on the current enabled sensors.
    /// </summary>
    public ObservableCollection<string> AvailableParameters { get; } = new();

    /// <summary>
    /// Event triggered when the chart needs to be updated.
    /// The view subscribes to this to redraw the chart in real time.
    /// </summary>
    public event EventHandler? ChartUpdateRequested;

    // ==== Public calculated property ====

    /// <summary>
    /// Gets the current elapsed time in seconds since data collection started.
    /// </summary>
    public double CurrentTimeInSeconds => sampleCounter / shimmer.SamplingRate;


    /// <summary>
    /// Gets or sets the sampling rate value entered by the user (string for binding).
    /// Triggers validation and updates sampling logic on change.
    /// </summary>
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


    /// <summary>
    /// Gets or sets the minimum value for the Y axis, as entered by the user (text binding).
    /// Triggers validation and updates the chart when changed.
    /// </summary>
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


    /// <summary>
    /// Gets or sets the maximum value for the Y axis, as entered by the user (text binding).
    /// Triggers validation and updates the chart when changed.
    /// </summary>
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


    /// <summary>
    /// Gets or sets the time window (in seconds) for data display, as entered by the user (text binding).
    /// Triggers validation and resets the chart window when changed.
    /// </summary>
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


    /// <summary>
    /// Gets or sets the interval (in seconds) between X axis labels, as entered by the user (text binding).
    /// Triggers validation and updates the chart when changed.
    /// </summary>
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


    // ==== IDisposable implementation ====


    /// <summary>
    /// Releases all resources used by this ViewModel, including timers and event handlers.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// Performs the actual resource cleanup.
    /// Stops timers, clears data collections, and unsubscribes events.
    /// </summary>
    /// <param name="disposing">True if called from Dispose; false if called from the finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            StopTimer();                         // Stop the periodic update timer
            ChartUpdateRequested = null;         // Unsubscribe all chart update event handlers
            ClearAllDataCollections();           // Clear all sensor data buffers
        }
        _disposed = true;
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="DataPageViewModel"/> class.
    /// Configures the ViewModel based on the selected Shimmer device and its sensor configuration,
    /// initializes available parameters, default axis settings, and prepares the state for real-time data acquisition.
    /// </summary>
    /// <param name="shimmerDevice">The connected Shimmer IMU device.</param>
    /// <param name="config">The sensor configuration object indicating which sensors are enabled.</param>
    public DataPageViewModel(XR2Learn_ShimmerIMU shimmerDevice, ShimmerDevice config)
    {
        shimmer = shimmerDevice;

        // Store which sensors are enabled for this session
        enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
        enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;
        enablePressureTemperature = config.EnablePressureTemperature;
        enableBattery = config.EnableBattery;
        enableExtA6 = config.EnableExtA6;
        enableExtA7 = config.EnableExtA7;
        enableExtA15 = config.EnableExtA15;

        // Initialize the sampling rate display property
        samplingRateDisplay = shimmer.SamplingRate;

        // Populate list of available chart parameters
        InitializeAvailableParameters();

        // Ensure the selected parameter is valid; otherwise, select the first available
        if (!AvailableParameters.Contains(SelectedParameter))
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

        // Create collections for only those parameters with real sensor data
        InitializeDataCollections();

        // Set up axis and chart settings based on the selected parameter
        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
            IsXAxisLabelIntervalEnabled = SelectedParameter != "HeartRate";
        }

        // Store the last valid input values for validation and restore
        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _samplingRateText = shimmer.SamplingRate.ToString(CultureInfo.InvariantCulture);
        _lastValidSamplingRate = shimmer.SamplingRate;
        OnPropertyChanged(nameof(SamplingRateText));
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        // Sync UI entry fields to current state
        UpdateTextProperties();
    }


    /// <summary>
    /// Initializes the internal collections for time series data storage,
    /// creating an entry for each enabled sensor parameter.
    /// Only parameters with real data (not group labels) are included.
    /// </summary>
    private void InitializeDataCollections()
    {

        // List all parameter names that will actually store real sensor data (no groups)
        var dataParameters = new List<string>();

        // Add axes for low-noise accelerometer if enabled
        if (enableLowNoiseAccelerometer)
            dataParameters.AddRange(new[] { "Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ" });

        // Add axes for wide-range accelerometer if enabled
        if (enableWideRangeAccelerometer)
            dataParameters.AddRange(new[] { "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" });

        // Add axes for gyroscope if enabled
        if (enableGyroscope)
            dataParameters.AddRange(new[] { "GyroscopeX", "GyroscopeY", "GyroscopeZ" });

        // Add axes for magnetometer if enabled
        if (enableMagnetometer)
            dataParameters.AddRange(new[] { "MagnetometerX", "MagnetometerY", "MagnetometerZ" });

        // Add environmental sensor parameters if enabled
        if (enablePressureTemperature)
            dataParameters.AddRange(new[] { "Temperature_BMP180", "Pressure_BMP180" });

        // Add battery parameters if enabled
        if (enableBattery)
            dataParameters.AddRange(new[] { "BatteryVoltage", "BatteryPercent" });

        // Add external ADC channels if enabled
        if (enableExtA6)
            dataParameters.Add("ExtADC_A6");
        if (enableExtA7)
            dataParameters.Add("ExtADC_A7");
        if (enableExtA15)
            dataParameters.Add("ExtADC_A15");

        // Create empty collections for all selected parameters (if not already present)
        foreach (var parameter in dataParameters)
        {
            if (!dataPointsCollections.ContainsKey(parameter))
            {
                dataPointsCollections[parameter] = new List<float>();
                timeStampsCollections[parameter] = new List<int>();
            }
        }
    }


    /// <summary>
    /// Handles logic when the AutoYAxis property changes.
    /// Switches between auto-scaling and manual Y-axis mode, backing up or restoring manual values as needed,
    /// recalculates the axis range if automatic mode is activated, and updates all relevant UI fields.
    /// </summary>
    /// <param name="value">True if auto-scaling is enabled, false if manual Y range is active.</param>
    partial void OnAutoYAxisChanged(bool value)
    {

        // Enable/disable manual entry fields based on current mode
        IsYAxisManualEnabled = !value;

        if (value)
        {

            // Switching to auto mode: backup current manual values for later restore
            _lastValidYAxisMin = YAxisMin;
            _lastValidYAxisMax = YAxisMax;

            // Calculate the best-fit Y axis range based on current data
            CalculateAutoYAxisRange();

            // Apply the new auto-calculated limits
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }
        else
        {

            // Switching to manual mode: restore last valid manual limits
            YAxisMin = _lastValidYAxisMin;
            YAxisMax = _lastValidYAxisMax;
        }

        // Sync the input field texts to match new limits
        UpdateTextProperties();

        // Clear any previous validation messages
        ValidationMessage = "";

        // Redraw the chart with the updated range
        UpdateChart();
    }


    /// <summary>
    /// Automatically calculates the best Y-axis range for the current chart,
    /// based on the min and max of all available data points for the selected parameter(s).
    /// Adds a margin to the range for visual clarity.
    /// Falls back to default values if no data is available.
    /// </summary>
    private void CalculateAutoYAxisRange()
    {

        // Get the parameter name
        string cleanParam = CleanParameterName(SelectedParameter);

        // If the selected parameter is a group (e.g., X/Y/Z): compute range from all sub-parameters
        if (IsMultiChart(cleanParam))
        {
            var subParams = GetSubParameters(cleanParam);
            var allValues = new List<float>();

            // Gather all values from all relevant sub-parameters
            foreach (var param in subParams)
            {
                if (dataPointsCollections.ContainsKey(param) && dataPointsCollections[param].Count > 0)
                    allValues.AddRange(dataPointsCollections[param]);
            }

            // No data found: use fallback defaults
            if (allValues.Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(cleanParam);
                _autoYAxisMax = GetDefaultYAxisMax(cleanParam);
                return;
            }

            var min = allValues.Min();
            var max = allValues.Max();
            var range = max - min;

            if (Math.Abs(range) < 0.001)
            {

                // All values are (almost) the same: set a small margin around the center
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {

                // Normal case: add 10% margin above/below the min/max
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }

        // Single parameter selected
        else
        {

            // If no data for this parameter: use fallback defaults
            if (!dataPointsCollections.ContainsKey(cleanParam) || dataPointsCollections[cleanParam].Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(cleanParam);
                _autoYAxisMax = GetDefaultYAxisMax(cleanParam);
                return;
            }

            var data = dataPointsCollections[cleanParam];
            var min = data.Min();
            var max = data.Max();
            var range = max - min;

            if (Math.Abs(range) < 0.001)
            {

                // All values are (almost) the same: center and add margin
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {

                // Normal case: add 10% margin
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }

        // Round the limits
        _autoYAxisMin = Math.Round(_autoYAxisMin, 3);
        _autoYAxisMax = Math.Round(_autoYAxisMax, 3);
    }



    /// <summary>
    /// Updates the backing string properties for all numeric input fields,
    /// so that the UI entries reflect the current numeric property values.
    /// Also raises property changed notifications for the UI to update.
    /// </summary>
    private void UpdateTextProperties()
    {

        // Convert numeric properties to string for display in text entry fields
        _yAxisMinText = YAxisMin.ToString(CultureInfo.InvariantCulture);
        _yAxisMaxText = YAxisMax.ToString(CultureInfo.InvariantCulture);
        _timeWindowSecondsText = TimeWindowSeconds.ToString();
        _xAxisLabelIntervalText = XAxisLabelInterval.ToString();

        // Notify the UI that the values have changed so the bound controls refresh
        OnPropertyChanged(nameof(YAxisMinText));
        OnPropertyChanged(nameof(YAxisMaxText));
        OnPropertyChanged(nameof(TimeWindowSecondsText));
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }


    /// <summary>
    /// Validates and updates the minimum Y-axis value based on user input.
    /// Enforces numeric range and logical consistency with the Y max value.
    /// Shows validation messages for invalid inputs.
    /// </summary>
    private void ValidateAndUpdateYAxisMin(string value)
    {

        // Ignore manual input if Auto Y-Axis is enabled
        if (AutoYAxis)
            return;

        // If empty, reset to default value for current parameter
        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMin = GetDefaultYAxisMin(SelectedParameter);
            ValidationMessage = "";
            YAxisMin = defaultMin;
            _lastValidYAxisMin = defaultMin;
            UpdateChart();
            return;
        }

        // Allow partial input (e.g. just "-" or "+") during typing
        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        // Try to parse the string as a double value
        if (TryParseDouble(value, out double result))
        {

            // Ensure value is within the allowed Y axis range
            if (result < MIN_Y_AXIS || result > MAX_Y_AXIS)
            {
                ValidationMessage = $"Y Min out of range ({MIN_Y_AXIS} to {MAX_Y_AXIS}).";
                ResetYAxisMinText();
                return;
            }

            // Y Min must be less than Y Max
            if (result >= YAxisMax)
            {
                ValidationMessage = "Y Min cannot be greater than or equal to Y Max.";
                ResetYAxisMinText();
                return;
            }

            // Valid input: update value, clear error, refresh chart
            ValidationMessage = "";
            YAxisMin = result;
            _lastValidYAxisMin = result;
            UpdateChart();
        }
        else
        {

            // Invalid input: show error, revert text field
            ValidationMessage = "Y Min must be a valid number (no letters or special characters allowed).";
            ResetYAxisMinText();
        }
    }


    /// <summary>
    /// Validates and updates the maximum Y-axis value based on user input.
    /// Enforces numeric range and logical consistency with the Y min value.
    /// Shows validation messages for invalid inputs.
    /// </summary>
    private void ValidateAndUpdateYAxisMax(string value)
    {

        // Ignore manual input if Auto Y-Axis is enabled
        if (AutoYAxis)
            return;

        // If empty, reset to default value for current parameter
        if (string.IsNullOrWhiteSpace(value))
        {
            var defaultMax = GetDefaultYAxisMax(SelectedParameter);
            ValidationMessage = "";
            YAxisMax = defaultMax;
            _lastValidYAxisMax = defaultMax;
            UpdateChart();
            return;
        }

        // Allow partial input (e.g. just "-" or "+") during typing
        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        // Try to parse the string as a double value
        if (TryParseDouble(value, out double result))
        {

            // Ensure value is within the allowed Y axis range
            if (result < MIN_Y_AXIS || result > MAX_Y_AXIS)
            {
                ValidationMessage = $"Y Max out of range ({MIN_Y_AXIS} to {MAX_Y_AXIS}).";
                ResetYAxisMaxText();
                return;
            }

            // Y Max must be greater than Y Min
            if (result <= YAxisMin)
            {
                ValidationMessage = "Y Max cannot be less than or equal to Y Min.";
                ResetYAxisMaxText();
                return;
            }

            // Valid input: update value, clear error, refresh chart
            ValidationMessage = "";
            YAxisMax = result;
            _lastValidYAxisMax = result;
            UpdateChart();
        }
        else
        {

            // Invalid input: show error, revert text field
            ValidationMessage = "Y Max must be a valid number (no letters or special characters allowed).";
            ResetYAxisMaxText();
        }
    }


    /// <summary>
    /// Validates and updates the sampling rate for the device based on user input.
    /// Ensures the new value is within defined min/max limits and restarts sampling if valid.
    /// Shows validation messages for invalid inputs.
    /// </summary>
    private void ValidateAndUpdateSamplingRate(string value)
    {

        // If empty, use the default sampling rate
        if (string.IsNullOrWhiteSpace(value))
        {
            const double defaultRate = 51.2;
            UpdateSamplingRateAndRestart(defaultRate);
            ValidationMessage = "";
            return;
        }

        // Allow partial input (e.g. just "-" or "+") during typing
        if (value.Trim() == "-" || value.Trim() == "+")
        {
            ValidationMessage = "";
            return;
        }

        // Try to parse the string as a double value
        if (TryParseDouble(value, out double result))
        {

            // Ensure the sampling rate is within allowed limits
            if (result > MAX_SAMPLING_RATE)
            {
                ValidationMessage = $"Sampling rate too high. Maximum {MAX_SAMPLING_RATE} Hz.";
                ResetSamplingRateText();
                return;
            }
            if (result < MIN_SAMPLING_RATE)
            {
                ValidationMessage = $"Sampling rate too low. Minimum {MIN_SAMPLING_RATE} Hz.";
                ResetSamplingRateText();
                return;
            }

            // Valid input: update and restart sampling, clear error
            UpdateSamplingRateAndRestart(result);
            ValidationMessage = "";
        }
        else
        {

            // Invalid input: show error, revert text field
            ValidationMessage = "Sampling rate must be a valid number (no letters or special characters allowed).";
            ResetSamplingRateText();
        }
    }


    /// <summary>
    /// Validates and updates the time window (in seconds) for the displayed data,
    /// based on user input. Ensures the value is within allowed range and resets
    /// all data collections and counters as needed. Shows validation messages for
    /// invalid input.
    /// </summary>
    private void ValidateAndUpdateTimeWindow(string value)
    {

        // If input is empty, reset to default value and clear data
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultTimeWindow = 20;
            ValidationMessage = "";
            TimeWindowSeconds = defaultTimeWindow;
            _lastValidTimeWindowSeconds = defaultTimeWindow;

            // Clear data collections and reset counters/timestamps
            ClearAllDataCollections();
            ResetAllTimestamps();
            ResetAllCounters();

            UpdateChart();
            return;
        }

        // Try to parse user input as an integer
        if (TryParseInt(value, out int result))
        {

            // Check max allowed time window
            if (result > MAX_TIME_WINDOW_SECONDS)
            {
                ValidationMessage = $"Time Window too large. Maximum {MAX_TIME_WINDOW_SECONDS} s.";
                ResetTimeWindowText();
                return;
            }

            // Check min allowed time window
            if (result < MIN_TIME_WINDOW_SECONDS)
            {
                ValidationMessage = $"Time Window too small. Minimum {MIN_TIME_WINDOW_SECONDS} s.";
                ResetTimeWindowText();
                return;
            }

            // Valid input: update, clear data, and refresh chart
            ValidationMessage = "";
            TimeWindowSeconds = result;
            _lastValidTimeWindowSeconds = result;

            ClearAllDataCollections();
            ResetAllTimestamps();
            ResetAllCounters();

            UpdateChart();
        }
        else
        {

            // Invalid input: show error and revert text field
            ValidationMessage = "Time Window must be a valid positive number.";
            ResetTimeWindowText();
        }
    }


    /// <summary>
    /// Validates and updates the interval for X-axis labels (in seconds),
    /// based on user input. Ensures the value is within allowed range and
    /// triggers a chart refresh. Shows validation messages for invalid input.
    /// </summary>
    private void ValidateAndUpdateXAxisInterval(string value)
    {

        // If input is empty, reset to default value
        if (string.IsNullOrWhiteSpace(value))
        {
            const int defaultInterval = 5;
            ValidationMessage = "";
            XAxisLabelInterval = defaultInterval;
            _lastValidXAxisLabelInterval = defaultInterval;
            UpdateChart();
            return;
        }

        // Try to parse user input as an integer
        if (TryParseInt(value, out int result))
        {

            // Check max allowed X label interval
            if (result > MAX_X_AXIS_LABEL_INTERVAL)
            {
                ValidationMessage = $"X Labels interval too high. Maximum {MAX_X_AXIS_LABEL_INTERVAL}.";
                ResetXAxisIntervalText();
                return;
            }

            // Check min allowed X label interval
            if (result < MIN_X_AXIS_LABEL_INTERVAL)
            {
                ValidationMessage = $"X Labels interval too low. Minimum {MIN_X_AXIS_LABEL_INTERVAL}.";
                ResetXAxisIntervalText();
                return;
            }

            // Valid input: update value and refresh chart
            ValidationMessage = "";
            XAxisLabelInterval = result;
            _lastValidXAxisLabelInterval = result;
            UpdateChart();
        }
        else
        {

            // Invalid input: show error and revert text field
            ValidationMessage = "X Labels interval must be a valid positive number (no letters or special characters allowed).";
            ResetXAxisIntervalText();
        }
    }


    /// <summary>
    /// Updates the device sampling rate and restarts the data acquisition logic.
    /// Stops the current timer, clears all existing data, resets counters,
    /// restarts the timer with the new sampling interval, and refreshes the chart.
    /// </summary>
    /// <param name="newRate">The new sampling rate to set (in Hz).</param>
    private void UpdateSamplingRateAndRestart(double newRate)
    {

        // Stop the current timer (avoid double timers running)
        StopTimer();

        // Set the new sampling rate for the Shimmer device
        shimmer.SamplingRate = newRate;
        SamplingRateDisplay = newRate;
        _lastValidSamplingRate = newRate;

        // Clear all data collections (old data is now invalid due to new rate)
        ClearAllDataCollections();

        // Reset the sample counter to zero (new acquisition window)
        ResetAllCounters();

        // Start the timer with the new sampling interval
        StartTimer();

        // Notify UI/chart to redraw (shows "Waiting for data..." until new samples arrive)
        UpdateChart();
    }


    /// <summary>
    /// Resets all internal counters used for tracking data samples (e.g., sample index).
    /// </summary>
    private void ResetAllCounters()
    {
        sampleCounter = 0;
    }



    /// <summary>
    /// Resets the sampling rate input field to the last valid value,
    /// and triggers property changed notification to update the UI.
    /// </summary>
    private void ResetSamplingRateText()
    {
        _samplingRateText = _lastValidSamplingRate.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SamplingRateText));
    }


    /// <summary>
    /// Returns the default minimum Y-axis value for a given sensor parameter.
    /// </summary>
    /// <param name="parameter">The name of the sensor parameter (e.g., "GyroscopeX").</param>
    /// <returns>Default minimum value for the parameter's Y axis.</returns>
    private double GetDefaultYAxisMin(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" => -5,
            "Low-Noise AccelerometerY" => -5,
            "Low-Noise AccelerometerZ" => -15,
            "Wide-Range AccelerometerX" => -5,
            "Wide-Range AccelerometerY" => -5,
            "Wide-Range AccelerometerZ" => -15,
            "GyroscopeX" => -250,
            "GyroscopeY" => -250,
            "GyroscopeZ" => -250,
            "MagnetometerX" => -5,
            "MagnetometerY" => -5,
            "MagnetometerZ" => -5,
            "Temperature_BMP180" => 15,
            "Pressure_BMP180" => 90,
            "BatteryVoltage" => 3.3,
            "BatteryPercent" => 0,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 0,
            _ => 0  // Default fallback for unknown parameter
        };
    }


    /// <summary>
    /// Returns the default maximum Y-axis value for a given sensor parameter.
    /// </summary>
    /// <param name="parameter">The name of the sensor parameter (e.g., "GyroscopeX").</param>
    /// <returns>Default maximum value for the parameter's Y axis.</returns>
    private double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            "Low-Noise AccelerometerX" => 5,
            "Low-Noise AccelerometerY" => 5,
            "Low-Noise AccelerometerZ" => 15,
            "Wide-Range AccelerometerX" => 5,
            "Wide-Range AccelerometerY" => 5,
            "Wide-Range AccelerometerZ" => 15,
            "GyroscopeX" => 250,
            "GyroscopeY" => 250,
            "GyroscopeZ" => 250,
            "MagnetometerX" => 5,
            "MagnetometerY" => 5,
            "MagnetometerZ" => 5,
            "Temperature_BMP180" => 40,
            "Pressure_BMP180" => 110,
            "BatteryVoltage" => 4.2,
            "BatteryPercent" => 100,
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 3.3,
            _ => 1   // Default fallback for unknown parameter
        };
    }


    /// <summary>
    /// Restores the Y-axis minimum input field text to the last valid value.
    /// </summary>
    private void ResetYAxisMinText()
    {
        _yAxisMinText = _lastValidYAxisMin.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
    }


    /// <summary>
    /// Restores the Y-axis maximum input field text to the last valid value.
    /// </summary>
    private void ResetYAxisMaxText()
    {
        _yAxisMaxText = _lastValidYAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMaxText));
    }


    /// <summary>
    /// Restores the time window input field text to the last valid value.
    /// </summary>
    private void ResetTimeWindowText()
    {
        _timeWindowSecondsText = _lastValidTimeWindowSeconds.ToString();
        OnPropertyChanged(nameof(TimeWindowSecondsText));
    }


    /// <summary>
    /// Restores the X-axis label interval input field text to the last valid value.
    /// </summary>
    private void ResetXAxisIntervalText()
    {
        _xAxisLabelIntervalText = _lastValidXAxisLabelInterval.ToString();
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }


    /// <summary>
    /// Populates the <see cref="AvailableParameters"/> collection based on which sensors are currently enabled.
    /// Includes both group headers (e.g., "Gyroscope") and their sub-parameters (e.g., "GyroscopeX", "GyroscopeY", "GyroscopeZ").
    /// Ensures the SelectedParameter is valid after updating the list.
    /// </summary>
    private void InitializeAvailableParameters()
    {
        AvailableParameters.Clear();

        // Add Low-Noise Accelerometer group and its X/Y/Z axes if enabled
        if (enableLowNoiseAccelerometer)
        {
            AvailableParameters.Add("Low-Noise Accelerometer");
            AvailableParameters.Add("    → Low-Noise AccelerometerX");
            AvailableParameters.Add("    → Low-Noise AccelerometerY");
            AvailableParameters.Add("    → Low-Noise AccelerometerZ");
        }

        // Add Wide-Range Accelerometer group and its X/Y/Z axes if enabled
        if (enableWideRangeAccelerometer)
        {
            AvailableParameters.Add("Wide-Range Accelerometer");
            AvailableParameters.Add("    → Wide-Range AccelerometerX");
            AvailableParameters.Add("    → Wide-Range AccelerometerY");
            AvailableParameters.Add("    → Wide-Range AccelerometerZ");
        }

        // Add Gyroscope group and its X/Y/Z axes if enabled
        if (enableGyroscope)
        {
            AvailableParameters.Add("Gyroscope");
            AvailableParameters.Add("    → GyroscopeX");
            AvailableParameters.Add("    → GyroscopeY");
            AvailableParameters.Add("    → GyroscopeZ");
        }

        // Add Magnetometer group and its X/Y/Z axes if enabled
        if (enableMagnetometer)
        {
            AvailableParameters.Add("Magnetometer");
            AvailableParameters.Add("    → MagnetometerX");
            AvailableParameters.Add("    → MagnetometerY");
            AvailableParameters.Add("    → MagnetometerZ");
        }

        // Add BatteryVoltage and BatteryPercent if battery monitoring is enabled (no group header)
        if (enableBattery)
        {
            AvailableParameters.Add("BatteryVoltage");
            AvailableParameters.Add("BatteryPercent");
        }

        // Add Pressure and Temperature if enabled
        if (enablePressureTemperature)
        {
            AvailableParameters.Add("Temperature_BMP180");
            AvailableParameters.Add("Pressure_BMP180");
        }

        // Add external ADC parameters if enabled
        if (enableExtA6)
            AvailableParameters.Add("ExtADC_A6");
        if (enableExtA7)
            AvailableParameters.Add("ExtADC_A7");
        if (enableExtA15)
            AvailableParameters.Add("ExtADC_A15");

        // If the current selection is no longer available, select the first parameter
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }
    }


    /// <summary>
    /// Removes formatting/indentation (such as arrow and spaces) from a parameter display name,
    /// returning only the raw parameter name.
    /// Used to map UI selections to internal parameter keys.
    /// </summary>
    /// <param name="displayName">The display name as shown in the UI (may include "    → ").</param>
    /// <returns>The clean, unformatted parameter name.</returns>
    public string CleanParameterName(string displayName)
    {
        if (displayName.StartsWith("    → "))
        {
            return displayName.Substring(6);    // Remove "    → " prefix
        }
        return displayName;
    }


    /// <summary>
    /// Determines if the specified parameter is a multi-parameter group,
    /// meaning it represents a sensor with multiple sub-components (e.g., X, Y, Z).
    /// </summary>
    /// <param name="parameter">The parameter or group name to check.</param>
    /// <returns>True if the parameter is a group (MultiChart); otherwise, false.</returns>
    private bool IsMultiChart(string parameter)
    {
        // Clean the display name to get the actual parameter name (remove formatting)
        string cleanName = CleanParameterName(parameter);

        // Return true for sensor groups that support multi-line charting
        return cleanName is "Low-Noise Accelerometer" or "Wide-Range Accelerometer" or
                          "Gyroscope" or "Magnetometer";
    }



    /// <summary>
    /// Returns the list of sub-parameters (typically X, Y, Z) for a given sensor group.
    /// If the group is not recognized, returns an empty list.
    /// </summary>
    /// <param name="groupParameter">The display name or group name selected by the user.</param>
    /// <returns>List of parameter names for the group (e.g., X/Y/Z axes).</returns>
    public List<string> GetSubParameters(string groupParameter)
    {

        // Clean the display name to get the actual group name
        string cleanName = CleanParameterName(groupParameter);

        // Map each known group to its corresponding sub-parameter names
        return cleanName switch
        {
            "Low-Noise Accelerometer" => new List<string> { "Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ" },
            "Wide-Range Accelerometer" => new List<string> { "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" },
            "Gyroscope" => new List<string> { "GyroscopeX", "GyroscopeY", "GyroscopeZ" },
            "Magnetometer" => new List<string> { "MagnetometerX", "MagnetometerY", "MagnetometerZ" },
            _ => new List<string>()  // Return empty if group not recognized
        };
    }


    /// <summary>
    /// Clears all data and timestamp collections for every parameter.
    /// </summary>
    private void ClearAllDataCollections()
    {

        // Iterate over all parameter keys and clear the corresponding data lists
        foreach (var key in dataPointsCollections.Keys.ToList())
        {
            dataPointsCollections[key].Clear();
        }

        // Do the same for the timestamp lists
        foreach (var key in timeStampsCollections.Keys.ToList())
        {
            timeStampsCollections[key].Clear();
        }
    }


    /// <summary>
    /// Ensures that the data and timestamp lists for the given parameter
    /// do not exceed the specified maximum number of points.
    /// If the collections grow too large (e.g., after a time window change),
    /// oldest samples are removed.
    /// </summary>
    /// <param name="parameter">The parameter whose data/timestamps should be trimmed.</param>
    /// <param name="maxPoints">The maximum number of points to retain in each collection.</param>
    private void TrimCollection(string parameter, int maxPoints)
    {

        // Check if both data and timestamp lists exist for the parameter
        if (dataPointsCollections.TryGetValue(parameter, out var dataList) &&
            timeStampsCollections.TryGetValue(parameter, out var timeList))
        {

            // Remove oldest items until the collection is within the maximum size
            while (dataList.Count > maxPoints && timeList.Count > 0)
            {
                if (dataList.Count > 0)
                    dataList.RemoveAt(0);   // Remove oldest data point
                if (timeList.Count > 0)
                    timeList.RemoveAt(0);   // Remove corresponding timestamp
            }
        }
    }

/// <summary>
/// ///////////////////////////////////////////////////////////////////////////////////////
/// </summary>
/// <returns></returns> 

    public List<string> GetCurrentSubParameters()
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(SelectedParameter);
        return IsMultiChart(cleanName) ? GetSubParameters(cleanName) : new List<string> { cleanName };
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




    // Metodo chiamato ogni volta che scatta il timer: aggiorna i dati e il grafico.
    // 1. First, modify the OnTimerElapsed method to handle battery data safely
    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            var sample = shimmer.LatestData;
            if (sample == null) return;

            sampleCounter++;

            double currentTimeSeconds = sampleCounter / shimmer.SamplingRate;

            UpdateDataCollectionsWithSingleSample(sample, currentTimeSeconds);

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
            if (enableLowNoiseAccelerometer)
            {
                values["Low-Noise AccelerometerX"] = (float)sample.LowNoiseAccelerometerX.Data;
                values["Low-Noise AccelerometerY"] = (float)sample.LowNoiseAccelerometerY.Data;
                values["Low-Noise AccelerometerZ"] = (float)sample.LowNoiseAccelerometerZ.Data;
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
                values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data / 1000f; // mV → V
                float batteryV = values["BatteryVoltage"];
                float percent;
                if (batteryV <= 3.3f)
                    percent = 0;
                else if (batteryV >= 4.2f)
                    percent = 100;
                else if (batteryV <= 4.10f)
                    percent = (batteryV - 3.3f) / (4.10f - 3.3f) * 97f;
                else
                    percent = 97f + (batteryV - 4.10f) / (4.20f - 4.10f) * 3f;
                values["BatteryPercent"] = Math.Clamp(percent, 0, 100);
            }


            if (enableExtA6)
                values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data / 1000f;
            if (enableExtA7)
                values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data / 1000f;
            if (enableExtA15)
                values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data / 1000f;


        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
            return;
        }

        lock (_dataLock)
        {

            // Convert current time to milliseconds for timestamp
            int timestampMs = (int)Math.Round(currentTimeSeconds * 1000);

            // Calculate max points based on current sampling rate
            var maxPoints = (int)(TimeWindowSeconds * shimmer.SamplingRate);

            // Update each collection with the new sample
            var parametersSnapshot = AvailableParameters.ToList();
            foreach (var parameter in parametersSnapshot)
            {
                string cleanName = CleanParameterName(parameter);
                if (values.ContainsKey(cleanName))
                {
                    dataPointsCollections[cleanName].Add(values[cleanName]);
                    timeStampsCollections[cleanName].Add(timestampMs);

                    // Mantieni solo TimeWindowSeconds * samplingRate punti
                    TrimCollection(cleanName, maxPoints);
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

    public (List<float> data, List<int> time) GetSeriesSnapshot(string parameter)
    {
        lock (_dataLock)
        {
            string cleanName = CleanParameterName(parameter);
            return (
                dataPointsCollections.ContainsKey(cleanName) ? new List<float>(dataPointsCollections[cleanName]) : new List<float>(),
                timeStampsCollections.ContainsKey(cleanName) ? new List<int>(timeStampsCollections[cleanName]) : new List<int>()
            );
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
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(value);

        // Determina la modalità di visualizzazione
        ChartDisplayMode = IsMultiChart(cleanName) ? ChartDisplayMode.Multi : ChartDisplayMode.Single;

        UpdateYAxisSettings(value); // Passa il valore originale che viene pulito dentro il metodo

        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;

        UpdateTextProperties();
        IsXAxisLabelIntervalEnabled = cleanName != "HeartRate";
        ValidationMessage = "";

        UpdateChart();
    }



    // Imposta etichette e limiti asse Y in base al parametro selezionato.
    private void UpdateYAxisSettings(string parameter)
    {
        // Pulisce il nome del parametro prima di verificare
        string cleanName = CleanParameterName(parameter);

        switch (cleanName)
        {
            case "Low-Noise Accelerometer":
                YAxisLabel = "Low-Noise Accelerometer";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Low-Noise Accelerometer (X,Y,Z)";
                YAxisMin = -20;
                YAxisMax = 20;
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
                YAxisMin = -5;
                YAxisMax = 5;
                break;
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
                YAxisMin = -15;
                YAxisMax = 15;
                break;
            case "Wide-Range AccelerometerX":
                YAxisLabel = "Wide-Range Accelerometer X";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer X";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Wide-Range AccelerometerY":
                YAxisLabel = "Wide-Range Accelerometer Y";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Wide-Range AccelerometerZ":
                YAxisLabel = "Wide-Range Accelerometer Z";
                YAxisUnit = "m/s²";
                ChartTitle = "Real-time Wide-Range Accelerometer Z";
                YAxisMin = -15;
                YAxisMax = 15;
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
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "MagnetometerY":
                YAxisLabel = "Magnetometer Y";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Y";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "MagnetometerZ":
                YAxisLabel = "Magnetometer Z";
                YAxisUnit = "local_flux*";
                ChartTitle = "Real-time Magnetometer Z";
                YAxisMin = -5;
                YAxisMax = 5;
                break;
            case "Temperature_BMP180":
                YAxisLabel = "Temperature";
                YAxisUnit = "°C";
                ChartTitle = "BMP180 Temperature";
                YAxisMin = 15;
                YAxisMax = 40;
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
                YAxisUnit = "V";
                ChartTitle = "Real-time Battery Voltage";
                YAxisMin = 3.3;
                YAxisMax = 4.2;
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
                YAxisUnit = "V";
                ChartTitle = "External ADC A6";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
            case "ExtADC_A7":
                YAxisLabel = "External ADC A7";
                YAxisUnit = "V";
                ChartTitle = "External ADC A7";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
            case "ExtADC_A15":
                YAxisLabel = "External ADC A15";
                YAxisUnit = "V";
                ChartTitle = "External ADC A15";
                YAxisMin = 0;
                YAxisMax = 3.3;
                break;
        }
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

    

 


    // Metodo per ottenere la configurazione corrente
    // Restituisce un oggetto che rappresenta la configurazione attuale dei sensori.
    public ShimmerDevice GetCurrentSensorConfiguration()
    {
        return new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = enableLowNoiseAccelerometer,
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

    public void ResetAllTimestamps()
    {
        lock (_dataLock)
        {
            foreach (var param in timeStampsCollections.Keys.ToList())
            {
                int count = timeStampsCollections[param].Count;
                for (int i = 0; i < count; i++)
                {
                    // Timestamp in millisecondi, come in acquisizione regolare
                    timeStampsCollections[param][i] = (int)(i * (1000.0 / shimmer.SamplingRate));
                }
            }
        }
    }



}
