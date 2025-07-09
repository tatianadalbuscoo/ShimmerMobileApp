using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui;
using ShimmerInterface.Models;


namespace ShimmerInterface.Views;


public partial class DataPage : ContentPage
{
    private readonly DataPageViewModel viewModel;

    public DataPage(XR2Learn_ShimmerGSR shimmer, SensorConfiguration sensorConfig)
    {
        InitializeComponent();

        viewModel = new DataPageViewModel(shimmer, sensorConfig);
        BindingContext = viewModel;

        viewModel.ChartUpdateRequested += OnChartUpdateRequested;
    }

    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        viewModel.OnCanvasViewPaintSurface(e.Surface.Canvas, e.Info);
    }

    private void OnChartUpdateRequested(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            canvasView.InvalidateSurface();
        });
    }

    protected override void OnDisappearing()
    {
        viewModel.ChartUpdateRequested -= OnChartUpdateRequested;
        base.OnDisappearing();
    }
}
