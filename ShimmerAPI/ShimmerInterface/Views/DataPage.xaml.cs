using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using ShimmerInterface.Models;


namespace ShimmerInterface.Views;

public partial class DataPage : ContentPage
{
    private readonly DataPageViewModel viewModel;

    // Costruttore della pagina. Inizializza il ViewModel con il dispositivo Shimmer e la configurazione selezionata,
    // imposta il BindingContext e si registra per aggiornare il grafico quando necessario.
    public DataPage(XR2Learn_ShimmerGSR shimmer, SensorConfiguration sensorConfig)
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);


        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    // Gestisce l'evento di disegno del grafico: delega al ViewModel per disegnare sulla superficie della canvas.
    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        viewModel.OnCanvasViewPaintSurface(e.Surface.Canvas, e.Info);
    }

    // Richiama l'invalidazione della canvas sul thread principale per forzare il ridisegno del grafico.
    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            canvasView.InvalidateSurface();
        });
    }

    //Metodo chiamato quando la pagina diventa visibile.
    // Riavvia il timer del ViewModel per aggiornare i dati e si registra di nuovo per l'evento di aggiornamento grafico.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.StartTimer(); // Lo abiliti ogni volta che la pagina torna attiva
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    // Metodo chiamato quando la pagina scompare.
    // Ferma il timer del ViewModel per evitare aggiornamenti inutili e si deregistra dall'evento di aggiornamento grafico.
    protected override void OnDisappearing()
    {
        viewModel.StopTimer();
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        base.OnDisappearing();
    }


}
