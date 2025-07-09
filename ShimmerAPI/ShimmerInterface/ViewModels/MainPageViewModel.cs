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

        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .Distinct()
            .ToList();

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
        var selectedDevices = AvailableDevices.Where(d => d.IsSelected).ToList();

        if (!selectedDevices.Any())
        {
            await App.Current.MainPage.DisplayAlert("Error", "Please select at least one Shimmer device", "OK");
            return;
        }

        foreach (var device in selectedDevices)
        {
            await nav.PushAsync(new LoadingPage(device));
            // Aspetta che l'utente torni alla MainPage per passare al successivo
        }
    }
}
