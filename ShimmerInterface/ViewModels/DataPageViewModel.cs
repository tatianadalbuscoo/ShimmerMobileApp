using CommunityToolkit.Mvvm.ComponentModel;
using ShimmerSDK.IMU;
using System.Collections.ObjectModel;
using System.Globalization;
using ShimmerInterface.Models;
using System.Linq;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using System.Reflection;
using System.Globalization;


#if IOS || MACCATALYST
using Microsoft.Maui.ApplicationModel;  // MainThread
#endif

using ShimmerSDK.EXG;  // XR2Learn_ShimmerEXG, ExgMode




namespace ShimmerInterface.ViewModels;


/// <summary>
/// Specifies the chart visualization mode: either a single parameter (e.g., only X),
/// or multiple parameters (e.g., X, Y, Z on the same chart).
/// </summary>
public enum ChartDisplayMode { Single, Multi, Split }


/// <summary>
/// ViewModel for the DataPage.
/// Manages real-time data acquisition, sensor configuration, and chart display options for a connected Shimmer device.
/// Exposes observable properties and commands for UI binding, following the MVVM pattern.
/// Implements IDisposable for proper cleanup of timers and resources.
/// </summary>
public partial class DataPageViewModel : ObservableObject, IDisposable
{
#if IOS || MACCATALYST
    // --- EXG mode (il “pallino” dal bridge) ---
    private ShimmerSDK_EXG? _exgBridge;
    private string _exgModeTitle = string.Empty;

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

    public bool HasExgMode => !string.IsNullOrEmpty(_exgModeTitle);
#endif

    // ==== Application-wide numeric limits for validation ====
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

    // ==== Device references and internal timer for periodic updates ====
    private readonly ShimmerSDK_IMU? shimmerImu;

private readonly ShimmerSDK_EXG? shimmerExg;

    // flag di sessione EXG (copiati dal config)
    private bool enableExg;
    private bool exgModeECG;
    private bool exgModeEMG;
    private bool exgModeTest;
    private bool exgModeRespiration;
    private bool _disposed = false;

    // ==== Data storage for real-time series ====
    private readonly Dictionary<string, List<float>> dataPointsCollections = new();
    private readonly Dictionary<string, List<int>> timeStampsCollections = new();
    private readonly object _dataLock = new();
    private int sampleCounter = 0;

    // ==== Sensor enablement flags (from current device config) ====
    // These indicate which sensors are enabled for this device/session
    private bool enableLowNoiseAccelerometer;
    private  bool enableWideRangeAccelerometer;
    private  bool enableGyroscope;
    private bool enableMagnetometer;
    private  bool enablePressureTemperature;
    private  bool enableBattery;
    private  bool enableExtA6;
    private  bool enableExtA7;
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

    // ==== Parameter name arrays for each sensor group ====
    private static readonly string[] LowNoiseAccelerometerAxes = ["Low-Noise AccelerometerX", "Low-Noise AccelerometerY", "Low-Noise AccelerometerZ"];
    private static readonly string[] WideRangeAccelerometerAxes = [ "Wide-Range AccelerometerX", "Wide-Range AccelerometerY", "Wide-Range AccelerometerZ" ];
    private static readonly string[] GyroscopeAxes = [ "GyroscopeX", "GyroscopeY", "GyroscopeZ" ];
    private static readonly string[] MagnetometerAxes = [ "MagnetometerX", "MagnetometerY", "MagnetometerZ" ];
    private static readonly string[] EnvSensors = [ "Temperature_BMP180", "Pressure_BMP180" ];
    private static readonly string[] BatteryParams = ["BatteryVoltage", "BatteryPercent"];

    private double timeBaselineSeconds = 0;

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

    // AVVISO/STATO durante la scrittura del sampling rate
    [ObservableProperty] private bool isApplyingSamplingRate;

    public event EventHandler<string>? ShowBusyRequested;
    public event EventHandler? HideBusyRequested;
    public event EventHandler<string>? ShowAlertRequested;

    public IRelayCommand ApplyYMinCommand { get; }
    public IRelayCommand ApplyYMaxCommand { get; }

    public bool IsExgSplit =>
    ChartDisplayMode == ChartDisplayMode.Split &&
    (CleanParameterName(SelectedParameter) is "EXG" or "ECG" or "EMG" or "EXG Test" or "Respiration");



    // Command da bindare al bottone ✓
    public IAsyncRelayCommand ApplySamplingRateCommand { get; }

    private double DeviceSamplingRate => shimmerImu?.SamplingRate
                                     ?? shimmerExg?.SamplingRate
                                         ?? 51.2;


    private void DeviceStartStreaming()
    {
        try { shimmerImu?.StartStreaming(); } catch { }
    try { shimmerExg?.StartStreaming(); } catch { }
    }
    private void DeviceStopStreaming()
    {
        try { shimmerImu?.StopStreaming(); } catch { }
    try { shimmerExg?.StopStreaming(); } catch { }
    }


    private double SetFirmwareSamplingRateNearestUnified(double newRate)
    {
        if (shimmerImu != null) return shimmerImu.SetFirmwareSamplingRateNearest(newRate);
    if (shimmerExg != null) return shimmerExg.SetFirmwareSamplingRateNearest(newRate);
        return newRate;
    }


    private void SubscribeSamples()
    {
        if (shimmerImu != null) shimmerImu.SampleReceived += OnSampleReceived;

    if (shimmerExg != null) shimmerExg.SampleReceived += OnSampleReceived;
    }
    private void UnsubscribeSamples()
    {
        if (shimmerImu != null) shimmerImu.SampleReceived -= OnSampleReceived;
    if (shimmerExg != null) shimmerExg.SampleReceived -= OnSampleReceived;
    }




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
    public double CurrentTimeInSeconds
        => Math.Max(0, (sampleCounter / DeviceSamplingRate) - timeBaselineSeconds);




    /// <summary>
    /// Gets or sets the sampling rate value entered by the user (string for binding).
    /// Triggers validation and updates sampling logic on change.
    /// </summary>
    public string SamplingRateText
    {
        get => _samplingRateText;
        set
        {
            // solo aggiorna il testo; niente apply finché non premi Enter
            SetProperty(ref _samplingRateText, value);
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
            // Just update the text. Do NOT validate here.
            SetProperty(ref _yAxisMinText, value);
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
            // Just update the text. Do NOT validate here.
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


    // ==== IDisposable implementation ====


    // ==== IDisposable implementation ====

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // sgancia eventi e pulisci risorse gestite
            UnsubscribeSamples();
            ChartUpdateRequested = null;
            ClearAllDataCollections();
        }

        _disposed = true;
    }



    public DataPageViewModel(ShimmerSDK_IMU shimmerDevice, ShimmerDevice config)
    {
        shimmerImu = shimmerDevice;
        SubscribeSamples();

        // IMU: copia sensori
        enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
        enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
        enableGyroscope = config.EnableGyroscope;
        enableMagnetometer = config.EnableMagnetometer;
        enablePressureTemperature = config.EnablePressureTemperature;
        enableBattery = config.EnableBattery;
        enableExtA6 = config.EnableExtA6;
        enableExtA7 = config.EnableExtA7;
        enableExtA15 = config.EnableExtA15;

        // EXG: in sessione IMU disattiva tutto
        enableExg = false;
        exgModeECG = exgModeEMG = exgModeTest = exgModeRespiration = false;

        samplingRateDisplay = DeviceSamplingRate;

        InitializeAvailableParameters();

        if (!AvailableParameters.Contains(SelectedParameter))
            SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

        InitializeDataCollections();

        if (!string.IsNullOrEmpty(SelectedParameter))
        {
            UpdateYAxisSettings(SelectedParameter);
            IsXAxisLabelIntervalEnabled = SelectedParameter != "HeartRate";
        }

        _lastValidYAxisMin = YAxisMin;
        _lastValidYAxisMax = YAxisMax;
        _samplingRateText = DeviceSamplingRate.ToString(CultureInfo.InvariantCulture);
        _lastValidSamplingRate = DeviceSamplingRate;
        OnPropertyChanged(nameof(SamplingRateText));
        _lastValidTimeWindowSeconds = TimeWindowSeconds;
        _lastValidXAxisLabelInterval = XAxisLabelInterval;

        ApplySamplingRateCommand = new AsyncRelayCommand(ApplySamplingRateAsync, () => !IsApplyingSamplingRate);
        ApplyYMinCommand = new RelayCommand(() => ApplyYMin(), () => IsYAxisManualEnabled);
        ApplyYMaxCommand = new RelayCommand(() => ApplyYMax(), () => IsYAxisManualEnabled);

        UpdateTextProperties();
    }

public DataPageViewModel(ShimmerSDK_EXG shimmerDevice, ShimmerDevice config)
{
    shimmerExg = shimmerDevice;
    SubscribeSamples();

    // sensori IMU: puoi mantenerli se l’EXG li forwarda
    enableLowNoiseAccelerometer = config.EnableLowNoiseAccelerometer;
    enableWideRangeAccelerometer = config.EnableWideRangeAccelerometer;
    enableGyroscope = config.EnableGyroscope;
    enableMagnetometer = config.EnableMagnetometer;
    enablePressureTemperature = config.EnablePressureTemperature;
    enableBattery = config.EnableBattery;
    enableExtA6 = config.EnableExtA6;
    enableExtA7 = config.EnableExtA7;
    enableExtA15 = config.EnableExtA15;

    // EXG: abilita e copia modalità
    enableExg = config.EnableExg;
    exgModeECG = config.IsExgModeECG;
    exgModeEMG = config.IsExgModeEMG;
    exgModeTest = config.IsExgModeTest;
    exgModeRespiration = config.IsExgModeRespiration;

    samplingRateDisplay = DeviceSamplingRate;

    InitializeAvailableParameters();
#if IOS || MACCATALYST
_exgBridge = shimmerDevice;

// inizializza da ciò che eventualmente è già noto
ExgModeTitle = shimmerDevice.CurrentExgModeTitle;
ApplyModeTitleToFlags(ExgModeTitle);
InitializeAvailableParameters();

// ascolta i cambi del “pallino”
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

        if (!AvailableParameters.Contains(SelectedParameter))
        SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

    InitializeDataCollections();

    if (!string.IsNullOrEmpty(SelectedParameter))
    {
        UpdateYAxisSettings(SelectedParameter);
        IsXAxisLabelIntervalEnabled = SelectedParameter != "HeartRate";
    }

    _lastValidYAxisMin = YAxisMin;
    _lastValidYAxisMax = YAxisMax;
    _samplingRateText = DeviceSamplingRate.ToString(CultureInfo.InvariantCulture);
    _lastValidSamplingRate = DeviceSamplingRate;
    OnPropertyChanged(nameof(SamplingRateText));
    _lastValidTimeWindowSeconds = TimeWindowSeconds;
    _lastValidXAxisLabelInterval = XAxisLabelInterval;

    ApplySamplingRateCommand = new AsyncRelayCommand(ApplySamplingRateAsync, () => !IsApplyingSamplingRate);
    ApplyYMinCommand = new RelayCommand(() => ApplyYMin(), () => IsYAxisManualEnabled);
    ApplyYMaxCommand = new RelayCommand(() => ApplyYMax(), () => IsYAxisManualEnabled);

    UpdateTextProperties();
}


    partial void OnIsYAxisManualEnabledChanged(bool value)
    {
        (ApplyYMinCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (ApplyYMaxCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void ApplyYMin()
    {
        // Riusa la stessa validazione dell'Entry
        ValidateAndUpdateYAxisMin(YAxisMinText);
    }


    private void ApplyYMax()
    {
        // Riusa la stessa validazione dell'Entry
        ValidateAndUpdateYAxisMax(YAxisMaxText);
    }

    /// <summary>
    /// Sincronizza i flag IMU/env dal device EXG (se sono cambiati) e dice se qualcosa è cambiato.
    /// </summary>
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


    private void OnSampleReceived(object? sender, dynamic sample)
    {
        // Se sono collegato via EXG, sincronizzo i flag IMU/env dal bridge.
        // Se qualcosa è cambiato, rigenero lista parametri, sistemo la selezione e pulisco i buffer.
        if (shimmerExg != null && SyncImuFlagsFromExgDeviceIfChanged())
        {
            InitializeAvailableParameters();

            if (!AvailableParameters.Contains(SelectedParameter))
                SelectedParameter = AvailableParameters.FirstOrDefault() ?? "";

            ClearAllDataCollections();
            UpdateChart();
        }

        // Incrementa il contatore e calcola il tempo corrente
        sampleCounter++;
        double currentTimeSeconds = CurrentTimeInSeconds; 

        // Aggiorna strutture e grafico (riusi le tue funzioni attuali)
        UpdateDataCollectionsWithSingleSample(sample, currentTimeSeconds);
        UpdateChart();
    }


#if IOS || MACCATALYST
private void ApplyModeTitleToFlags(string? title)
{
    var t = (title ?? "").Trim();
    exgModeECG         = t.Equals("ECG", StringComparison.OrdinalIgnoreCase);
    exgModeEMG         = t.Equals("EMG", StringComparison.OrdinalIgnoreCase);
    exgModeTest        = t.Equals("EXG Test", StringComparison.OrdinalIgnoreCase);
    exgModeRespiration = t.Equals("Respiration", StringComparison.OrdinalIgnoreCase);
    enableExg = true; // stiamo comunque mostrando EXG
}
#endif


    /// <summary>
    /// Imposta il baseline dell'asse X alla prima apertura della pagina.
    /// Se clearBuffers=true, azzera anche dati e contatori, così la traccia parte vuota da 0.
    /// </summary>
    public void MarkFirstOpenBaseline(bool clearBuffers = true)
    {
        if (clearBuffers)
        {
            // partenza "pulita"
            timeBaselineSeconds = 0;
            ClearAllDataCollections();
            ResetAllCounters();
            UpdateChart();
        }
        else
        {
            // solo ri-baseline senza perdere i dati
            timeBaselineSeconds = sampleCounter / DeviceSamplingRate;
            UpdateChart();
        }
    }


    public void ApplySamplingRateNow()
    {
        if (TryParseDouble(SamplingRateText, out var req))
        {
            if (req > MAX_SAMPLING_RATE) { ValidationMessage = $"Sampling rate too high. Maximum {MAX_SAMPLING_RATE} Hz."; ResetSamplingRateText(); return; }
            if (req < MIN_SAMPLING_RATE) { ValidationMessage = $"Sampling rate too low. Minimum {MIN_SAMPLING_RATE} Hz."; ResetSamplingRateText(); return; }

            ValidationMessage = "";
            UpdateSamplingRateAndRestart(req);
        }
        else
        {
            ValidationMessage = "Sampling rate must be a valid number (no letters or special characters allowed).";
            ResetSamplingRateText();
        }
    }

    private static bool TryUnwrap(object? v, out float value)
    {
        value = 0;
        if (v == null) return false;

        var vt = v.GetType();
        var dataProp = vt.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (dataProp != null)
        {
            var dataVal = dataProp.GetValue(v);
            if (dataVal is IConvertible)
            {
                value = Convert.ToSingle(dataVal, CultureInfo.InvariantCulture);
                return true;
            }
        }
        if (v is IConvertible)
        {
            value = Convert.ToSingle(v, CultureInfo.InvariantCulture);
            return true;
        }
        return false;
    }

    private static bool TryGetScalar(object obj, string name, out float value)
    {
        value = 0;
        var t = obj.GetType();
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (p != null)
        {
            var v = p.GetValue(obj);
            return TryUnwrap(v, out value);
        }
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (f != null)
        {
            var v = f.GetValue(obj);
            return TryUnwrap(v, out value);
        }
        return false;
    }

    // prova su sample e (se esiste) dentro a contenitori tipici: Exg/EXG/Ecg/ECG
    private static bool TryGetAnyEXG(object sample, string[] names, out float value)
    {
        foreach (var n in names)
            if (TryGetScalar(sample, n, out value))
                return true;

        string[] containers = { "Exg", "EXG", "Ecg", "ECG" };
        var t = sample.GetType();
        foreach (var c in containers)
        {
            var containerProp = t.GetProperty(c, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (containerProp?.GetValue(sample) is object inner)
            {
                foreach (var n in names)
                    if (TryGetScalar(inner, n, out value))
                        return true;
            }
        }
        value = 0;
        return false;
    }


    public void AttachToDevice()
    {
        try { UnsubscribeSamples(); SubscribeSamples(); }
        catch { }

    }

    public void DetachFromDevice()
    {
        try { UnsubscribeSamples(); } catch { }

    }


    partial void OnIsApplyingSamplingRateChanged(bool value)
    {
        (ApplySamplingRateCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    private async Task ApplySamplingRateAsync()
    {
        // validazione IDENTICA alla tua ApplySamplingRateNow()
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

        // WARNING in inglese mentre scrive
        ShowBusyRequested?.Invoke(this, "Writing sampling rate to device…\nPlease wait.");

        try
        {
            // eseguo l’apply senza bloccare la UI
            await Task.Run(() => UpdateSamplingRateAndRestart(req));

            // conferma finale con OK (inglese, vale anche su Windows)
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
            // opzionale: se un domani vuoi traccia separata per la respirazione
            // dopo
            if (!dataPointsCollections.ContainsKey("ExgRespiration"))
            {
                dataPointsCollections["ExgRespiration"] = new List<float>();
                timeStampsCollections["ExgRespiration"] = new List<int>();
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

    public string ChartModeLabel
    {
        get
        {
            var clean = CleanParameterName(SelectedParameter);
            bool isExg = clean is "EXG" or "ECG" or "EMG" or "EXG Test" or "Respiration";

            return ChartDisplayMode switch
            {
                ChartDisplayMode.Single => "Single Parameter",
                ChartDisplayMode.Multi => isExg
                    ? "Multi Parameter (EXG1, EXG2)"
                    : "Multi Parameter (X, Y, Z)",
                ChartDisplayMode.Split => isExg
                    ? "Split (two separate charts)"
                    : "Split (three separate charts)",
                _ => "Single Parameter"
            };
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
        UpdateYAxisTextPropertiesOnly();

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
        // Single parameter selected
        else
        {
            var key = MapToInternalKey(cleanParam);

            // Se non ci sono dati per questo parametro (display→key interna), usa i default del "display"
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
            ValidationMessage = $"Impossibile applicare il sampling rate: {ex.Message}";
            ResetSamplingRateText();
        }
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


    private static double GetDefaultYAxisMin(string parameter)
    {
        return parameter switch
        {
            // === Gruppi (visualizzazione Multi: X,Y,Z) ===
            // Accelerometri (m/s²): range largo per vedere variazioni su tutte le componenti
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" => -20,
            // Giroscopio (deg/s)
            "Gyroscope" => -250,
            // Magnetometro (unità relative/local_flux*)
            "Magnetometer" => -5,

            // === Singole componenti/parametri ===
            // Accelerometri (m/s²)
            "Low-Noise AccelerometerX" => -5,
            "Low-Noise AccelerometerY" => -5,
            "Low-Noise AccelerometerZ" => -15,   // Z spesso include gravità: range più ampio
            "Wide-Range AccelerometerX" => -5,
            "Wide-Range AccelerometerY" => -5,
            "Wide-Range AccelerometerZ" => -15,

            // Giroscopio (deg/s)
            "GyroscopeX" => -250,
            "GyroscopeY" => -250,
            "GyroscopeZ" => -250,

            // Magnetometro (unità relative/local_flux*)
            "MagnetometerX" => -5,
            "MagnetometerY" => -5,
            "MagnetometerZ" => -5,

            // Sensori ambientali
            "Temperature_BMP180" => 15,  // °C
            "Pressure_BMP180" => 90,  // kPa

            // Batteria
            "BatteryVoltage" => 3.3,   // V
            "BatteryPercent" => 0,     // %

            // ADC esterni (V)
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 0,

            "ECG" or "EMG" or "EXG Test" => -15.0,
            "Respiration" => -15.0,


            // Fallback generico
            _ => 0
        };
    }



    private static double GetDefaultYAxisMax(string parameter)
    {
        return parameter switch
        {
            // === Gruppi (visualizzazione Multi: X,Y,Z) ===
            "Low-Noise Accelerometer" or "Wide-Range Accelerometer" => 20,  // m/s²
            "Gyroscope" => 250,                                             // deg/s
            "Magnetometer" => 5,                                            // unità relative/local_flux*

            // === Singole componenti/parametri ===
            // Accelerometri (m/s²)
            "Low-Noise AccelerometerX" => 5,
            "Low-Noise AccelerometerY" => 5,
            "Low-Noise AccelerometerZ" => 15,
            "Wide-Range AccelerometerX" => 5,
            "Wide-Range AccelerometerY" => 5,
            "Wide-Range AccelerometerZ" => 15,

            // Giroscopio (deg/s)
            "GyroscopeX" => 250,
            "GyroscopeY" => 250,
            "GyroscopeZ" => 250,

            // Magnetometro (unità relative/local_flux*)
            "MagnetometerX" => 5,
            "MagnetometerY" => 5,
            "MagnetometerZ" => 5,

            // Sensori ambientali
            "Temperature_BMP180" => 40,   // °C
            "Pressure_BMP180" => 110,  // kPa

            // Batteria
            "BatteryVoltage" => 4.2,   // V
            "BatteryPercent" => 100,   // %

            // ADC esterni (V)
            "ExtADC_A6" or "ExtADC_A7" or "ExtADC_A15" => 3.3,

            "ECG" or "EMG" or "EXG Test" => 15.0,
            "Respiration" => 15.0,



            // Fallback generico
            _ => 1
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

        // ===== EXG (gruppo + variante split EXG1·EXG2) =====
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


    public static string MapToInternalKey(string displayName)
    {
        var name = CleanParameterName(displayName);
        // Per EXG usiamo chiavi distinte per canale; i gruppi (ECG/EMG/EXG Test/Respiration)
        // non vengono mappati a un buffer singolo perché in Multi-chart usiamo i sotto-parametri.
        return name;
    }




    private static bool IsSplitVariantLabel(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return false;
        return displayName.Contains("separate charts", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("split", StringComparison.OrdinalIgnoreCase);
    }



    /// <summary>
    /// Determines if the specified parameter is a multi-parameter group,
    /// meaning it represents a sensor with multiple sub-components (e.g., X, Y, Z).
    /// </summary>
    /// <param name="parameter">The parameter or group name to check.</param>
    /// <returns>True if the parameter is a group (MultiChart); otherwise, false.</returns>
    private static bool IsMultiChart(string parameter)
    {
        // Clean the display name to get the actual parameter name (remove formatting)
        string cleanName = CleanParameterName(parameter);

        // Return true for sensor groups that support multi-line charting
              return cleanName is "Low-Noise Accelerometer" or "Wide-Range Accelerometer"
         or "Gyroscope" or "Magnetometer"
                                  // Trattiamo anche le modalità EXG come gruppi a 2 serie
        or "ECG" or "EMG" or "EXG Test" or "Respiration" or "EXG";


    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void LogExg(Dictionary<string, float> values, int tMs)
    {
        try
        {
            var has1 = values.TryGetValue("Exg1", out var v1);
            var has2 = values.TryGetValue("Exg2", out var v2);
                        if (has1 || has2)
            {
                string mode = exgModeRespiration ? "Respiration"
                                            : exgModeECG ? "ECG"
                                            : exgModeEMG ? "EMG"
                                            : exgModeTest ? "EXG Test"
                                            : "EXG";
                string s1 = has1 ? v1.ToString("F4") : "-";
                string s2 = has2 ? v2.ToString("F4") : "-";
                            }
                        else if (enableExg && sampleCounter % 50 == 0)
                            {
                System.Diagnostics.Debug.WriteLine("[EXG] nessun campo EXG trovato nel sample (Exg1/Exg2/ExgRespiration).");
                            }
        }
        catch { }
    }





    /// <summary>
    /// Returns the list of sub-parameters (typically X, Y, Z) for a given sensor group.
    /// If the group is not recognized, returns an empty list.
    /// </summary>
    /// <param name="groupParameter">The display name or group name selected by the user.</param>
    /// <returns>List of parameter names for the group (e.g., X/Y/Z axes).</returns>
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

    // === Legend helpers ===
    public static string GetLegendLabel(string groupParameter, string subParameter)
    {
        var group = CleanParameterName(groupParameter);

        // Nei gruppi EXG vogliamo "EXG1/EXG2" al posto di Exg1/Exg2
        if (group is "ECG" or "EMG" or "EXG Test" or "Respiration" or "EXG")
        {
            return subParameter switch
            {
                "Exg1" => "EXG1",
                "Exg2" => "EXG2",
                _ => subParameter
            };
        }

        // Per i gruppi IMU usa X/Y/Z
        if (subParameter.EndsWith("X")) return "X";
        if (subParameter.EndsWith("Y")) return "Y";
        if (subParameter.EndsWith("Z")) return "Z";

        // Parametri singoli: riusa il nome pulito
        return CleanParameterName(subParameter);
    }

    // Etichette leggibili per la legenda corrente (bindabile dalla view)
    public List<string> LegendLabels =>
        GetCurrentSubParameters().Select(p => GetLegendLabel(SelectedParameter, p)).ToList();

    // Etichette singole per la legenda (comode da bindare in XAML)
    public string Legend1Text => LegendLabels.ElementAtOrDefault(0) ?? "";
    public string Legend2Text => LegendLabels.ElementAtOrDefault(1) ?? "";
    public string Legend3Text => LegendLabels.ElementAtOrDefault(2) ?? "";

    // Colori coerenti con le serie disegnate.
    // Nota: se in Multi hai solo 2 serie, la seconda la faccio Blu (come nello screenshot)
    public Color Legend1Color => Colors.Red;
    public Color Legend2Color => (LegendLabels.Count == 2) ? Colors.Blue : Colors.Green;
    public Color Legend3Color => Colors.Blue;




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
    /// Returns the list of sub-parameters to be displayed for the currently selected parameter.
    /// If the parameter supports multiple sub-parameters (i.e., is a multi-chart),
    /// the method returns all sub-parameters; otherwise, it returns a list containing
    /// only the cleaned parameter name.
    /// </summary>
    /// <returns>
    /// A list of strings containing the sub-parameters for the current parameter selection.
    /// </returns>
    public List<string> GetCurrentSubParameters()
    {
        string cleanName = CleanParameterName(SelectedParameter);
        return IsMultiChart(cleanName) ? GetSubParameters(cleanName) : new List<string> { cleanName };
    }

    public string GetSplitParameterForCanvas(int slotIndex)
    {
        // slotIndex: 0 = primo riquadro (in XAML è canvasX), 1 = secondo (canvasY), 2 = terzo (canvasZ)
        var clean = CleanParameterName(SelectedParameter);

        if (ChartDisplayMode != ChartDisplayMode.Split)
            return clean; // non in split: ignora

        // Caso EXG: 2 soli canali
        if (IsExgSplit)
        {
            return slotIndex switch
            {
                0 => "Exg1",
                1 => "Exg2",
                _ => "" // il terzo grafico (Z) resta vuoto/nascosto
            };
        }

        // Caso IMU XYZ: 3 assi
        var sub = GetSubParameters(clean);
        return (slotIndex >= 0 && slotIndex < sub.Count) ? sub[slotIndex] : "";
    }

    private static bool TryGetNumeric(dynamic sample, string field, out float val)
    {
        val = 0f;
        try
        {
            var pi = sample?.GetType().GetProperty(field);
            if (pi == null) return false;
            var x = pi.GetValue(sample);
            if (x == null) return false;

            // Caso 1: già numero
            if (x is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
            { val = Convert.ToSingle(x); return true; }

            // Caso 2: wrapper con proprietà .Data
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
    /// Processes a single data sample from the Shimmer device, updates all enabled data collections
    /// with the new values, manages timestamps, trims collections to respect the time window,
    /// and updates the Y-axis range automatically if required.
    /// </summary>
    /// <param name="sample">The latest dynamic data sample from the Shimmer device.</param>
    /// <param name="currentTimeSeconds">The timestamp (in seconds) of the current sample.</param>
    private void UpdateDataCollectionsWithSingleSample(dynamic sample, double currentTimeSeconds)
    {
        var values = new Dictionary<string, float>();


        try
        {
            // ===== Low-noise Accelerometer =====
            if (enableLowNoiseAccelerometer)
            {
                if (HasProp(sample, "LowNoiseAccelerometerX") && sample.LowNoiseAccelerometerX != null)
                    values["Low-Noise AccelerometerX"] = (float)sample.LowNoiseAccelerometerX.Data;
                if (HasProp(sample, "LowNoiseAccelerometerY") && sample.LowNoiseAccelerometerY != null)
                    values["Low-Noise AccelerometerY"] = (float)sample.LowNoiseAccelerometerY.Data;
                if (HasProp(sample, "LowNoiseAccelerometerZ") && sample.LowNoiseAccelerometerZ != null)
                    values["Low-Noise AccelerometerZ"] = (float)sample.LowNoiseAccelerometerZ.Data;
            }

            // ===== Wide-range Accelerometer =====
            if (enableWideRangeAccelerometer)
            {
                if (HasProp(sample, "WideRangeAccelerometerX") && sample.WideRangeAccelerometerX != null)
                    values["Wide-Range AccelerometerX"] = (float)sample.WideRangeAccelerometerX.Data;
                if (HasProp(sample, "WideRangeAccelerometerY") && sample.WideRangeAccelerometerY != null)
                    values["Wide-Range AccelerometerY"] = (float)sample.WideRangeAccelerometerY.Data;
                if (HasProp(sample, "WideRangeAccelerometerZ") && sample.WideRangeAccelerometerZ != null)
                    values["Wide-Range AccelerometerZ"] = (float)sample.WideRangeAccelerometerZ.Data;
            }

            // ===== Gyroscope =====
            if (enableGyroscope)
            {
                if (HasProp(sample, "GyroscopeX") && sample.GyroscopeX != null)
                    values["GyroscopeX"] = (float)sample.GyroscopeX.Data;
                if (HasProp(sample, "GyroscopeY") && sample.GyroscopeY != null)
                    values["GyroscopeY"] = (float)sample.GyroscopeY.Data;
                if (HasProp(sample, "GyroscopeZ") && sample.GyroscopeZ != null)
                    values["GyroscopeZ"] = (float)sample.GyroscopeZ.Data;
            }

            // ===== Magnetometer =====
            if (enableMagnetometer)
            {
                if (HasProp(sample, "MagnetometerX") && sample.MagnetometerX != null)
                    values["MagnetometerX"] = (float)sample.MagnetometerX.Data;
                if (HasProp(sample, "MagnetometerY") && sample.MagnetometerY != null)
                    values["MagnetometerY"] = (float)sample.MagnetometerY.Data;
                if (HasProp(sample, "MagnetometerZ") && sample.MagnetometerZ != null)
                    values["MagnetometerZ"] = (float)sample.MagnetometerZ.Data;
            }

            // ===== Env (BMP180) =====
            if (enablePressureTemperature)
            {
                if (HasProp(sample, "Temperature_BMP180") && sample.Temperature_BMP180 != null)
                    values["Temperature_BMP180"] = (float)sample.Temperature_BMP180.Data;
                if (HasProp(sample, "Pressure_BMP180") && sample.Pressure_BMP180 != null)
                    values["Pressure_BMP180"] = (float)sample.Pressure_BMP180.Data;
            }

            // ===== Battery =====
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

            // ===== External ADC =====
            if (enableExtA6 && HasProp(sample, "ExtADC_A6") && sample.ExtADC_A6 != null)
                values["ExtADC_A6"] = (float)sample.ExtADC_A6.Data / 1000f;
            if (enableExtA7 && HasProp(sample, "ExtADC_A7") && sample.ExtADC_A7 != null)
                values["ExtADC_A7"] = (float)sample.ExtADC_A7.Data / 1000f;
            if (enableExtA15 && HasProp(sample, "ExtADC_A15") && sample.ExtADC_A15 != null)
                values["ExtADC_A15"] = (float)sample.ExtADC_A15.Data / 1000f;
            // ===== EXG: sempre due canali visibili (CH1/CH2) + opzionale respiration =====
            // ===== EXG: 2 canali + (opz.) respiration — alias per piattaforma =====
            if (enableExg)
            {
#if IOS || MACCATALYST
                // 🔒 iOS/Mac — tieni IDENTICHE le tue righe
                if (TryGetNumeric(sample, "Exg1", out float vExg1))
                    values["Exg1"] = vExg1;

                if (TryGetNumeric(sample, "Exg2", out float vExg2))
                    values["Exg2"] = vExg2;

                // opzionale, solo se usi davvero Respiration
                if (TryGetNumeric(sample, "ExgRespiration", out float vResp))
                    values["ExgRespiration"] = vResp;

#elif WINDOWS
                if (HasProp(sample, "Exg1") && sample.Exg1 != null)
                    values["Exg1"] = (float)sample.Exg1.Data;
                if (HasProp(sample, "Exg2") && sample.Exg2 != null)
                    values["Exg2"] = (float)sample.Exg2.Data;
                if (HasProp(sample, "ExgRespiration") && sample.ExgRespiration != null)
                    values["ExgRespiration"] = (float)sample.ExgRespiration.Data;

#elif ANDROID
                if (HasProp(sample, "Exg1") && sample.Exg1 != null)
                    values["Exg1"] = (float)sample.Exg1.Data;
                if (HasProp(sample, "Exg2") && sample.Exg2 != null)
                    values["Exg2"] = (float)sample.Exg2.Data;
                if (HasProp(sample, "ExgRespiration") && sample.ExgRespiration != null)
                    values["ExgRespiration"] = (float)sample.ExgRespiration.Data;

#endif
            }


        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing sample: {ex.Message}");
            return;
        }

        // ==== Store (thread-safe) ====
        lock (_dataLock)
        {
            int timestampMs = (int)Math.Round(currentTimeSeconds * 1000);
            int maxPoints = (int)(TimeWindowSeconds * DeviceSamplingRate);

            LogExg(values, timestampMs);

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

        // ==== Auto Y-axis ====
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


    private static bool HasProp(dynamic obj, string name)
    {
        try
        {
            var t = obj.GetType();
            return t.GetProperty(name) != null || t.GetField(name) != null; // <-- aggiungi il check dei field
        }
        catch { return false; }
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

        // Raise the ChartUpdateRequested event
        ChartUpdateRequested?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// Handles changes to the selected parameter, updating the chart display mode,
    /// Y-axis settings, label intervals, and triggers a chart update.
    /// This method is called automatically when the SelectedParameter property changes.
    /// </summary>
    /// <param name="value">The newly selected parameter name.</param>
    partial void OnSelectedParameterChanged(string value)
    {
        value ??= string.Empty;
        bool split = IsSplitVariantLabel(value);
        string cleanName = CleanParameterName(value);

        ChartDisplayMode = split
            ? ChartDisplayMode.Split
            : (IsMultiChart(cleanName) ? ChartDisplayMode.Multi : ChartDisplayMode.Single);

        OnPropertyChanged(nameof(ChartModeLabel));
        OnPropertyChanged(nameof(IsExgSplit)); // <-- AGGIUNGI QUESTA

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
    /// Sets the Y-axis label, unit, chart title, and min/max limits according to the selected parameter.
    /// This ensures the chart is correctly labeled and scaled for the currently displayed data.
    /// </summary>
    /// <param name="parameter">The name of the selected parameter.</param>
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

            // Environmental sensors
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

            // External ADC channels
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


    /// <summary>
    /// Attempts to convert a string to a valid double value.
    /// Handles partial user input (e.g. just "-" or "+") as temporarily valid,
    /// and accepts both '.' and ',' as decimal separators.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="result">
    /// When this method returns, contains the double value equivalent of the input string, if the conversion succeeded,
    /// or 0 if the conversion failed or input is a partial sign ("-" or "+").
    /// </param>
    /// <returns>
    /// True if the input is a valid double (or a temporarily valid partial input like "+" or "-"); otherwise, false.
    /// </returns>
    public static bool TryParseDouble(string input, out double result)
    {
        result = 0;

        // Reject null, empty, or whitespace input as invalid
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var cleanInput = input.Trim();
        if (string.IsNullOrEmpty(cleanInput))
            return false;

        // Special case: allow single "+" or "-" as a valid partial input during user editing
        if (cleanInput == "-" || cleanInput == "+")
        {
            result = 0;   // Temporary value
            return true;  // Considered valid for input scenarios
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

        // Try to parse using both InvariantCulture and CurrentCulture
        // to support both dot and comma as decimal separators
        return double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
               double.TryParse(cleanInput, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
    }

    private void UpdateYAxisTextPropertiesOnly()
    {
        _yAxisMinText = YAxisMin.ToString(CultureInfo.InvariantCulture);
        _yAxisMaxText = YAxisMax.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(YAxisMinText));
        OnPropertyChanged(nameof(YAxisMaxText));
    }

    /// <summary>
    /// Attempts to convert a string to a valid integer value.
    /// Accepts optional leading '+' or '-' signs and checks for valid numeric characters only.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="result">
    /// When this method returns, contains the integer value equivalent of the input string if the conversion succeeded, or 0 if the conversion failed.
    /// </param>
    /// <returns>True if the input is a valid integer; otherwise, false.</returns>
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


    /// <summary>
    /// Returns an object representing the current sensor configuration.
    /// Each property indicates whether a specific sensor is enabled.
    /// </summary>
    /// <returns>
    /// A <see cref="ShimmerDevice"/> object reflecting the currently selected sensor settings.
    /// </returns>
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


    /// <summary>
    /// Resets all timestamps in the timeStampsCollections for each parameter.
    /// The timestamps are set as if acquired regularly based on the current sampling rate,
    /// starting from zero and increasing by a fixed interval for each sample.
    /// This method is thread-safe.
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

