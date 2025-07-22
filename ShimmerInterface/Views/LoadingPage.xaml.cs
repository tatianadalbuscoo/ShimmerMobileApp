using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShimmerInterface.Models;
using XR2Learn_ShimmerAPI;
using XR2Learn_ShimmerAPI.IMU;

namespace ShimmerInterface.Views;

public partial class LoadingPage : ContentPage, INotifyPropertyChanged
{
    private readonly ShimmerDevice device;
    private bool connectionInProgress;
    private string connectingMessage;
    private readonly TaskCompletionSource<XR2Learn_ShimmerIMU> _completion;

    public string ConnectingMessage
    {
        get => connectingMessage;
        set
        {
            if (connectingMessage != value)
            {
                connectingMessage = value;
                OnPropertyChanged();
            }
        }
    }

    // Costruttore della pagina di caricamento.
    // Riceve il dispositivo selezionato e un oggetto TaskCompletionSource per restituire il risultato della connessione.
    // Imposta il messaggio di connessione e il BindingContext per il data binding.
    public LoadingPage(ShimmerDevice device, TaskCompletionSource<XR2Learn_ShimmerIMU> completion)
    {
        InitializeComponent();
        this.device = device;
        _completion = completion;
        ConnectingMessage = $"Connecting to {device.Port1} / {device.Port2}...";
        BindingContext = this;
    }

    // Metodo chiamato automaticamente quando la pagina diventa visibile.
    // Tenta di connettersi al dispositivo Shimmer su una delle due porte (Port1 o Port2).
    // Se la connessione ha successo, avvia lo streaming e restituisce il dispositivo connesso.
    // In ogni caso, mostra un messaggio di successo o errore all'utente.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(500);

        if (connectionInProgress) return;
        connectionInProgress = true;

        XR2Learn_ShimmerIMU? connectedShimmer = null;
        string? usedPort = null;

        foreach (var port in new[] { device.Port1, device.Port2 })
        {
            try
            {
                var shimmer = new XR2Learn_ShimmerIMU
                {
                    EnableLowNoiseAccelerometer = device.EnableAccelerometer,
                    EnableWideRangeAccelerometer = device.EnableWideRangeAccelerometer,
                    EnableGyroscope = device.EnableGyroscope,
                    EnableMagnetometer = device.EnableMagnetometer
                };

                shimmer.Configure("Shimmer", port);
                shimmer.Connect();

                if (shimmer.IsConnected())
                {
                    shimmer.StartStreaming();
                    connectedShimmer = shimmer;
                    usedPort = port;
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SHIMMER ERROR] on {port}: {ex.Message}");
            }
        }

        await DisplayAlert(
            connectedShimmer != null ? "Success" : "Connection Failed",
            connectedShimmer != null ? $"{device.DisplayName} connected on {usedPort}"
                      : $"Could not connect to {device.DisplayName}.",
            "OK");

        _completion.SetResult(connectedShimmer);
    }

    // Evento di notifica delle modifiche alle proprietà, richiesto per aggiornare l'interfaccia utente durante il binding.
    public new event PropertyChangedEventHandler PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
