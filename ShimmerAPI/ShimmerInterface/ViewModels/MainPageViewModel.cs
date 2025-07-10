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

    // Costruttore: inizializza il comando di connessione e carica i dispositivi disponibili
    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        LoadDevices();
    }

    // Carica e accoppia le porte seriali disponibili in coppie (COMx, COMy) per ogni Shimmer
    private void LoadDevices()
    {
        AvailableDevices.Clear();
        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        // Accoppia le porte seriali due a due (es. COM3 + COM4), assumendo che ogni dispositivo Shimmer appaia con due porte
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

    // Connette tutti i dispositivi selezionati mostrando una schermata di caricamento per ciascuno
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

    // Crea una TabbedPage in cui ogni scheda rappresenta un dispositivo Shimmer connesso
    // Crea una nuova interfaccia a schede (TabbedPage) dove ogni tab rappresenta un dispositivo Shimmer connesso.
    // Per ciascun dispositivo:
    // - Trova la configurazione selezionata dall’utente (accelerometro, GSR, PPG)
    // - Crea una pagina DataPage per visualizzare i dati del sensore
    // - Imposta un titolo personalizzato (es. "Shimmer 1", "Shimmer 2"...)
    // Infine, imposta la TabbedPage come nuova MainPage per mostrare subito i grafici live.

    private void CreateTabbedPage()
    {
        var tabbedPage = new TabbedPage();

        foreach (var shimmer in connectedShimmers)
        {
            // Trova il device associato allo shimmer
            var device = AvailableDevices.FirstOrDefault(d =>
                d.EnableAccelerometer == shimmer.EnableAccelerator &&
                d.EnableGSR == shimmer.EnableGSR &&
                d.EnablePPG == shimmer.EnablePPG);

            var sensorConfig = new SensorConfiguration
            {
                EnableAccelerometer = device?.EnableAccelerometer ?? true,
                EnableGSR = device?.EnableGSR ?? true,
                EnablePPG = device?.EnablePPG ?? true
            };

            var dataPage = new DataPage(shimmer, sensorConfig);
            dataPage.Title = $"Shimmer {connectedShimmers.IndexOf(shimmer) + 1}";

            tabbedPage.Children.Add(dataPage);
        }

        Application.Current.MainPage = tabbedPage;
    }

}