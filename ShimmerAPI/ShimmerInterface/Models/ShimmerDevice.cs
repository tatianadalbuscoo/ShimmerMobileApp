// This class represents a Shimmer device with two COM ports,
// a display name, selection state, and sensor configuration flags (Accelerometer, GSR, PPG).
// It is used in the device selection UI and supports data binding through CommunityToolkit.Mvvm.

using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models;

public partial class ShimmerDevice : ObservableObject
{
    [ObservableProperty] private string displayName;
    [ObservableProperty] private string port1;
    [ObservableProperty] private string port2;

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool enableAccelerometer = true;
    [ObservableProperty] private bool enableGSR = true;
    [ObservableProperty] private bool enablePPG = true;
}
