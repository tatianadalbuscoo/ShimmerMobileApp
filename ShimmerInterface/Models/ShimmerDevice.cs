using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models
{
    public partial class ShimmerDevice : ObservableObject
    {
        [ObservableProperty]
        private string displayName;

        [ObservableProperty]
        private string port1;

        [ObservableProperty]
        private string port2;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool enableAccelerometer = true;

        [ObservableProperty]
        private bool enableWideRangeAccelerometer = true;

        [ObservableProperty]
        private bool enableGyroscope = true;

        [ObservableProperty]
        private bool enableMagnetometer = true;

        [ObservableProperty]
        private bool enableBattery = true;

        [ObservableProperty]
        private bool enableExtA6 = true;

        [ObservableProperty]
        private bool enableExtA7 = true;

        [ObservableProperty]
        private bool enableExtA15 = true;

        [ObservableProperty] 
        private bool enablePressureTemperature = true;


        // Nuove proprietà per il nome Shimmer e l'indirizzo Bluetooth
        [ObservableProperty]
        private string shimmerName;

        [ObservableProperty]
        private string bluetoothAddress;


        [ObservableProperty]
        private double samplingRate = 51.2;
    }
}