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

    // Lista per tenere traccia degli Shimmer connessi
    private List<XR2Learn_ShimmerGSR> connectedShimmers = new();

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

        connectedShimmers.Clear();

        foreach (var device in selectedDevices)
        {
            var tcs = new TaskCompletionSource<XR2Learn_ShimmerGSR>();
            var loadingPage = new LoadingPage(device, tcs);

            await Application.Current.MainPage.Navigation.PushModalAsync(loadingPage);
            var shimmer = await tcs.Task;
            await Application.Current.MainPage.Navigation.PopModalAsync();

            if (shimmer != null)
            {
                connectedShimmers.Add(shimmer);
            }
        }

        // Dopo aver connesso tutti i dispositivi, crea la TabbedPage
        if (connectedShimmers.Any())
        {
            CreateTabbedPage();
        }
    }

    private void CreateTabbedPage()
    {
        var tabbedPage = new TabbedPage();

        foreach (var shimmer in connectedShimmers)
        {
            var sensorConfig = new SensorConfiguration
            {
                // Configura con i valori del dispositivo
                EnableAccelerometer = true,
                EnableGSR = true,
                EnablePPG = true
            };

            var dataPage = new DataPage(shimmer, sensorConfig);
            dataPage.Title = $"Shimmer {connectedShimmers.IndexOf(shimmer) + 1}";

            tabbedPage.Children.Add(dataPage);
        }

        Application.Current.MainPage = tabbedPage;
    }
}