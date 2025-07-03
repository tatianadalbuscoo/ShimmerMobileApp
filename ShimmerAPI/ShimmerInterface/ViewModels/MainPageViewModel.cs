using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Views;

namespace ShimmerInterface.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool enableAccelerometer = true;

    [ObservableProperty]
    private bool enableGSR = true;

    [ObservableProperty]
    private bool enablePPG = true;

    public IRelayCommand<INavigation> ConnectCommand { get; }

    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
    }

    private async Task Connect(INavigation nav)
    {
        // Naviga a LoadingPage con i sensori selezionati
        await nav.PushAsync(new LoadingPage(EnableAccelerometer, EnableGSR, EnablePPG));
    }
}
