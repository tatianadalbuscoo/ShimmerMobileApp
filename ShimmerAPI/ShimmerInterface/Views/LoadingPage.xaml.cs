using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage, INotifyPropertyChanged
{
    private readonly ShimmerDevice device;
    private bool connectionInProgress;
    private string connectingMessage;

    private readonly TaskCompletionSource<bool> _completion;

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

    public LoadingPage(ShimmerDevice device, TaskCompletionSource<bool> completion)
    {
        InitializeComponent();
        this.device = device;
        _completion = completion;
        ConnectingMessage = $"Connecting to {device.Port1} / {device.Port2}...";
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(500);

        if (connectionInProgress) return;
        connectionInProgress = true;

        bool connected = false;
        string? usedPort = null;

        foreach (var port in new[] { device.Port1, device.Port2 })
        {
            try
            {
                var shimmer = new XR2Learn_ShimmerGSR
                {
                    EnableAccelerator = device.EnableAccelerometer,
                    EnableGSR = device.EnableGSR,
                    EnablePPG = device.EnablePPG
                };

                shimmer.Configure("Shimmer", port);
                shimmer.Connect();

                if (shimmer.IsConnected())
                {
                    shimmer.StartStreaming();
                    connected = true;
                    usedPort = port;
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SHIMMER ERROR] on {port}: {ex.Message}");
            }
        }

        await DisplayAlert(
            connected ? "Success" : "Connection Failed",
            connected ? $"{device.DisplayName} connected on {usedPort}"
                      : $"Could not connect to {device.DisplayName}.",
            "OK");

        _completion.SetResult(connected);
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
