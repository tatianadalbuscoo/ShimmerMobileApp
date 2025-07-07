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

    // Metodo per ottenere la configurazione dei sensori
    public SensorConfiguration GetSensorConfiguration()
    {
        return new SensorConfiguration
        {
            EnableAccelerometer = EnableAccelerometer,
            EnableGSR = EnableGSR,
            EnablePPG = EnablePPG
        };
    }
}

// Classe helper per trasferire la configurazione dei sensori
public class SensorConfiguration
{
    public bool EnableAccelerometer { get; set; }
    public bool EnableGSR { get; set; }
    public bool EnablePPG { get; set; }
}