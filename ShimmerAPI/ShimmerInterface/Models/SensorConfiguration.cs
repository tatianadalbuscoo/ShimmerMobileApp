using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models;

public partial class SensorConfiguration : ObservableObject
{
    [ObservableProperty] private string portName;

    [ObservableProperty] private bool isSelected;

    [ObservableProperty] private bool enableAccelerometer = true;
    [ObservableProperty] private bool enableGSR = true;
    [ObservableProperty] private bool enablePPG = true;

    public string DisplayName => $"Shimmer on {PortName}";
}

