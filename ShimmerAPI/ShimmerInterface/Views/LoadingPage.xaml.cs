using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage, INotifyPropertyChanged
{
    private readonly SensorConfiguration config;
    private bool connectionInProgress;
    private string connectingMessage;

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

    public LoadingPage(SensorConfiguration selected)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);

        config = selected;
        ConnectingMessage = $"Connecting to {config.PortName}...";
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(500);
        if (connectionInProgress) return;
        connectionInProgress = true;

        bool connected = false;

        await Task.Factory.StartNew(() =>
        {
            try
            {
                var shimmer = new XR2Learn_ShimmerGSR
                {
                    EnableAccelerator = config.EnableAccelerometer,
                    EnableGSR = config.EnableGSR,
                    EnablePPG = config.EnablePPG
                };

                shimmer.Configure("Shimmer", config.PortName);
                shimmer.Connect();

                if (shimmer.IsConnected())
                {
                    shimmer.StartStreaming();
                    connected = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SHIMMER ERROR] " + ex.Message);
            }
        }, TaskCreationOptions.LongRunning);

        await DisplayAlert(
            connected ? "Success" : "Connection Failed",
            connected ? $"Device on {config.PortName} connected successfully"
                      : $"Could not connect to {config.PortName}.",
            "OK"
        );

        connectionInProgress = false;
        await Navigation.PopAsync();
    }

    public new event PropertyChangedEventHandler PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
