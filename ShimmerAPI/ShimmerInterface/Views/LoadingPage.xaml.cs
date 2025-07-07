using ShimmerInterface.Views;
using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage
{
    private readonly LoadingPageViewModel viewModel;
    private readonly SensorConfiguration config;

    public LoadingPage(bool enableAccelerometer, bool enableGSR, bool enablePPG)
    {
        InitializeComponent();

        config = new SensorConfiguration
        {
            EnableAccelerometer = enableAccelerometer,
            EnableGSR = enableGSR,
            EnablePPG = enablePPG
        };

        viewModel = new LoadingPageViewModel(enableAccelerometer, enableGSR, enablePPG);
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Delay(500); // mostra loading

        var shimmer = await viewModel.ConnectAsync();
        if (shimmer != null)
        {
            await Navigation.PushAsync(new DataPage(shimmer, config)); // 👈 passa anche la configurazione
            Navigation.RemovePage(this);
        }
        else
        {
            await DisplayAlert("Error", "Connection failed", "OK");
            await Navigation.PopAsync();
        }
    }
}
