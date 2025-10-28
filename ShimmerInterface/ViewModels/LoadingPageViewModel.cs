/*
 * LoadingPageViewModel — MAUI MVVM
 * Orchestrates the async connection flow to a Shimmer device (EXG or IMU) on Windows/Android.
 * Exposes observable UI state (spinner text, is-connecting, alert title/message/visibility)
 * and commands to start the connection and dismiss alerts.
 * Uses a 30s timeout inside ConnectAsync, starts streaming on success,
 * and returns the connected instance to the caller via a TaskCompletionSource<object?>.
 * Note: iOS/macOS follow the WebSocket bridge path; this ViewModel is not used there.
 */


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerSDK.IMU;
using ShimmerInterface.Models;
using System.Diagnostics;


#if WINDOWS || ANDROID
using ShimmerSDK.EXG;
#endif


namespace ShimmerInterface.ViewModels;


/// <summary>
/// ViewModel for the LoadingPage.
/// Handles the asynchronous connection process to a Shimmer device,
/// exposing UI-bound properties and commands following the MVVM pattern.
/// </summary>
public partial class LoadingPageViewModel : ObservableObject
{

    // Selected Shimmer device, immutable reference used to configure and open the connection.
    private readonly ShimmerDevice device;

    // Completion source that returns the connected device instance (either ShimmerSDK_IMU or ShimmerSDK_EXG) to the caller.
    private readonly TaskCompletionSource<object?> completion;

    // TaskCompletionSource used internally to wait for the alert dialog to be dismissed by the user.
    private TaskCompletionSource<bool> alertCompletionSource = new TaskCompletionSource<bool>();

    // Message displayed on the UI during the connection process
    [ObservableProperty]
    private string connectingMessage = string.Empty;

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

#elif ANDROID

        this.connectingMessage = $"Connecting to {device.ShimmerName} [{device.Port1}] via Bluetooth...";

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
    /// Opens a Shimmer connection: uses EXG when enabled, otherwise IMU.
    /// Configures platform-specific settings, attempts a 30s connection, starts streaming,
    /// and returns the connected instance.
    /// </summary>
    /// <returns>
    /// The connected device instance (<see cref="ShimmerSDK.EXG.ShimmerSDK_EXG"/> or
    /// <see cref="ShimmerSDK.IMU.ShimmerSDK_IMU"/>) boxed as <see cref="object"/>; 
    /// <c>null</c> on timeout or failure.
    /// </returns>
    private async Task<object?> ConnectAsync()
    {
        try
        {

            // ----- EXG path -----

#if WINDOWS

            if (device.IsExg && device.EnableExg)
            {

                // Configure EXG
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

                    // Always true
                    EnableExg = true,
                    ExgMode = device.ExgModeEnum
                };

                // Windows configuration 
                exg.ConfigureWindows(
                    "Shimmer3",
                    device.Port1,
                    device.EnableLowNoiseAccelerometer,
                    device.EnableWideRangeAccelerometer,
                    device.EnableGyroscope,
                    device.EnableMagnetometer,
                    device.EnablePressureTemperature,
                    device.EnableBattery,
                    device.EnableExtA6,
                    device.EnableExtA7,
                    device.EnableExtA15,
                    enableExg: true,
                    exgMode: device.ExgModeEnum
                );

                // Connect with 30s timeout
                var connectTaskExg = Task.Run(() => exg.Connect());
                var completedExg = await Task.WhenAny(connectTaskExg, Task.Delay(30000));
                if (completedExg != connectTaskExg) return null;    // timeout
                if (connectTaskExg.IsFaulted) return null;          // exception during connect
                if (!exg.IsConnected()) return null;                // not connected

                // start data stream
                exg.StartStreaming();

                // return connected EXG
                return exg; 

            }

#elif ANDROID

            if (device.IsExg && device.EnableExg)
            {

                // Pick EXG mode from model flags
                var exgModeSel =
                    device.IsExgModeRespiration ? ExgMode.Respiration :
                    device.IsExgModeECG        ? ExgMode.ECG :
                    device.IsExgModeEMG        ? ExgMode.EMG :
                    ExgMode.ECG; // default

                // Configure EXG
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

                // Android configuration 
                exg.ConfigureAndroid(
                    "Shimmer3",
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

                // Connect with 30s timeout
                var connectTaskExg = Task.Run(() => exg.Connect());
                var completedExg = await Task.WhenAny(connectTaskExg, Task.Delay(30000));
                if (completedExg != connectTaskExg) return null;    // timeout
                if (connectTaskExg.IsFaulted) return null;          // exception during connect
                if (!exg.IsConnected()) return null;                // not connected

                // start data stream
                exg.StartStreaming();

                // return connected EXG
                return exg;

            }

#endif
            // ----- IMU path -----


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

            // Windows IMU configuration
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

            // Android IMU configuration
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

            // Connect with 30s timeout
            var connectTask = Task.Run(() => imu.Connect());
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(30000));

            if (completedTask != connectTask) return null;      // timeout
            if (connectTask.IsFaulted) return null;             // exception during connect
            if (!imu.IsConnected()) return null;                // not connected

            // start data stream
            imu.StartStreaming();

            // return connected IMU
            return imu;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ConnectAsync error: {ex.Message}");
            return null;
        }
    }
}
