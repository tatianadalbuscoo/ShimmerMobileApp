using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI.IMU;

namespace ShimmerInterface.Views;

/// <summary>
/// LoadingPage handles the connection process to a Shimmer device.
/// Displays a loading spinner and updates the UI while attempting the connection.
/// </summary>
public partial class LoadingPage : ContentPage, INotifyPropertyChanged
{

    // Device configuration info
    private readonly ShimmerDevice device;

    // Flag to prevent multiple connection attempts
    private bool connectionInProgress;

    // Message shown while connecting
    private string connectingMessage;

    // For async result handling
    private readonly TaskCompletionSource<XR2Learn_ShimmerIMU> _completion;

    /// <summary>
    /// Property bound to the UI that displays the connection status.
    /// </summary>
    public string ConnectingMessage
    {
        get => connectingMessage;
        set
        {
            if (connectingMessage != value)
            {
                connectingMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Constructor that initializes the page and starts the connection message.
    /// </summary>
    /// <param name="device">The Shimmer device selected by the user.</param>
    /// <param name="completion">A task completion source used to return the result to the caller.</param>
    public LoadingPage(ShimmerDevice device, TaskCompletionSource<XR2Learn_ShimmerIMU> completion)
    {
        InitializeComponent();
        this.device = device;
        _completion = completion;

        // Set dynamic message with COM port info
        ConnectingMessage = $"Connecting to {device.ShimmerName} on {device.Port1}...";
        BindingContext = this;
    }

    /// <summary>
    /// Called when the page becomes visible. Attempts to connect to the Shimmer device.
    /// Displays a success or failure popup, then closes the page.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Small delay to ensure UI renders before connection starts
        await Task.Delay(500);

        if (connectionInProgress) return;
        connectionInProgress = true;

        XR2Learn_ShimmerIMU? connectedShimmer = null;

        try
        {
            // Create and configure Shimmer object with selected sensor flags
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

            shimmer.Configure("Shimmer", device.Port1,
                device.EnableLowNoiseAccelerometer,
                device.EnableWideRangeAccelerometer,
                device.EnableGyroscope,
                device.EnableMagnetometer,
                device.EnablePressureTemperature,
                device.EnableBattery,
                device.EnableExtA6,
                device.EnableExtA7,
                device.EnableExtA15);

            shimmer.Connect();

            if (shimmer.IsConnected())
            {
                shimmer.StartStreaming();
                connectedShimmer = shimmer;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SHIMMER ERROR] on {device.Port1}: {ex.Message}");
        }

        // Mostra prima il popup e ATTENDI che venga chiuso dall'utente
        await DisplayAlert(
            connectedShimmer != null ? "Success" : "Connection Failed",
            connectedShimmer != null ? $"{device.DisplayName} connected on {device.Port1}"
                                     : $"Could not connect to {device.DisplayName}.",
            "OK");

        // Solo dopo che il popup è stato chiuso dall'utente, chiudi la LoadingPage
        _completion.SetResult(connectedShimmer);
    }


    public new event PropertyChangedEventHandler PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}