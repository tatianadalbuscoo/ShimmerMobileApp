using ShimmerInterface.Views;
using ShimmerInterface.ViewModels;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage
{
    private readonly LoadingPageViewModel viewModel;

    public LoadingPage(bool enableAccelerometer, bool enableGSR, bool enablePPG)
    {
        InitializeComponent();
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
            await Navigation.PushAsync(new DataPage(shimmer));
            Navigation.RemovePage(this);
        }
        else
        {
            await DisplayAlert("Error", "Connession failed", "OK");
            await Navigation.PopAsync();
        }
    }
}
