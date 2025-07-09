using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI;
namespace ShimmerInterface.Views;
public partial class LoadingPage : ContentPage
{
    private readonly SensorConfiguration config;
    public LoadingPage(SensorConfiguration selected)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        config = selected;

    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(500);
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
            await DisplayAlert("OK", "Dispositivo connesso con successo", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlert("Errore", "Connessione fallita", "OK");
            await Navigation.PopAsync();
        }
    }
}