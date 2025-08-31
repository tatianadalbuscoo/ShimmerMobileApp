using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models
{
    /// <summary>
    /// Represents a Shimmer device with configuration options for each available sensor.
    /// Used for UI binding and runtime control of sensor activation and metadata.
    /// </summary>
    public partial class ShimmerDevice : ObservableObject
    {
        // Display name shown in the UI (e.g., "Shimmer E123 (COM4)")
        [ObservableProperty]
        private string displayName = "";

        // Internal Shimmer identifier (e.g., "E123")
        [ObservableProperty]
        private string shimmerName = "";

        // Serial port used for communication
        [ObservableProperty]
        private string port1 = "";

        // Whether this device is selected for connection (checkbox)
        [ObservableProperty]
        private bool isSelected;

        // Enable low-noise accelerometer (default: true)
        [ObservableProperty]
        private bool enableLowNoiseAccelerometer = true;

        // Enable wide-range accelerometer (default: true)
        [ObservableProperty]
        private bool enableWideRangeAccelerometer = true;

        // Enable gyroscope (default: true)
        [ObservableProperty]
        private bool enableGyroscope = true;

        // Enable magnetometer (default: true)
        [ObservableProperty]
        private bool enableMagnetometer = true;

        // Enable pressure and temperature sensor (default: true)
        [ObservableProperty]
        private bool enablePressureTemperature = true;

        // Enable battery voltage monitoring (default: true)
        [ObservableProperty]
        private bool enableBattery = true;

        // Enable external ADC channel A6 (default: true)
        [ObservableProperty]
        private bool enableExtA6 = true;

        // Enable external ADC channel A7 (default: true)
        [ObservableProperty]
        private bool enableExtA7 = true;

        // Enable external ADC channel A15 (default: true)
        [ObservableProperty]
        private bool enableExtA15 = true;

        // ========= NEW: Scan results for UI =========

        // True => EXG, False => IMU (set by scanner in VM)
        [ObservableProperty]
        private bool isExg;

        // Raw board id/name as seen from FW (optional, useful for debug)
        [ObservableProperty]
        private string boardRawId = "";

        // Channels read from FW (comma-separated)
        [ObservableProperty]
        private string channelsDisplay = "(none)";

        // Badge text for the card ("EXG" or "IMU")
        public string BoardKindLabel => IsExg ? "EXG" : "IMU";

        // Update dependent computed property when IsExg changes
        partial void OnIsExgChanged(bool value) => OnPropertyChanged(nameof(BoardKindLabel));

        // ========= UI helpers =========

        public string PortDisplay =>
#if ANDROID
            $"MAC: {Port1}";
#else
            $"Port: {Port1}";
#endif

        partial void OnPort1Changed(string value) => OnPropertyChanged(nameof(PortDisplay));
    }
}
