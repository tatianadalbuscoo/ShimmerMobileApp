using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;

namespace ShimmerInterface.Views;

public partial class DataPage : ContentPage
{
    private DataPageViewModel viewModel;

    public DataPage(XR2Learn_ShimmerGSR shimmer)
    {
        InitializeComponent();
        viewModel = new DataPageViewModel(shimmer);
        BindingContext = viewModel;

        // Sottoscrive l'evento per aggiornare il grafico
        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        viewModel.OnCanvasViewPaintSurface(e.Surface.Canvas, e.Info);
    }

    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        // Forza il ridisegno del canvas sul thread UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            canvasView.InvalidateSurface();
        });
    }

    protected override void OnDisappearing()
    {
        // Pulisce gli eventi quando la pagina viene chiusa
        if (viewModel != null)
        {
            viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        }
        base.OnDisappearing();
    }
}