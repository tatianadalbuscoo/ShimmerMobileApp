/*
 * DataPageViewModel — MAUI MVVM
 * Drives real-time charting for Shimmer IMU/EXG on Windows/Android/iOS/macOS.
 * Exposes observable UI state (axes, units, titles, grid, legend) and commands (apply Y min/max, sampling rate).
 * Subscribes to device samples, buffers and trims by time window, updates chart via events, and rebuilds timestamps.
 * Supports Multi/Split modes (IMU: X/Y/Z; EXG: EXG1/EXG2), legend labels/colors, and auto/manual Y-axis with margins.
 * Validates user input (culture-aware doubles/ints), applies nearest firmware sampling rate, and restarts streaming.
 * iOS/macCatalyst integrates EXG bridge (mode title + change notifications); thread-safe updates via _dataLock.
 * Cleans up subscriptions and buffers via IDisposable; provides sensor configuration snapshot helpers.
 */


using CommunityToolkit.Mvvm.ComponentModel;
using ShimmerSDK.IMU;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;
using CommunityToolkit.Mvvm.Input;
using ShimmerSDK.EXG;


#if IOS || MACCATALYST
using Microsoft.Maui.ApplicationModel;
#endif


namespace ShimmerInterface.ViewModels;


/// <summary>
/// Chart display modes:
/// <list type="bullet">
/// <item><term>Multi</term><description>Single canvas with overlaid series (IMU: X/Y/Z; EXG: EXG1/EXG2 or single series).</description></item>
/// <item><term>Split</term><description>Separate panels (IMU: three panels X/Y/Z; EXG: two panels EXG1/EXG2, third hidden).</description></item>
/// </list>
/// </summary>
public enum ChartDisplayMode { Multi, Split }


/// <summary>
/// ViewModel for the DataPage.  
/// Drives real-time charting for Shimmer IMU/EXG sensors.
/// Manages device connections and streaming, buffers time-series data,
/// handles axis/legend/parameter selection, applies sampling-rate changes,
/// and updates the UI on new samples. Implements <see cref="IDisposable"/>
/// to clean up device event subscriptions and data buffers.
/// </summary>
public partial class DataPageViewModel : ObservableObject, IDisposable
{

    // ----- Application-wide numeric limits for validation -----
    private const double MAX_DOUBLE = 1e6;
    private const double MIN_DOUBLE = -1e6;
    private const double MAX_Y_AXIS = 100_000;
    private const double MIN_Y_AXIS = -100_000;
    private const int MAX_TIME_WINDOW_SECONDS = 600;    // 10 minutes
    private const int MIN_TIME_WINDOW_SECONDS = 1;
    private const int MAX_X_AXIS_LABEL_INTERVAL = 1000;
    private const int MIN_X_AXIS_LABEL_INTERVAL = 1;
    private const double MAX_SAMPLING_RATE = 100;
    private const double MIN_SAMPLING_RATE = 1;

    // ----- Device references (IMU/EXG) -----
    private readonly ShimmerSDK_IMU? shimmerImu;
    private readonly ShimmerSDK_EXG? shimmerExg;

    // ----- Enabled-sensor flags for this session -----
    private bool enableLowNoiseAccelerometer;
    private bool enableWideRangeAccelerometer;
    private bool enableGyroscope;
    private bool enableMagnetometer;
    private bool enablePressureTemperature;
    private bool enableBattery;
    private bool enableExtA6;
    private bool enableExtA7;
    private bool enableExtA15;

    // ----- EXG session flags -----
    private bool enableExg;
    private bool exgModeECG;
    private bool exgModeEMG;
    private bool exgModeTest;
    private bool exgModeRespiration;

    // ----- Tracks whether Dispose() has already run to prevent double cleanup -----
    private bool _disposed = false;

    // ----- Data storage for real-time series -----
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();

    // ----- Synchronizes access to series while updating from the sample thread -----
    private readonly object _dataLock = new();

    // ----- Total number of received samples since (re)start -----
    private int sampleCounter = 0;


    // ----- Last valid values for restoring user input -----
    private double _lastValidYAxisMin = 0;
    private double _lastValidYAxisMax = 1;
    private int _lastValidTimeWindowSeconds = 20;
    private int _lastValidXAxisLabelInterval = 5;
    private double _lastValidSamplingRate = 51.2;

    // ----- Backing fields for user input (text entry fields) -----
    private string _yAxisMinText = "0";
    private string _yAxisMaxText = "1";
    private string _timeWindowSecondsText = "20";
    private string _xAxisLabelIntervalText = "5";
    private string _samplingRateText = "51.2";

    // ----- Temporary values for auto-range Y axis calculation -----
    private double _autoYAxisMin = 0;
    private double _autoYAxisMax = 1;

    // ----- Parameter name arrays for each sensor group -----
    private static readonly string[] LowNoiseAccelerometerAxes = ["Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ"];
    private static readonly string[] WideRangeAccelerometerAxes = [ "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" ];
    private static readonly string[] GyroscopeAxes = [ "GyroscopeX", "GyroscopeY", "GyroscopeZ" ];
    private static readonly string[] MagnetometerAxes = [ "MagnetometerX", "MagnetometerY", "MagnetometerZ" ];
    private static readonly string[] EnvSensors = [ "Temperature_BMP180", "Pressure_BMP180" ];
    private static readonly string[] BatteryParams = ["BatteryVoltage", "BatteryPercent"];

    // ----- X-axis baseline offset (s) used to start charts at 0 after (re)open -----
    private double timeBaselineSeconds = 0;

    // ----- Public properties and events -----

    // UI hooks (view subscribes)
    public event EventHandler<string>? ShowBusyRequested;
    public event EventHandler? HideBusyRequested;
    public event EventHandler<string>? ShowAlertRequested;
    public event EventHandler? ChartUpdateRequested;

    // Parameters available for charting based on enabled sensors.
    public ObservableCollection<string> AvailableParameters { get; } = new();


    // ----- MVVM Bindable Properties -----
    // These properties are observable and used for data binding in the UI
    [ObservableProperty]
    private string selectedParameter = "Low-Noise AccelerometerX";          // current series/group selection

    [ObservableProperty]
    private double yAxisMin = 0;                                            // Y-axis lower bound

    [ObservableProperty]
    private double yAxisMax = 1;                                            // Y-axis upper bound

    [ObservableProperty]
    private int timeWindowSeconds = 20;                                     // visible time window (s)

    [ObservableProperty]
    private string yAxisLabel = "Value";                                    // label for Y-axis

    [ObservableProperty]
    private string yAxisUnit = "Unit";                                      // unit for Y-axis

    [ObservableProperty]
    private string chartTitle = "Real-time Data";                           // chart header

    [ObservableProperty]
    private int xAxisLabelInterval = 5;                                     // tick labels spacing on X (s)

    [ObservableProperty]
    private string validationMessage = "";                                  // UI validation/errors

    [ObservableProperty]
    private bool isXAxisLabelIntervalEnabled = true;                        // enable X-interval entry

    [ObservableProperty]
    private double samplingRateDisplay;                                     // applied device rate (Hz)

    [ObservableProperty]
    private bool showGrid = true;                                           // toggle gridlines

    [ObservableProperty]
    private bool autoYAxis = false;                                         // auto-scale Y on/off

    [ObservableProperty]
    private bool isYAxisManualEnabled = true;                               // manual Y entries enabled

    [ObservableProperty]
    private ChartDisplayMode chartDisplayMode = ChartDisplayMode.Multi;     // Multi/Split

    [ObservableProperty] 
    private bool isApplyingSamplingRate;                                    // busy flag while writing SR


    // ----- Text-entry (bindable string) properties -----

    /// <summary>
    /// User-entered sampling rate as text (for Entry binding).
    /// Only updates the text; validation and applying the value happen elsewhere (e.g., on Apply/Enter).
    /// </summary>
    public string SamplingRateText
    {
        get => _samplingRateText;
        set
        {
            SetProperty(ref _samplingRateText, value);
        }
    }


    /// <summary>
    /// User-entered Y-axis minimum (text binding).
    /// Updates only the backing text; validation/apply are handled elsewhere.
    /// </summary>
    public string YAxisMinText
    {
        get => _yAxisMinText;
        set
        {
            SetProperty(ref _yAxisMinText, value);
        }
    }


    /// <summary>
    /// User-entered Y-axis maximum (text binding).
    /// Updates only the backing text; validation/apply are handled elsewhere.
    /// </summary>
    public string YAxisMaxText
    {
        get => _yAxisMaxText;
        set
        {
            SetProperty(ref _yAxisMaxText, value);
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


    // ----- Computed / Derived properties -----

    /// <summary>
    /// Indicates whether the view should use the EXG split layout:
    /// true when the chart is in <c>Split</c> mode and the selected parameter
    /// belongs to the EXG family (ECG/EMG/EXG Test/Respiration).
    /// </summary>
    /// <returns>
    /// <c>true</c> to render two EXG panels (EXG1/EXG2); otherwise, <c>false</c>.
    /// </returns>
    public bool IsExgSplit
    {
        get
        {
            return ChartDisplayMode == ChartDisplayMode.Split &&
                   (CleanParameterName(SelectedParameter) is "ECG" or "EMG" or "EXG Test" or "Respiration");
        }
    }


    /// <summary>
    /// Gets the current elapsed time in seconds since data collection started.
    /// </summary>
    public double CurrentTimeInSeconds
        => Math.Max(0, (sampleCounter / DeviceSamplingRate) - timeBaselineSeconds);


    // ----- Commands -----

    /// <summary>
    /// Command that validates and applies the user-entered Y-axis minimum.
    /// </summary>
    public IRelayCommand ApplyYMinCommand { get; }


    /// <summary>
    /// Command that validates and applies the user-entered Y-axis maximum.
    /// </summary>
    public IRelayCommand ApplyYMaxCommand { get; }


    /// <summary>
    /// Command that writes the requested sampling rate to the device asynchronously,
    /// restarts streaming if needed, and refreshes the chart state.
    /// </summary>
    public IAsyncRelayCommand ApplySamplingRateCommand { get; }


    // ----- Device helpers: sampling rate, start/stop streaming, subscriptions -----

    /// <summary>
    /// Effective sampling rate in Hz: IMU, otherwise EXG, otherwise 51.2.
    /// </summary>
    private double DeviceSamplingRate => shimmerImu?.SamplingRate
                                     ?? shimmerExg?.SamplingRate
                                         ?? 51.2;


    /// <summary>
    /// Starts streaming on whichever device(s) are present (IMU/EXG).
    /// Swallows device-specific exceptions to avoid crashing the UI.
    /// Consider logging exceptions.
    /// </summary>
    private void DeviceStartStreaming()
    {
        try { 
            shimmerImu?.StartStreaming(); 
        } 
        catch {}

        try { 
            shimmerExg?.StartStreaming(); 
        } 
        catch {}
    }


    /// <summary>
    /// Stops streaming on whichever device(s) are present (IMU/EXG).
    /// Exceptions are intentionally ignored; consider logging.
    /// </summary>
    private void DeviceStopStreaming()
    {
        try 
        { 
            shimmerImu?.StopStreaming(); 
        } 
        catch {}

        try {
            shimmerExg?.StopStreaming(); 
        } 
        catch {}
    }


    /// <summary>
    /// Sets the closest supported firmware sampling rate on the connected device.
    /// Tries IMU first, then EXG; if neither is available, returns the requested rate.
    /// </summary>
    /// <param name="newRate">Requested sampling rate (Hz).</param>
    /// <returns>The actual rate applied (or the input if no device is present).</returns>
    private double SetFirmwareSamplingRateNearestUnified(double newRate)
    {
        if (shimmerImu != null) 
            return shimmerImu.SetFirmwareSamplingRateNearest(newRate);
        if (shimmerExg != null)
            return shimmerExg.SetFirmwareSamplingRateNearest(newRate);

        return newRate;
    }


    /// <summary>
    /// Subscribes to SampleReceived on available devices (IMU/EXG).
    /// </summary>
    private void SubscribeSamples()
    {
        if (shimmerImu != null) 
            shimmerImu.SampleReceived += OnSampleReceived;

        if (shimmerExg != null) 
            shimmerExg.SampleReceived += OnSampleReceived;
    }

    /// <summary>
    /// Unsubscribes from SampleReceived on available devices (IMU/EXG).
    /// </summary>
    private void UnsubscribeSamples()
    {
        if (shimmerImu != null) 
            shimmerImu.SampleReceived -= OnSampleReceived;

        if (shimmerExg != null) 
            shimmerExg.SampleReceived -= OnSampleReceived;
    }


    // ----- Constructors & initialization -----

    /// <summary>
    /// IMU-mode constructor. Wires sample events, copies enabled IMU flags from
    /// <paramref name="config"/>, initializes parameter lists and buffers,
    /// applies initial axis/rate UI state, and sets up commands.
    /// </summary>
    /// <param name="shimmerDevice">Connected IMU device instance.</param>
    /// <param name="config">Initial sensor configuration to mirror in the VM.</param>
    public DataPageViewModel(ShimmerSDK_IMU shimmerDevice, ShimmerDevice config)
    {

        // Keep a reference to the IMU device and start listening to samples
        shimmerImu = shimmerDevice;
        SubscribeSamples();

        // Mirror IMU-related sensor flags from the provided configuration
        enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
        enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;
        enablePressureTemperature = config.EnablePressureTemperature;
        enableBattery = config.EnableBattery;
        enableExtA6 = config.EnableExtA6;
        enableExtA7 = config.EnableExtA7;
        enableExtA15 = config.EnableExtA15;

        // EXG is off in a pure-IMU session
        enableExg = false;
        exgModeECG = exgModeEMG = exgModeTest = exgModeRespiration = false;

        // Seed UI with the current device sampling rate
        samplingRateDisplay = DeviceSamplingRate;

        // Build the parameter list for the combo / selector
        InitializeAvailableParameters();

        // Ensure the selected parameter is valid
        if (!AvailableParameters.Contains(SelectedParameter))
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

        // Prepare empty data buffers for all enabled parameters
        InitializeDataCollections();

        // Apply initial Y-axis/labels based on the selection
        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
        }

        // Capture “last valid” values used by validation rollback
        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _samplingRateText = DeviceSamplingRate.ToString(CultureInfo.InvariantCulture);
        _lastValidSamplingRate = DeviceSamplingRate;
        OnPropertyChanged(nameof(SamplingRateText));
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        // Commands (async for sampling rate; guarded by CanExecute where needed)
        ApplySamplingRateCommand = new AsyncRelayCommand(ApplySamplingRateAsync, () => !IsApplyingSamplingRate);
        ApplyYMinCommand = new RelayCommand(() => ApplyYMin(), () => IsYAxisManualEnabled);
        ApplyYMaxCommand = new RelayCommand(() => ApplyYMax(), () => IsYAxisManualEnabled);

        // Sync text-entry mirrors (string) with numeric properties
        UpdateTextProperties();
    }


    /// <summary>
    /// EXG-mode constructor. Wires sample events, mirrors IMU flags from
    /// <paramref name="config"/>, applies EXG mode flags (ECG/EMG/Test/Resp),
    /// initializes parameter lists and buffers, sets initial axis/rate UI state,
    /// and configures commands. On iOS/macOS, also reacts to live EXG mode changes
    /// coming from the bridge (“dot”) via <c>ExgModeChanged</c>.
    /// </summary>
    /// <param name="shimmerDevice">Connected EXG device instance (bridge).</param>
    /// <param name="config">Initial sensor configuration to mirror in the VM.</param>
    public DataPageViewModel(ShimmerSDK_EXG shimmerDevice, ShimmerDevice config)
    {

        // Keep a reference to the EXG device and start listening to samples
        shimmerExg = shimmerDevice;
        SubscribeSamples();

        // IMU flags
        enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
        enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;
        enablePressureTemperature = config.EnablePressureTemperature;
        enableBattery = config.EnableBattery;
        enableExtA6 = config.EnableExtA6;
        enableExtA7 = config.EnableExtA7;
        enableExtA15 = config.EnableExtA15;

        // Enable EXG and copy its current mode flags
        enableExg = config.EnableExg;
        exgModeECG = config.IsExgModeECG;
        exgModeEMG = config.IsExgModeEMG;
        exgModeTest = config.IsExgModeTest;
        exgModeRespiration = config.IsExgModeRespiration;

        // Seed UI with the current device sampling rate
        samplingRateDisplay = DeviceSamplingRate;

        // Build the parameter list for the combo / selector
        InitializeAvailableParameters();

#if IOS || MACCATALYST

        // iOS/macOS: keep a bridge reference and sync mode title/flags now and on changes
        _exgBridge = shimmerDevice;

        // Initialize from bridge’s current mode (if known)
        ExgModeTitle = shimmerDevice.CurrentExgModeTitle;
        ApplyModeTitleToFlags(ExgModeTitle);
        InitializeAvailableParameters();

        // React to future EXG mode changes coming from the bridge UI indicator (“dot”)
        shimmerDevice.ExgModeChanged += (_, title) =>
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                ExgModeTitle = title;
                ApplyModeTitleToFlags(title);
                InitializeAvailableParameters();
                UpdateChart();
            });
        };

#endif

        // Ensure current selection is valid
        if (!AvailableParameters.Contains(SelectedParameter))
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

        // Prepare empty data buffers for all enabled parameters
        InitializeDataCollections();

        // Apply initial Y-axis/labels based on the selection
        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
        }

        // Capture “last valid” values used by validation rollback
        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _samplingRateText = DeviceSamplingRate.ToString(CultureInfo.InvariantCulture);
        _lastValidSamplingRate = DeviceSamplingRate;
        OnPropertyChanged(nameof(SamplingRateText));
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        // Commands (async for sampling rate; guarded by CanExecute where needed)
        ApplySamplingRateCommand = new AsyncRelayCommand(ApplySamplingRateAsync, () => !IsApplyingSamplingRate);
        ApplyYMinCommand = new RelayCommand(() => ApplyYMin(), () => IsYAxisManualEnabled);
        ApplyYMaxCommand = new RelayCommand(() => ApplyYMax(), () => IsYAxisManualEnabled);

        // Sync text-entry mirrors (string) with numeric properties
        UpdateTextProperties();
    }


    // ----- IDisposable implementation -----

    /// <summary>
    /// Releases resources and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// Core dispose method. When <paramref name="disposing"/> is true,
    /// releases managed resources (unsubscribes events, clears collections).
    /// Safe to call multiple times.
    /// </summary>
    /// <param name="disposing">True to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            UnsubscribeSamples();
            ChartUpdateRequested = null;
            ClearAllDataCollections();
        }

        _disposed = true;
    }


    // ----- Commands & change handlers -----

    /// <summary>
    /// Updates the enabled/disabled state of Y–axis commands when manual mode changes.
    /// </summary>
    /// <param name="value">
    /// <c>true</c> if manual Y–axis editing is enabled; <c>false</c> if auto-scaling is active.
    /// </param>
    partial void OnIsYAxisManualEnabledChanged(bool value)
    {
        (ApplyYMinCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyYMaxCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }


    /// <summary>
    /// Validates and applies the current Y–axis minimum entered in the bound textbox.
    /// </summary>
    private void ApplyYMin()
    {
        ValidateAndUpdateYAxisMin(YAxisMinText);
    }


    /// <summary>
    /// Validates and applies the current Y–axis maximum entered in the bound textbox.
    /// </summary>
    private void ApplyYMax()
    {
        ValidateAndUpdateYAxisMax(YAxisMaxText);
    }


    // ----- Device capability sync (EXG bridge → IMU flags) -----

    /// <summary>
    /// Synchronizes IMU flags from the EXG device and reports whether any value changed.
    /// </summary>
    /// <returns><c>true</c> if at least one flag was updated; otherwise, <c>false</c>.</returns>
    private bool SyncImuFlagsFromExgDeviceIfChanged()
    {
        if (shimmerExg == null) return false;

        bool changed = false;

        void Set(ref bool field, bool value)
        {
            if (field != value) { field = value; changed = true; }
        }

        Set(ref enableLowNoiseAccelerometer, shimmerExg.EnableLowNoiseAccelerometer);
        Set(ref enableWideRangeAccelerometer, shimmerExg.EnableWideRangeAccelerometer);
        Set(ref enableGyroscope, shimmerExg.EnableGyroscope);
        Set(ref enableMagnetometer, shimmerExg.EnableMagnetometer);
        Set(ref enablePressureTemperature, shimmerExg.EnablePressureTemperature);
        Set(ref enableBattery, shimmerExg.EnableBatteryVoltage);
        Set(ref enableExtA6, shimmerExg.EnableExtA6);
        Set(ref enableExtA7, shimmerExg.EnableExtA7);
        Set(ref enableExtA15, shimmerExg.EnableExtA15);

        return changed;
    }


    // ----- Sample handling (event handlers) -----

    /// <summary>
    /// Handles each incoming sample from IMU/EXG:
    /// syncs IMU flags when bridged via EXG, keeps selection valid,
    /// updates buffers and requests a chart refresh.
    /// </summary>
    /// <param name="sender">Source device raising the event.</param>
    /// <param name="sample">Dynamic sample payload from the SDK.</param>
    private void OnSampleReceived(object? sender, dynamic sample)
    {
        if (shimmerExg != null && SyncImuFlagsFromExgDeviceIfChanged())
        {
            InitializeAvailableParameters();

            if (!AvailableParameters.Contains(SelectedParameter))
                SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

            ClearAllDataCollections();
            UpdateChart();
        }

        // Advance global sample count and compute current time (s).
        sampleCounter++;
        double currentTimeSeconds = CurrentTimeInSeconds;

        // Push values into buffers and request a redraw.
        UpdateDataCollectionsWithSingleSample(sample, currentTimeSeconds);
        UpdateChart();
    }


    // ----- Device attach / detach / connect / start / stop -----

    /// <summary>
    /// Re-subscribes to device events safely.
    /// </summary>
    public void AttachToDevice()
    {
        try { UnsubscribeSamples(); SubscribeSamples(); }
        catch { }

    }


    /// <summary>
    /// Unsubscribes from device events safely.
    /// </summary>
    public void DetachFromDevice()
    {
        try { UnsubscribeSamples(); } catch { }

    }


    /// <summary>
    /// Updates ApplySamplingRateCommand CanExecute when busy state changes.
    /// </summary>
    partial void OnIsApplyingSamplingRateChanged(bool value)
    {
        (ApplySamplingRateCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }


    /// <summary>
    /// Connects to the IMU/EXG devices if needed and starts streaming.
    /// Shows a busy hint, attempts connection, then starts streaming on available devices.
    /// </summary>
    /// <returns>A task that completes when the connection and streaming start attempt has finished.</returns>
    public async Task ConnectAndStartAsync()
    {
        ShowBusyRequested?.Invoke(this, "Connecting to device… Please wait.");

        try
        {
            // IMU
            if (shimmerImu != null)
            {
                if (!shimmerImu.IsConnected())
                {
                    shimmerImu.Connect();
                    await Task.Delay(100);
                }
            }

            // EXG
            if (shimmerExg != null)
            {
                if (!shimmerExg.IsConnected())
                {
                    shimmerExg.Connect();
                    await Task.Delay(100);
                }
            }

            DeviceStartStreaming();
        }
        catch (Exception ex)
        {
            ShowAlertRequested?.Invoke(this, $"Bridge connection failed:\n{ex.Message}");
        }
        finally
        {
            HideBusyRequested?.Invoke(this, EventArgs.Empty);
        }
    }


    /// <summary>
    /// Stops streaming and, optionally, disposes device resources.
    /// </summary>
    /// <param name="disconnect">
    /// If <c>true</c>, attempts to dispose device instances after stopping streaming; otherwise only stops streaming.
    /// </param>
    /// <returns>A task that completes when streaming is stopped (and optional disposal is done).</returns>
    public async Task StopAsync(bool disconnect = false)
    {
        await Task.Run(() =>
        {
            try { DeviceStopStreaming(); } catch { }

            if (disconnect)
            {
                try { 
                    (shimmerImu as IDisposable)?.Dispose(); 
                } 
                catch { }
                try {
                    (shimmerExg as IDisposable)?.Dispose(); 
                } 
                catch { }
            }
        });
    }


    // ----- Sampling rate apply / restart -----

    /// <summary>
    /// Validates the user-entered sampling rate, shows a busy overlay,
    /// applies the nearest supported firmware rate asynchronously,
    /// and restarts streaming to make the change effective. Updates UI feedback on success or failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the apply-and-restart flow finishes.</returns>
    private async Task ApplySamplingRateAsync()
    {
        if (!TryParseDouble(SamplingRateText, out var req))
        {
            ValidationMessage = "Sampling rate must be a valid number (no letters or special characters allowed).";
            ResetSamplingRateText();
            return;
        }
        if (req > MAX_SAMPLING_RATE) { ValidationMessage = $"Sampling rate too high. Maximum {MAX_SAMPLING_RATE} Hz."; ResetSamplingRateText(); return; }
        if (req < MIN_SAMPLING_RATE) { ValidationMessage = $"Sampling rate too low. Minimum {MIN_SAMPLING_RATE} Hz."; ResetSamplingRateText(); return; }

        ValidationMessage = "";
        IsApplyingSamplingRate = true;

        ShowBusyRequested?.Invoke(this, "Writing sampling rate to device…\nPlease wait.");

        try
        {
            await Task.Run(() => UpdateSamplingRateAndRestart(req));
            ShowAlertRequested?.Invoke(this, $"Sampling rate set to {DeviceSamplingRate:0.###} Hz.\nClick OK to continue.");
        }
        catch (Exception ex)
        {
            ShowAlertRequested?.Invoke(this, $"Failed to apply sampling rate.\n{ex.Message}");
            ResetSamplingRateText();
        }
        finally
        {
            IsApplyingSamplingRate = false;
            HideBusyRequested?.Invoke(this, EventArgs.Empty);
        }
    }


    /// <summary>
    /// Applies the requested sampling rate (snapped to the nearest supported by firmware),
    /// restarts streaming to apply the change, refreshes UI-bound values and clears buffers.
    /// </summary>
    /// <param name="newRate">Requested sampling rate in Hz.</param>
    private void UpdateSamplingRateAndRestart(double newRate)
    {
        try
        {
            try { DeviceStopStreaming(); } catch { }
            double applied = SetFirmwareSamplingRateNearestUnified(newRate);
            try { DeviceStartStreaming(); } catch { }

            SamplingRateDisplay = applied;
            _lastValidSamplingRate = applied;
            _samplingRateText = applied.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(SamplingRateText));

            ClearAllDataCollections();
            ResetAllCounters();
            ValidationMessage = "";
            UpdateChart();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Unable to apply sampling rate: {ex.Message}";
            ResetSamplingRateText();
        }
    }


    // ----- Data buffers and chart/timeline helpers -----

    /// <summary>
    /// Initializes the internal collections used for storing time series data.
    /// For each enabled sensor parameter, creates empty lists for data and timestamps.
    /// </summary>
    private void InitializeDataCollections()
    {
        // Build a list of all sensor parameter names to store real data for (not group labels)
        var dataParameters = new List<string>();

        // Use predefined static arrays for each group to optimize memory and performance
        if (enableLowNoiseAccelerometer)
            dataParameters.AddRange(LowNoiseAccelerometerAxes);

        if (enableWideRangeAccelerometer)
            dataParameters.AddRange(WideRangeAccelerometerAxes);

        if (enableGyroscope)
            dataParameters.AddRange(GyroscopeAxes);

        if (enableMagnetometer)
            dataParameters.AddRange(MagnetometerAxes);

        if (enablePressureTemperature)
            dataParameters.AddRange(EnvSensors);

        if (enableBattery)
            dataParameters.AddRange(BatteryParams);

        if (enableExtA6)
            dataParameters.Add("ExtADC_A6");
        if (enableExtA7)
            dataParameters.Add("ExtADC_A7");
        if (enableExtA15)
            dataParameters.Add("ExtADC_A15");

        // EXG
        if (enableExg)
        {
            if (!dataPointsCollections.ContainsKey("Exg1"))
            {
                dataPointsCollections["Exg1"] = new List<float>();
                timeStampsCollections["Exg1"] = new List<int>();
            }
            if (!dataPointsCollections.ContainsKey("Exg2"))
            {
                dataPointsCollections["Exg2"] = new List<float>();
                timeStampsCollections["Exg2"] = new List<int>();
            }
        }

        // Create empty data and timestamp collections if not already present
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
    /// Sets/clears the X-axis baseline on first open. If <paramref name="clearBuffers"/> is true,
    /// also clears data and counters so the trace restarts from 0.
    /// </summary>
    /// <param name="clearBuffers">If true, clears buffers and counters; otherwise keeps data.</param>
    public void MarkFirstOpenBaseline(bool clearBuffers = true)
    {
        if (clearBuffers)
        {
            timeBaselineSeconds = 0;
            ClearAllDataCollections();
            ResetAllCounters();
            UpdateChart();
        }
        else
        {
            timeBaselineSeconds = sampleCounter / DeviceSamplingRate;
            UpdateChart();
        }
    }


    /// <summary>
    /// Clears all data and timestamp collections for every parameter.
    /// </summary>
    private void ClearAllDataCollections()
    {
        foreach (var key in dataPointsCollections.Keys.ToList())
        {
            dataPointsCollections[key].Clear();
        }

        foreach (var key in timeStampsCollections.Keys.ToList())
        {
            timeStampsCollections[key].Clear();
        }
    }


    /// <summary>
    /// Ensures that the data and timestamp lists for the given parameter
    /// do not exceed the specified maximum number of points. Removes oldest samples if needed.
    /// </summary>
    /// <param name="parameter">Parameter key to trim.</param>
    /// <param name="maxPoints">Maximum number of samples to retain.</param>
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
    /// Retrieves a snapshot (deep copy) of the data and timestamp series for a given parameter.
    /// This method is thread-safe and returns empty lists if the parameter does not exist.
    /// </summary>
    /// <param name="parameter">The name of the parameter whose data series to retrieve.</param>
    /// <returns>
    /// A tuple containing:
    /// - data: a list of float values representing the parameter data,
    /// - time: a list of int values representing the corresponding timestamps (in ms).
    /// </returns>
    public (List<float> data, List<int> time) GetSeriesSnapshot(string parameter)
    {
        lock (_dataLock)
        {
            string key = MapToInternalKey(parameter);

            var dataList = dataPointsCollections.TryGetValue(key, out var d) ? new List<float>(d) : new List<float>();
            var timeList = timeStampsCollections.TryGetValue(key, out var t) ? new List<int>(t) : new List<int>();

            return (dataList, timeList);
        }
    }


    /// <summary>
    /// Triggers an event to notify that the chart should be updated.
    /// Subscribers to ChartUpdateRequested will be notified.
    /// </summary>
    private void UpdateChart()
    {
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }


    // ----- UI labels & mode description -----

    /// <summary>
    /// Human-readable label for the current chart mode (Multi/Split),
    /// adapted to the selected parameter family (IMU: X·Y·Z, EXG: EXG1·EXG2).
    /// </summary>
    /// <returns>
    /// UI-ready string:
    /// - "Multi Parameter (X, Y, Z)" for IMU groups in Multi,
    /// - "Multi Parameter (EXG1, EXG2)" for EXG in Multi,
    /// - "Split (three charts)" for IMU in Split,
    /// - "Split (two charts)" for EXG in Split,
    /// - "Unified" as a fallback.
    /// </returns>
    public string ChartModeLabel
    {
        get
        {
            var clean = CleanParameterName(SelectedParameter);
            bool isExg = clean is "ECG" or "EMG" or "EXG Test" or "Respiration";

            return ChartDisplayMode switch
            {
                ChartDisplayMode.Multi => isExg
                    ? "Multi Parameter (EXG1, EXG2)"
                    : "Multi Parameter (X, Y, Z)",
                ChartDisplayMode.Split => isExg
                    ? "Split (two separate charts)"
                    : "Split (three separate charts)",
                _ => "Unified"
            };
        }
    }


    // ----- Legend (labels and colors) -----

    // Readable labels for the current legend (bindable from the view)
    public List<string> LegendLabels =>
        GetCurrentSubParameters().Select(p => GetLegendLabel(SelectedParameter, p)).ToList();

    // Single labels for the legend (convenient to bind in XAML)
    public string Legend1Text => LegendLabels.ElementAtOrDefault(0) ?? "";
    public string Legend2Text => LegendLabels.ElementAtOrDefault(1) ?? "";
    public string Legend3Text => LegendLabels.ElementAtOrDefault(2) ?? "";

    // Colors consistent with the drawn series.
    public Color Legend1Color => Colors.Red;
    public Color Legend2Color => (LegendLabels.Count == 2) ? Colors.Blue : Colors.Green;
    public Color Legend3Color => Colors.Blue;

#if IOS || MACCATALYST

    // EXG bridge handle for iOS/macOS
    private ShimmerSDK_EXG? _exgBridge;

    // Backing field for the current EXG mode title (e.g., "ECG", "EMG")
    private string _exgModeTitle = string.Empty;


    /// <summary>
    /// Bindable EXG mode title used by the UI and legend (e.g., "ECG", "EMG").
    /// Notifies changes and also triggers an update of <see cref="HasExgMode"/>.
    /// </summary>
    /// <returns>The current EXG mode title; empty if no mode is set.</returns>
    public string ExgModeTitle
    {
        get => _exgModeTitle;
        private set
        {
            if (_exgModeTitle == value) return;
            _exgModeTitle = value;
            OnPropertyChanged(nameof(ExgModeTitle));
            OnPropertyChanged(nameof(HasExgMode));
        }
    }


    /// <summary>
    /// Indicates whether an EXG mode is currently set (non-empty title).
    /// </summary>
    /// <returns><c>true</c> when an EXG mode title is available; otherwise <c>false</c>.</returns>
    public bool HasExgMode => !string.IsNullOrEmpty(_exgModeTitle);

#endif


    // ----- Auto Y-axis handling -----

    /// <summary>
    /// Handles toggling between auto-scaling and manual Y-axis mode. 
    /// Backs up/restores manual values, recalculates auto range, updates UI, and refreshes the chart.
    /// </summary>
    /// <param name="value">True to enable auto-scaling; false to use manual Y-axis limits.</param>
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
        UpdateYAxisTextPropertiesOnly();

        // Clear any previous validation messages
        ValidationMessage = "";

        // Redraw the chart with the updated range
        UpdateChart();
    }


    /// <summary>
    /// Computes the best-fit Y-axis range for the current selection based on available data,
    /// with a small margin. Falls back to defaults when no data is present.
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
                if (dataPointsCollections.TryGetValue(param, out var list) && list.Count > 0)
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
            var key = MapToInternalKey(cleanParam);

            // If there is no data for this parameter (display→internal key), use the "display" defaults
            if (!dataPointsCollections.TryGetValue(key, out var list) || list.Count == 0)
            {
                _autoYAxisMin = GetDefaultYAxisMin(cleanParam);
                _autoYAxisMax = GetDefaultYAxisMax(cleanParam);
                return;
            }

            var data = dataPointsCollections[key];
            var min = data.Min();
            var max = data.Max();
            var range = max - min;

            if (Math.Abs(range) < 0.001)
            {
                var center = (min + max) / 2;
                var margin = Math.Abs(center) * 0.1 + 0.1;
                _autoYAxisMin = center - margin;
                _autoYAxisMax = center + margin;
            }
            else
            {
                var margin = range * 0.1;
                _autoYAxisMin = min - margin;
                _autoYAxisMax = max + margin;
            }
        }

        // Round the limits
        _autoYAxisMin = Math.Round(_autoYAxisMin, 3);
        _autoYAxisMax = Math.Round(_autoYAxisMax, 3);
    }


    // ----- Text-entry sync helpers -----

    /// <summary>
    /// Synchronizes all text-entry backing strings (Y min/max, time window, X label interval)
    /// with their numeric counterparts and notifies the UI.
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
    /// Updates only the Y-axis text-entry strings from the numeric Y min/max values
    /// and notifies the UI.
    /// </summary>
    private void UpdateYAxisTextPropertiesOnly()
    {
        _yAxisMinText = YAxisMin.ToString(CultureInfo.InvariantCulture);
        _yAxisMaxText = YAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
        OnPropertyChanged(nameof(YAxisMaxText));
    }


    // ----- Validation handlers (user input → numeric state) -----

    /// <summary>
    /// Validates and applies a new Y-axis minimum. Enforces allowed range and consistency with Y max.
    /// Shows a validation message on error and restores the last valid value.
    /// </summary>
    /// <param name="value">User-entered text for Y-axis minimum.</param>
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
    /// Validates and applies a new Y-axis maximum. Enforces allowed range and consistency with Y min.
    /// Shows a validation message on error and restores the last valid value.
    /// </summary>
    /// <param name="value">User-entered text for Y-axis maximum.</param>
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
    /// Validates and applies a new time window (seconds). Enforces allowed range,
    /// clears series data and counters, and refreshes the chart.
    /// </summary>
    /// <param name="value">User-entered text for the time window in seconds.</param>
    private void ValidateAndUpdateTimeWindow(string value)
    {

        // If input is empty, reset to default value and clear data
        if (string.IsNullOrWhiteSpace(value))
        {
            ValidationMessage = "";
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

            // Invalid input: show error and revert text field.
            ValidationMessage = "Time Window must be a valid positive number.";
            ResetTimeWindowText();
        }
    }


    /// <summary>
    /// Validates and applies the X-axis label interval (seconds). Enforces allowed range
    /// and refreshes the chart.
    /// </summary>
    /// <param name="value">User-entered text for the X-axis label interval in seconds.</param>
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


    // ----- Reset helpers -----

    /// <summary>
    /// Resets all internal counters related to sample tracking (e.g., sample index).
    /// </summary>
    private void ResetAllCounters()
    {
        sampleCounter = 0;
    }


    /// <summary>
    /// Restores the sampling-rate text field to the last valid value and notifies the UI.
    /// </summary>
    private void ResetSamplingRateText()
    {
        _samplingRateText = _lastValidSamplingRate.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SamplingRateText));
    }


    /// <summary>
    /// Restores the Y min text field to the last valid value and notifies the UI.
    /// </summary>
    private void ResetYAxisMinText()
    {
        _yAxisMinText = _lastValidYAxisMin.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
    }


    /// <summary>
    /// Restores the Y max text field to the last valid value and notifies the UI.
    /// </summary>
    private void ResetYAxisMaxText()
    {
        _yAxisMaxText = _lastValidYAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMaxText));
    }


    /// <summary>
    /// Restores the time-window text field to the last valid value and notifies the UI.
    /// </summary>
    private void ResetTimeWindowText()
    {
        _timeWindowSecondsText = _lastValidTimeWindowSeconds.ToString();
        OnPropertyChanged(nameof(TimeWindowSecondsText));
    }


    /// <summary>
    /// Restores the X-axis label interval text field to the last valid value and notifies the UI.
    /// </summary>
    private void ResetXAxisIntervalText()
    {
        _xAxisLabelIntervalText = _lastValidXAxisLabelInterval.ToString();
        OnPropertyChanged(nameof(XAxisLabelIntervalText));
    }


    // ----- Default axis ranges -----


    /// <summary>
    /// Provides a default Y-axis minimum for the given parameter or group,
    /// used when no data is available or input is empty.
    /// </summary>
    /// <param name="parameter">Parameter or group display name.</param>
    /// <returns>Default Y-axis minimum.</returns>
    private static double GetDefaultYAxisMin(string parameter)
    {
        return parameter switch
        {
            // Groups
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" => -20,
            "Gyroscope" => -250,
            "Magnetometer" => -5,

            // Single
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
            "BatteryPercent" => 0,     // %
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 0,
            "ECG" or "EMG" or "EXG Test" => -15.0,
            "Respiration" => -15.0,

            _ => 0
        };
    }


    /// <summary>
    /// Provides a default Y-axis maximum for the given parameter or group,
    /// used when no data is available or input is empty.
    /// </summary>
    /// <param name="parameter">Parameter or group display name.</param>
    /// <returns>Default Y-axis maximum.</returns>
    private static double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            // Groups
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" => 20,  
            "Gyroscope" => 250,                                             
            "Magnetometer" => 5,                                            

            // Single
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
            "BatteryPercent" => 100,   // %
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 3.3,
            "ECG" or "EMG" or "EXG Test" => 15.0,
            "Respiration" => 15.0,

            _ => 1
        };
    }

    // ----- Available parameters and mapping -----

    /// <summary>
    /// Rebuilds <see cref="AvailableParameters"/> based on enabled sensors and EXG mode,
    /// and keeps <see cref="SelectedParameter"/> valid.
    /// </summary>
    private void InitializeAvailableParameters()
    {
        AvailableParameters.Clear();

        // Add Low-Noise Accelerometer group and its X/Y/Z axes if enabled
        if (enableLowNoiseAccelerometer)
        {
            AvailableParameters.Add("Low-Noise Accelerometer");
            AvailableParameters.Add("    → Low-Noise Accelerometer — separate charts (X·Y·Z)");
        }

        // Add Wide-Range Accelerometer group and its X/Y/Z axes if enabled
        if (enableWideRangeAccelerometer)
        {
            AvailableParameters.Add("Wide-Range Accelerometer");
            AvailableParameters.Add("    → Wide-Range Accelerometer — separate charts (X·Y·Z)");
        }

        // Add Gyroscope group and its X/Y/Z axes if enabled
        if (enableGyroscope)
        {
            AvailableParameters.Add("Gyroscope");
            AvailableParameters.Add("    → Gyroscope — separate charts (X·Y·Z)");
        }

        // Add Magnetometer group and its X/Y/Z axes if enabled
        if (enableMagnetometer)
        {
            AvailableParameters.Add("Magnetometer");
            AvailableParameters.Add("    → Magnetometer — separate charts (X·Y·Z)");
        }

        // Add BatteryVoltage and BatteryPercent if battery monitoring is enabled
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

        // EXG (group + split variant EXG1·EXG2)
        if (enableExg)
        {
            if (exgModeRespiration)
            {
                AvailableParameters.Add("Respiration");
                AvailableParameters.Add("    → Respiration — separate charts (EXG1·EXG2)");
            }
            else if (exgModeECG)
            {
                AvailableParameters.Add("ECG");
                AvailableParameters.Add("    → ECG — separate charts (EXG1·EXG2)");
            }
            else if (exgModeEMG)
            {
                AvailableParameters.Add("EMG");
                AvailableParameters.Add("    → EMG — separate charts (EXG1·EXG2)");
            }
            else if (exgModeTest)
            {
                AvailableParameters.Add("EXG Test");
                AvailableParameters.Add("    → EXG Test — separate charts (EXG1·EXG2)");
            }
            else
            {
                AvailableParameters.Add("EXG");
                AvailableParameters.Add("    → EXG — separate charts (EXG1·EXG2)");
            }
        }

        // If the current selection is no longer available, select the first parameter
        if (!AvailableParameters.Contains(SelectedParameter))
        {
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";
        }
    }


    /// <summary>
    /// Removes UI adornments (split/multi hints) from a display name.
    /// </summary>
    /// <param name="displayName">Raw display name as shown in the UI.</param>
    /// <returns>Clean parameter/group name.</returns>
    public static string CleanParameterName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "";
        if (displayName.StartsWith("    → ")) displayName = displayName[6..];
        displayName = displayName
            .Replace(" — separate charts (EXG1·EXG2)", "")
            .Replace(" - separate charts (EXG1·EXG2)", "")
            .Replace(" — separate charts (X·Y·Z)", "")
            .Replace(" - separate charts (X·Y·Z)", "")
            .Replace(" (separate charts)", "")
            .Trim();
        return displayName;
    }


    /// <summary>
    /// Maps a display name to its internal series key (e.g., "EXG1" → "Exg1").
    /// </summary>
    /// <param name="displayName">Display name.</param>
    /// <returns>Internal key for series dictionaries.</returns>
    public static string MapToInternalKey(string displayName)
    {
        var name = CleanParameterName(displayName);
        if (name.Equals("EXG1", StringComparison.OrdinalIgnoreCase)) 
            return "Exg1";
        if (name.Equals("EXG2", StringComparison.OrdinalIgnoreCase)) 
            return "Exg2";

        return name;
    }


    /// <summary>
    /// Detects whether a display label refers to a split variant (separate charts).
    /// </summary>
    /// <param name="displayName">Display label to check.</param>
    /// <returns>True if it's a split variant label; otherwise false.</returns>
    private static bool IsSplitVariantLabel(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return false;
        return displayName.Contains("separate charts", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("split", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Determines if a parameter represents a multi-series group (e.g., X/Y/Z or EXG1/EXG2).
    /// </summary>
    /// <param name="parameter">Parameter or group name.</param>
    /// <returns>True if it’s a multi-series group; otherwise false.</returns>
    private static bool IsMultiChart(string parameter)
    {
        string cleanName = CleanParameterName(parameter);
              return cleanName is "Low-Noise Accelerometer" or "Wide-Range Accelerometer"
                or "Gyroscope" or "Magnetometer"
                or "ECG" or "EMG" or "EXG Test" or "Respiration" or "EXG";
    }


    // ----- Parameter groups and legend helpers -----

    /// <summary>
    /// Returns the list of sub-parameters for a given sensor group (e.g., X/Y/Z or EXG1/EXG2).
    /// Returns an empty list if the group is not recognized.
    /// </summary>
    /// <param name="groupParameter">Display/group name selected by the user.</param>
    /// <returns>List of parameter names for the group (e.g., X/Y/Z or EXG1/EXG2).</returns>
    public static List<string> GetSubParameters(string groupParameter)
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
            "EXG" or "ECG" or "EMG" or "EXG Test" or "Respiration" => new List<string> { "Exg1", "Exg2" },
            _ => new List<string>()  // Return empty if group not recognized
        };

    }


    /// <summary>
    /// Returns the legend label for a sub-parameter within a group (e.g., "X", "Y", "Z", or "EXG1"/"EXG2").
    /// </summary>
    /// <param name="groupParameter">Group display name.</param>
    /// <param name="subParameter">Sub-parameter internal name (e.g., "GyroscopeX", "Exg1").</param>
    /// <returns>Readable legend label for the UI.</returns>
    public static string GetLegendLabel(string groupParameter, string subParameter)
    {
        var group = CleanParameterName(groupParameter);

        // EXG groups: prefer EXG1/EXG2 labels
        if (group is "ECG" or "EMG" or "EXG Test" or "Respiration" or "EXG")
        {
            return subParameter switch
            {
                "Exg1" => "EXG1",
                "Exg2" => "EXG2",
                _ => subParameter
            };
        }

        // IMU groups: compress axis suffix
        if (subParameter.EndsWith("X")) return "X";
        if (subParameter.EndsWith("Y")) return "Y";
        if (subParameter.EndsWith("Z")) return "Z";

        // Single parameters: reuse cleaned name
        return CleanParameterName(subParameter);
    }


    // ----- Selection -> sub-parameters -----

    /// <summary>
    /// Returns sub-parameters for the currently selected parameter. 
    /// If the selection is a group (multi-chart), returns all sub-parameters; 
    /// otherwise returns a single-item list with the cleaned name.
    /// </summary>
    /// <returns>List of sub-parameter names for the current selection.</returns>
    public List<string> GetCurrentSubParameters()
    {
        string cleanName = CleanParameterName(SelectedParameter);
        return IsMultiChart(cleanName) ? GetSubParameters(cleanName) : new List<string> { cleanName };
    }


    // ----- Sample ingestion and storage -----

    /// <summary>
    /// Try to extract a numeric value from a dynamic sample field:
    /// accepts primitives or wrappers exposing a public 'Data' property.
    /// </summary>
    /// <param name="sample">Dynamic sample object.</param>
    /// <param name="field">Field/property name to read.</param>
    /// <param name="val">On success, receives the numeric value.</param>
    /// <returns>True if a numeric value was found and converted; otherwise false.</returns>
    private static bool TryGetNumeric(dynamic sample, string field, out float val)
    {
        val = 0f;
        try
        {
            var pi = sample?.GetType().GetProperty(field);
            if (pi == null) return false;
            var x = pi.GetValue(sample);
            if (x == null) return false;

            // Case 1: already number
            if (x is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
            { val = Convert.ToSingle(x); return true; }

            // Case 2: Wrapper with .Data property
            var dp = x.GetType().GetProperty("Data");
            if (dp != null)
            {
                var inner = dp.GetValue(x);
                if (inner == null) return false;
                val = Convert.ToSingle(inner);
                return true;
            }
        }
        catch { }
        return false;
    }


    /// <summary>
    /// Process one device sample: gather enabled values, compute battery %, push into
    /// data/timestamp collections (bounded by time window), and optionally auto-scale Y.
    /// </summary>
    /// <param name="sample">Latest dynamic device sample.</param>
    /// <param name="currentTimeSeconds">Current timestamp in seconds.</param>
    private void UpdateDataCollectionsWithSingleSample(dynamic sample, double currentTimeSeconds)
    {
        var values = new Dictionary<string, float>();

        try
        {
            // Low-noise Accelerometer
            if (enableLowNoiseAccelerometer)
            {
                if (HasProp(sample, "LowNoiseAccelerometerX") && sample.LowNoiseAccelerometerX != null)
                    values["Low-Noise AccelerometerX"] = (float)sample.LowNoiseAccelerometerX.Data;
                if (HasProp(sample, "LowNoiseAccelerometerY") && sample.LowNoiseAccelerometerY != null)
                    values["Low-Noise AccelerometerY"] = (float)sample.LowNoiseAccelerometerY.Data;
                if (HasProp(sample, "LowNoiseAccelerometerZ") && sample.LowNoiseAccelerometerZ != null)
                    values["Low-Noise AccelerometerZ"] = (float)sample.LowNoiseAccelerometerZ.Data;
            }

            // Wide-range Accelerometer
            if (enableWideRangeAccelerometer)
            {
                if (HasProp(sample, "WideRangeAccelerometerX") && sample.WideRangeAccelerometerX != null)
                    values["Wide-Range AccelerometerX"] = (float)sample.WideRangeAccelerometerX.Data;
                if (HasProp(sample, "WideRangeAccelerometerY") && sample.WideRangeAccelerometerY != null)
                    values["Wide-Range AccelerometerY"] = (float)sample.WideRangeAccelerometerY.Data;
                if (HasProp(sample, "WideRangeAccelerometerZ") && sample.WideRangeAccelerometerZ != null)
                    values["Wide-Range AccelerometerZ"] = (float)sample.WideRangeAccelerometerZ.Data;
            }

            // Gyroscope
            if (enableGyroscope)
            {
                if (HasProp(sample, "GyroscopeX") && sample.GyroscopeX != null)
                    values["GyroscopeX"] = (float)sample.GyroscopeX.Data;
                if (HasProp(sample, "GyroscopeY") && sample.GyroscopeY != null)
                    values["GyroscopeY"] = (float)sample.GyroscopeY.Data;
                if (HasProp(sample, "GyroscopeZ") && sample.GyroscopeZ != null)
                    values["GyroscopeZ"] = (float)sample.GyroscopeZ.Data;
            }

            // Magnetometer
            if (enableMagnetometer)
            {
                if (HasProp(sample, "MagnetometerX") && sample.MagnetometerX != null)
                    values["MagnetometerX"] = (float)sample.MagnetometerX.Data;
                if (HasProp(sample, "MagnetometerY") && sample.MagnetometerY != null)
                    values["MagnetometerY"] = (float)sample.MagnetometerY.Data;
                if (HasProp(sample, "MagnetometerZ") && sample.MagnetometerZ != null)
                    values["MagnetometerZ"] = (float)sample.MagnetometerZ.Data;
            }

            // BMP180
            if (enablePressureTemperature)
            {
                if (HasProp(sample, "Temperature_BMP180") && sample.Temperature_BMP180 != null)
                    values["Temperature_BMP180"] = (float)sample.Temperature_BMP180.Data;
                if (HasProp(sample, "Pressure_BMP180") && sample.Pressure_BMP180 != null)
                    values["Pressure_BMP180"] = (float)sample.Pressure_BMP180.Data;
            }

            // Battery
            if (enableBattery && HasProp(sample, "BatteryVoltage") && sample.BatteryVoltage != null)
            {
                values["BatteryVoltage"] = (float)sample.BatteryVoltage.Data / 1000f; // mV → V

                float batteryV = values["BatteryVoltage"];
                float percent;
                if (batteryV <= 3.3f) percent = 0;
                else if (batteryV >= 4.2f) percent = 100;
                else if (batteryV <= 4.10f) percent = (batteryV - 3.3f) / (4.10f - 3.3f) * 97f;
                else percent = 97f + (batteryV - 4.10f) / (4.20f - 4.10f) * 3f;

                values["BatteryPercent"] = Math.Clamp(percent, 0, 100);
            }

            // External ADC
            if (enableExtA6 && HasProp(sample, "ExtADC_A6") && sample.ExtADC_A6 != null)
                values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data / 1000f;
            if (enableExtA7 && HasProp(sample, "ExtADC_A7") && sample.ExtADC_A7 != null)
                values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data / 1000f;
            if (enableExtA15 && HasProp(sample, "ExtADC_A15") && sample.ExtADC_A15 != null)
                values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data / 1000f;

            // EXG (two channels)
            if (enableExg)
            {

#if IOS || MACCATALYST

                if (TryGetNumeric(sample, "Exg1", out float vExg1))
                    values["Exg1"] = vExg1;

                if (TryGetNumeric(sample, "Exg2", out float vExg2))
                    values["Exg2"] = vExg2;

#elif WINDOWS

                if (HasProp(sample, "Exg1") && sample.Exg1 != null)
                    values["Exg1"] = (float)sample.Exg1.Data;
                if (HasProp(sample, "Exg2") && sample.Exg2 != null)
                    values["Exg2"] = (float)sample.Exg2.Data;

#elif ANDROID

                if (HasProp(sample, "Exg1") && sample.Exg1 != null)
                    values["Exg1"] = (float)sample.Exg1.Data;
                if (HasProp(sample, "Exg2") && sample.Exg2 != null)
                    values["Exg2"] = (float)sample.Exg2.Data;

#endif

            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
            return;
        }

        // Store (thread-safe)
        lock (_dataLock)
        {
            int timestampMs = (int)Math.Round(currentTimeSeconds * 1000);
            int maxPoints = (int)(TimeWindowSeconds * DeviceSamplingRate);


            foreach (var kv in values)
            {
                var key = kv.Key;
                var v = kv.Value;

                if (!dataPointsCollections.ContainsKey(key))
                {
                    dataPointsCollections[key] = new List<float>();
                    timeStampsCollections[key] = new List<int>();
                }

                dataPointsCollections[key].Add(v);
                timeStampsCollections[key].Add(timestampMs);
                TrimCollection(key, maxPoints);
            }
        }

        // Auto Y-axis
        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();

            if (Math.Abs(YAxisMin - _autoYAxisMin) > 0.01 || Math.Abs(YAxisMax - _autoYAxisMax) > 0.01)
            {
                YAxisMin = _autoYAxisMin;
                YAxisMax = _autoYAxisMax;
                UpdateYAxisTextPropertiesOnly();
            }
        }
    }


    /// <summary>
    /// True if the dynamic object exposes a property or field with the given name.
    /// </summary>
    /// <param name="obj">Dynamic object to probe.</param>
    /// <param name="name">Member name.</param>
    /// <returns>True if property or field exists; otherwise false.</returns>
    private static bool HasProp(dynamic obj, string name)
    {
        try
        {
            var t = obj.GetType();
            return t.GetProperty(name) != null || t.GetField(name) != null;
        }
        catch { return false; }
    }


    // ----- Selection change -> chart mode and Y axis -----

    /// <summary>
    /// Reacts to SelectedParameter changes: sets display mode (Multi/Split), 
    /// updates Y-axis settings and legend bindings, and refreshes the chart.
    /// </summary>
    /// <param name="value">Newly selected parameter display name.</param>
    partial void OnSelectedParameterChanged(string value)
    {
        value ??= string.Empty;
        bool split = IsSplitVariantLabel(value);
        string cleanName = CleanParameterName(value);

        ChartDisplayMode = split ? ChartDisplayMode.Split : ChartDisplayMode.Multi;

        OnPropertyChanged(nameof(ChartModeLabel));
        OnPropertyChanged(nameof(IsExgSplit));

        UpdateYAxisSettings(cleanName);

        if (AutoYAxis)
        {
            CalculateAutoYAxisRange();
            YAxisMin = _autoYAxisMin;
            YAxisMax = _autoYAxisMax;
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        UpdateTextProperties();
        OnPropertyChanged(nameof(LegendLabels));
        OnPropertyChanged(nameof(Legend1Text));
        OnPropertyChanged(nameof(Legend2Text));
        OnPropertyChanged(nameof(Legend3Text));
        OnPropertyChanged(nameof(Legend1Color));
        OnPropertyChanged(nameof(Legend2Color));
        OnPropertyChanged(nameof(Legend3Color));
        OnPropertyChanged(nameof(LegendLabels));
        ValidationMessage = "";
        UpdateChart();
    }


    /// <summary>
    /// Sets Y-axis label/unit/title and default min/max based on selected parameter.
    /// </summary>
    /// <param name="parameter">Selected parameter display name.</param>
    private void UpdateYAxisSettings(string parameter)
    {

        // Clean up the parameter name before checking
        string cleanName = CleanParameterName(parameter);

        switch (cleanName)
        {

            // Groups
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

            // Single axis
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

            // BMP180 sensor
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

            // Battery monitoring
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

            // External ADC
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

            // EXG mode
            case "ECG":
                YAxisLabel = "ECG"; YAxisUnit = "mV";
                ChartTitle = "ECG";
                YAxisMin = -15; YAxisMax = 15;
                break;
            case "EMG":
                YAxisLabel = "EMG"; YAxisUnit = "mV";
                ChartTitle = "EMG";
                YAxisMin = -15; YAxisMax = 15;
                break;
            case "EXG Test":
                YAxisLabel = "EXG Test"; YAxisUnit = "mV";
                ChartTitle = "EXG Test";
                YAxisMin = -15; YAxisMax = 15;
                break;
            case "Respiration":
                YAxisLabel = "Respiration";
                YAxisUnit = "mV";
                ChartTitle = "Respiration";
                YAxisMin = -15;
                YAxisMax = 15;
                break;
        }
    }


    // ----- Parsing helpers (double / int) -----

    /// <summary>
    /// Attempts to parse a string into a valid double. Accepts temporary inputs like "+" or "-",
    /// and both '.' and ',' as decimal separators.
    /// </summary>
    /// <param name="input">User input string.</param>
    /// <param name="result">
    /// Parsed double value (0 for temporary '+'/'-') if successful; otherwise undefined.
    /// </param>
    /// <returns>True if the input is a valid double or a temporary sign; otherwise false.</returns>
    public static bool TryParseDouble(string input, out double result)
    {
        result = 0;

        // Reject null, empty, or whitespace input
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleanInput = input.Trim();
        if (string.IsNullOrEmpty(cleanInput))
            return false;

        // Accept single '+' or '-' as a temporary editing state
        if (cleanInput == "-" || cleanInput == "+")
        {
            result = 0;
            return true;
        }

        // Check for valid characters and proper position of sign
        for (int i = 0; i < cleanInput.Length; i++)
        {
            char c = cleanInput[i];

            // Only allow "+" or "-" as the very first character
            if (c == '-' || c == '+')
            {
                if (i != 0) // Sign is only valid at the first position
                    return false;
            }
            else if (c == '.' || c == ',')
            {
                // Accept "." or "," as possible decimal separators
                continue;
            }
            else if (!char.IsDigit(c))
            {
                // Reject any other non-digit character
                return false;
            }
        }

        // Parse with both invariant and current culture
        return double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
               double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }


    /// <summary>
    /// Attempts to parse a string into a valid integer. Accepts optional leading '+' or '-'.
    /// </summary>
    /// <param name="input">User input string.</param>
    /// <param name="result">Parsed integer value if successful; otherwise undefined.</param>
    /// <returns>True if the input is a valid integer; otherwise false.</returns>
    public static bool TryParseInt(string input, out int result)
    {
        result = 0;

        // Reject null, empty, or whitespace input
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleanInput = input.Trim();
        if (string.IsNullOrEmpty(cleanInput))
            return false;

        // Check for valid characters (digits, optional leading '+' or '-')
        foreach (char c in cleanInput)
        {
            if (!char.IsDigit(c) && c != '-' && c != '+')
                return false;
        }

        // Attempt to parse the cleaned input as an integer
        return int.TryParse(cleanInput, out result);
    }


    // ----- Sensor configuration snapshot -----

    /// <summary>
    /// Builds a snapshot of current sensor enable flags as a <see cref="ShimmerDevice"/>.
    /// </summary>
    /// <returns>ShimmerDevice populated with the current sensor configuration.</returns>
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
            EnableExtA15 = enableExtA15,

            // EXG
            EnableExg = enableExg,
            IsExgModeECG = exgModeECG,
            IsExgModeEMG = exgModeEMG,
            IsExgModeTest = exgModeTest,
            IsExgModeRespiration = exgModeRespiration
        };

    }

#if IOS || MACCATALYST


    /// <summary>
    /// Maps a human-readable EXG mode title to internal EXG flags (ECG/EMG/Test/Respiration).
    /// </summary>
    /// <param name="title">Mode title to apply.</param>
    private void ApplyModeTitleToFlags(string? title)
    {
        var t = (title ?? "").Trim();
        exgModeECG         = t.Equals("ECG", StringComparison.OrdinalIgnoreCase);
        exgModeEMG         = t.Equals("EMG", StringComparison.OrdinalIgnoreCase);
        exgModeTest        = t.Equals("EXG Test", StringComparison.OrdinalIgnoreCase);
        exgModeRespiration = t.Equals("Respiration", StringComparison.OrdinalIgnoreCase);
        enableExg = true;   // we are displaying EXG anyway
    }

#endif


    // ----- Timestamp utilities -----

    /// <summary>
    /// Rebuilds all timestamps as evenly spaced (ms) from zero, using the current sampling rate.
    /// Thread-safe.
    /// </summary>
    public void ResetAllTimestamps()
    {
        lock (_dataLock)
        {

            // For each parameter, update each timestamp to be evenly spaced based on sampling rate
            foreach (var param in timeStampsCollections.Keys.ToList())
            {
                int count = timeStampsCollections[param].Count;
                for (int i = 0; i < count; i++)
                {

                    // Set timestamp in milliseconds, as in regular acquisition
                    timeStampsCollections[param][i] = (int)(i * (1000.0 / DeviceSamplingRate));
                }
            }
        }
    }
}
