using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    public ObservableCollection<SensorConfiguration> AvailableDevices { get; } = new();

    public IRelayCommand<INavigation> ConnectCommand { get; }

    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        LoadDevices();
    }

    private void LoadDevices()
    {
        AvailableDevices.Clear();
        var ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();

        foreach (var port in ports)
        {
            AvailableDevices.Add(new SensorConfiguration
            {
                PortName = port,
                IsSelected = false
            });
        }
    }

    private async Task Connect(INavigation nav)
    {
        var selectedDevice = AvailableDevices.FirstOrDefault(d => d.IsSelected);

        if (selectedDevice == null)
        {
            await App.Current.MainPage.DisplayAlert("Errore", "Seleziona un dispositivo Shimmer", "OK");
            return;
        }

        await nav.PushAsync(new LoadingPage(selectedDevice));
    }
}
