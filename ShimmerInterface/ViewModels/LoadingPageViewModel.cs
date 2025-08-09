using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XR2Learn_ShimmerAPI.IMU;
using ShimmerInterface.Models;
using System.Diagnostics;

namespace ShimmerInterface.ViewModels;

/// <summary>
/// ViewModel for the LoadingPage.
/// Handles the asynchronous connection process to a Shimmer device,
/// exposing UI-bound properties and commands following the MVVM pattern.
/// </summary>
public partial class LoadingPageViewModel : ObservableObject
{
    private readonly ShimmerDevice device;
    private readonly TaskCompletionSource<XR2Learn_ShimmerIMU?> completion;

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
    public LoadingPageViewModel(ShimmerDevice device, TaskCompletionSource<XR2Learn_ShimmerIMU?> completion)
    {
        this.device = device;
        this.completion = completion;

#if WINDOWS
    this.connectingMessage = $"Connecting to {device.ShimmerName} on {device.Port1}...";
#elif MACCATALYST
    this.connectingMessage = $"Connecting to {device.ShimmerName} via BLE...";
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

        var shimmer = await ConnectAsync();

        // Prepare alert content based on the result
        if (shimmer != null)
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

        // If the connection was successful, complete the external Task with the shimmer device
        if (shimmer != null)
        {
            completion.SetResult(shimmer);
        }
        else
        {
            completion.SetResult(null);
        }

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
    /// Attempts to establish a connection with the Shimmer device.
    /// Includes a timeout mechanism (30 seconds) to prevent indefinite blocking.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="XR2Learn_ShimmerIMU"/> if the connection is successful; otherwise, null.
    /// </returns>
    /// <summary>
    /// Attempts to connect to the Shimmer device asynchronously. 
    /// Returns the initialized device if successful, otherwise null.
    /// Handles connection timeouts and any exceptions safely, 
    /// ensuring the application does not crash on connection failure.
    /// </summary>
    /// <returns>The connected <see cref="XR2Learn_ShimmerIMU"/> instance if successful; otherwise, null.</returns>
    private async Task<XR2Learn_ShimmerIMU?> ConnectAsync()
    {
        try
        {
            var shimmer = new XR2Learn_ShimmerIMU
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

            // ⚠️ Qui l’unica differenza tra Windows e Mac:
            // - Windows: Port1 = COMxx
            // - Mac:     Port1 = BLE name hint (es. "Shimmer3")
#if WINDOWS
        shimmer.Configure("Shimmer3", device.Port1,
#else
            shimmer.Configure("Shimmer3", device.Port1,   // device.Port1 = hint nome BLE su Mac
#endif
                device.EnableLowNoiseAccelerometer,
                device.EnableWideRangeAccelerometer,
                device.EnableGyroscope,
                device.EnableMagnetometer,
                device.EnablePressureTemperature,
                device.EnableBattery,
                device.EnableExtA6,
                device.EnableExtA7,
                device.EnableExtA15);

            var connectTask = Task.Run(() => shimmer.Connect());
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(30000));

            if (completedTask != connectTask) return null;       // timeout
            if (connectTask.IsFaulted) return null;
            if (!shimmer.IsConnected()) return null;

            shimmer.StartStreaming();
            return shimmer;
        }
        catch
        {
            return null;
        }
    }

}

