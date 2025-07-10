using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    public ObservableCollection<ShimmerDevice> AvailableDevices { get; } = new();
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
            .OrderBy(p => p)
            .ToList();

        for (int i = 0; i < ports.Count - 1; i += 2)
        {
            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = $"Shimmer Device {i / 2 + 1}",
                Port1 = ports[i],
                Port2 = ports[i + 1],
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
            var loadingPage = new LoadingPage(device);
            await nav.PushAsync(loadingPage);

            // Aspetta che la connessione finisca
            if (loadingPage.BindingContext is not null)
            {
                await loadingPage.ConnectionTask;
            }

            // oppure piccolo delay per sicurezza tra le connessioni
            await Task.Delay(200);
        }
    }

}
