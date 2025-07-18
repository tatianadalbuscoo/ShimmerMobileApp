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
        private bool enableGyroscope = true;

        [ObservableProperty]
        private bool enableMagnetometer = true;

        // Nuove proprietà per il nome Shimmer e l'indirizzo Bluetooth
        [ObservableProperty]
        private string shimmerName;

        [ObservableProperty]
        private string bluetoothAddress;

        // Proprietà per configurazioni aggiuntive se necessarie
        [ObservableProperty]
        private bool enableGSR = false;

        [ObservableProperty]
        private bool enablePPG = false;

        [ObservableProperty]
        private double samplingRate = 51.2;
    }
}