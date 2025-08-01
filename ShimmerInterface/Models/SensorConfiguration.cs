
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models;


public partial class SensorConfiguration : ObservableObject
{
    [ObservableProperty] private string portName;

    [ObservableProperty] private bool isSelected;

    [ObservableProperty] private bool enableLowNoiseAccelerometer = true;
    [ObservableProperty] private bool enableWideRangeAccelerometer = true;
    [ObservableProperty] private bool enableGyroscope = true;
    [ObservableProperty] private bool enableMagnetometer = true;
    [ObservableProperty] private bool enableBattery = true;
    [ObservableProperty] private bool enablePressureTemperature = true;

    [ObservableProperty] private bool enableExtA6 = true;

    [ObservableProperty] private bool enableExtA7 = true;

    [ObservableProperty] private bool enableExtA15 = true;
    public string DisplayName => $"Shimmer on {PortName}";
}
