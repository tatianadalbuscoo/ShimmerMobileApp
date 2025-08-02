using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI;
using XR2Learn_ShimmerAPI.IMU;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage, INotifyPropertyChanged
{
    private readonly ShimmerDevice device;
    private bool connectionInProgress;
    private string connectingMessage;
    private readonly TaskCompletionSource<XR2Learn_ShimmerIMU> _completion;

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

    public LoadingPage(ShimmerDevice device, TaskCompletionSource<XR2Learn_ShimmerIMU> completion)
    {
        InitializeComponent();
        this.device = device;
        _completion = completion;
        ConnectingMessage = $"Connecting to {device.ShimmerName} on {device.Port1}...";
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(500);

        if (connectionInProgress) return;
        connectionInProgress = true;

        XR2Learn_ShimmerIMU? connectedShimmer = null;

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