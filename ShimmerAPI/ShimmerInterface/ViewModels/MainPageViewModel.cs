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
        // Ensure collection is completely cleared
        while (AvailableDevices.Count > 0)
        {
            AvailableDevices.RemoveAt(0);
        }

        var ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();

        // Filter out COM3 by default and remove duplicates
        var filteredPorts = ports
            .Where(port => !port.Equals("COM3", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        foreach (var port in filteredPorts)
        {
            // Double-check that we're not adding duplicates
            if (!AvailableDevices.Any(d => d.PortName == port))
            {
                AvailableDevices.Add(new SensorConfiguration
                {
                    PortName = port,
                    IsSelected = false
                });
            }
        }
    }

    private async Task Connect(INavigation nav)
    {
        var selectedDevice = AvailableDevices.FirstOrDefault(d => d.IsSelected);
        if (selectedDevice == null)
        {
            await App.Current.MainPage.DisplayAlert("Error", "Please select a Shimmer device", "OK");
            return;
        }

        await nav.PushAsync(new LoadingPage(selectedDevice));
    }
}