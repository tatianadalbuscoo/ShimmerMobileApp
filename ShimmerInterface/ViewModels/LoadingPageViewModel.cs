using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerSDK.IMU;
using ShimmerInterface.Models;
using System.Diagnostics;


#if WINDOWS || ANDROID
using ShimmerSDK.EXG; // ← wrapper EXG
#endif


namespace ShimmerInterface.ViewModels;

/// <summary>
/// ViewModel for the LoadingPage.
/// Handles the asynchronous connection process to a Shimmer device,
/// exposing UI-bound properties and commands following the MVVM pattern.
/// </summary>
public partial class LoadingPageViewModel : ObservableObject
{
    private readonly ShimmerDevice device;

    // Generalizzato: può restituire XR2Learn_ShimmerIMU o XR2Learn_ShimmerEXG
    private readonly TaskCompletionSource<object?> completion;

    // Message displayed on the UI during the connection process
    [ObservableProperty]
    private string connectingMessage;

    // Indicates whether a connection attempt is currently in progress
    [ObservableProperty]
    private bool isConnecting;

    // Title of the alert dialog to be shown after connection attempt
    [ObservableProperty]
    private string alertTitle = "";

    // Message body of the alert dialog shown to the user.
    [ObservableProperty]
    private string alertMessage = "";

    // Flag that indicates whether an alert should be shown on the UI.
    [ObservableProperty]
    private bool showAlert;

    /// <summary>
    /// Constructs the ViewModel and initializes the connection message.
    /// </summary>
    /// <param name="device">The Shimmer device to be connected.</param>
    /// <param name="completion">A TaskCompletionSource to return the connection result asynchronously.</param>
    public LoadingPageViewModel(ShimmerDevice device, TaskCompletionSource<object?> completion)
    {
        this.device = device;
        this.completion = completion;

#if WINDOWS
        this.connectingMessage = $"Connecting to {device.ShimmerName} on {device.Port1}...";
#elif MACCATALYST
        this.connectingMessage = $"Connecting to {device.ShimmerName} via BLE...";
#elif ANDROID
        this.connectingMessage = $"Connecting to {device.ShimmerName} [{device.Port1}] via Bluetooth...";
#else
        this.connectingMessage = $"Connecting to {device.ShimmerName}...";
#endif
    }

    /// <summary>
    /// Command that initiates the asynchronous connection to the Shimmer device.
    /// Displays a loading spinner, handles timeout, and notifies the view when the result is ready.
    /// </summary>
    [RelayCommand]
    public async Task StartConnectionAsync()
    {
        if (IsConnecting) return;

        IsConnecting = true;

        // Small delay to ensure the UI is fully rendered
        await Task.Delay(500);

        var dev = await ConnectAsync();

        // Prepare alert content based on the result
        if (dev != null)
        {
            AlertTitle = "Success";
            AlertMessage = $"Connected to {device.ShimmerName} on {device.Port1}";
        }
        else
        {
            AlertTitle = "Connection Failed";
            AlertMessage = $"Could not connect to {device.ShimmerName} on {device.Port1}";
        }

        // Trigger alert dialog on the UI
        ShowAlert = true;

        // Wait for the user to dismiss the alert (Click on OK)
        alertCompletionSource = new TaskCompletionSource<bool>();
        await alertCompletionSource.Task;

        // Return result to caller
        completion.SetResult(dev);

        IsConnecting = false;
    }

    /// <summary>
    /// TaskCompletionSource used internally to wait for the alert dialog to be dismissed by the user.
    /// </summary>
    private TaskCompletionSource<bool> alertCompletionSource = new TaskCompletionSource<bool>();

    /// <summary>
    /// Command invoked when the alert is dismissed by the user.
    /// Resumes the execution of the connection process.
    /// </summary>
    [RelayCommand]
    public void DismissAlert()
    {
        ShowAlert = false;
        alertCompletionSource?.SetResult(true);
    }

    /// <summary>
    /// Attempts to connect to the Shimmer device asynchronously.
    /// On Windows: se la board è EXG e l’utente ha abilitato EXG, usa XR2Learn_ShimmerEXG; altrimenti IMU.
    /// </summary>
    private async Task<object?> ConnectAsync()
    {
        try
        {
            // ===== Branch EXG (solo Windows) =====
#if WINDOWS
            if (device.IsExg && device.EnableExg)
            {
                var exg = new ShimmerSDK_EXG
                {
                    // IMU flags (puoi tenerli accesi: il wrapper EXG li gestisce comunque)
                    EnableLowNoiseAccelerometer = device.EnableLowNoiseAccelerometer,
                    EnableWideRangeAccelerometer = device.EnableWideRangeAccelerometer,
                    EnableGyroscope = device.EnableGyroscope,
                    EnableMagnetometer = device.EnableMagnetometer,
                    EnablePressureTemperature = device.EnablePressureTemperature,
                    EnableBatteryVoltage = device.EnableBattery,
                    EnableExtA6 = device.EnableExtA6,
                    EnableExtA7 = device.EnableExtA7,
                    EnableExtA15 = device.EnableExtA15,

                    // EXG flags specifici
                    EnableExg = true,
                    ExgMode = device.ExgModeEnum // mapping dal model
                };

                // Config Windows (stessa firma dell’IMU + EXG attivo nel device)
                exg.ConfigureWindows(
                    "Shimmer3",
                    device.Port1,
                    device.EnableLowNoiseAccelerometer,
                    device.EnableWideRangeAccelerometer,
                    device.EnableGyroscope,
                    device.EnableMagnetometer,
                    device.EnablePressureTemperature,
                    device.EnableBattery,   // bool per battery voltage nella firma EXG
                    device.EnableExtA6,
                    device.EnableExtA7,
                    device.EnableExtA15,
                    enableExg: true,
                    exgMode: device.ExgModeEnum
                );


                var connectTaskExg = Task.Run(() => exg.Connect());
                var completedExg = await Task.WhenAny(connectTaskExg, Task.Delay(30000));
                if (completedExg != connectTaskExg) return null; // timeout
                if (connectTaskExg.IsFaulted) return null;
                if (!exg.IsConnected()) return null;

                exg.StartStreaming();
                return exg; // ← può essere usato come dynamic nella DataPage
            }
#elif ANDROID
    if (device.IsExg && device.EnableExg)
    {
        var exgModeSel =
            device.IsExgModeRespiration ? ExgMode.Respiration :
            device.IsExgModeECG        ? ExgMode.ECG :
            device.IsExgModeEMG        ? ExgMode.EMG :
            ExgMode.ECG; // default

        var exg = new ShimmerSDK_EXG
        {
            EnableLowNoiseAccelerometer = device.EnableLowNoiseAccelerometer,
            EnableWideRangeAccelerometer = device.EnableWideRangeAccelerometer,
            EnableGyroscope = device.EnableGyroscope,
            EnableMagnetometer = device.EnableMagnetometer,
            EnablePressureTemperature = device.EnablePressureTemperature,
            EnableBatteryVoltage = device.EnableBattery,
            EnableExtA6 = device.EnableExtA6,
            EnableExtA7 = device.EnableExtA7,
            EnableExtA15 = device.EnableExtA15,

            EnableExg = true,
            ExgMode   = exgModeSel
        };

        // ⬇️ NIENTE named args: tutti posizionali
        exg.ConfigureAndroid(
            "Shimmer3",                    // deviceId / name (come per IMU)
            device.Port1,                  // MAC
            device.EnableLowNoiseAccelerometer,
            device.EnableWideRangeAccelerometer,
            device.EnableGyroscope,
            device.EnableMagnetometer,
            device.EnablePressureTemperature,
            device.EnableBattery,
            device.EnableExtA6,
            device.EnableExtA7,
            device.EnableExtA15,
            true,                          // enableExg
            exgModeSel                     // ExgMode
        );

        var connectTaskExg = Task.Run(() => exg.Connect());
        var completedExg = await Task.WhenAny(connectTaskExg, Task.Delay(30000));
        if (completedExg != connectTaskExg) return null;
        if (connectTaskExg.IsFaulted) return null;
        if (!exg.IsConnected()) return null;

        exg.StartStreaming();
        return exg;
    }

#endif

            // ===== Branch IMU (default o piattaforme non-Windows) =====

            var imu = new ShimmerSDK_IMU
            {
                EnableLowNoiseAccelerometer = device.EnableLowNoiseAccelerometer,
                EnableWideRangeAccelerometer = device.EnableWideRangeAccelerometer,
                EnableGyroscope = device.EnableGyroscope,
                EnableMagnetometer = device.EnableMagnetometer,
                EnablePressureTemperature = device.EnablePressureTemperature,
                EnableBattery = device.EnableBattery,
                EnableExtA6 = device.EnableExtA6,
                EnableExtA7 = device.EnableExtA7,
                EnableExtA15 = device.EnableExtA15
            };

#if WINDOWS
            imu.ConfigureWindows(
                "Shimmer3", device.Port1,
                device.EnableLowNoiseAccelerometer,
                device.EnableWideRangeAccelerometer,
                device.EnableGyroscope,
                device.EnableMagnetometer,
                device.EnablePressureTemperature,
                device.EnableBattery,
                device.EnableExtA6,
                device.EnableExtA7,
                device.EnableExtA15
            );
#elif ANDROID
            imu.ConfigureAndroid(
                "Shimmer3", device.Port1,
                device.EnableLowNoiseAccelerometer,
                device.EnableWideRangeAccelerometer,
                device.EnableGyroscope,
                device.EnableMagnetometer,
                device.EnablePressureTemperature,
                device.EnableBattery,
                device.EnableExtA6,
                device.EnableExtA7,
                device.EnableExtA15
            );
#endif

            var connectTask = Task.Run(() => imu.Connect());
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(30000));

            if (completedTask != connectTask) return null; // timeout
            if (connectTask.IsFaulted) return null;
            if (!imu.IsConnected()) return null;

            imu.StartStreaming();
            return imu;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ConnectAsync error: {ex.Message}");
            return null;
        }
    }
}
