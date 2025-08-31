using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models
{
    /// <summary>
    /// Represents a Shimmer device with configuration options for each available sensor.
    /// Used for UI binding and runtime control of sensor activation and metadata.
    /// </summary>
    public partial class ShimmerDevice : ObservableObject
    {
        // ===== Identità / selezione =====

        // Display name shown in the UI (e.g., "Shimmer E123 (COM4)")
        [ObservableProperty] private string displayName = "";

        // Internal Shimmer identifier (e.g., "E123")
        [ObservableProperty] private string shimmerName = "";

        // Serial port used for communication
        [ObservableProperty] private string port1 = "";

        // Whether this device is selected for connection (checkbox)
        [ObservableProperty] private bool isSelected;

        // ===== Sensori (default attivi come nel tuo codice) =====

        [ObservableProperty] private bool enableLowNoiseAccelerometer = true;
        [ObservableProperty] private bool enableWideRangeAccelerometer = true;
        [ObservableProperty] private bool enableGyroscope = true;
        [ObservableProperty] private bool enableMagnetometer = true;
        [ObservableProperty] private bool enablePressureTemperature = true;
        [ObservableProperty] private bool enableBattery = true;
        [ObservableProperty] private bool enableExtA6 = true;
        [ObservableProperty] private bool enableExtA7 = true;
        [ObservableProperty] private bool enableExtA15 = true;

        // ===== Risultati scan (IMU/EXG) =====

        // True => EXG, False => IMU (set by scanner in VM)
        [ObservableProperty] private bool isExg;

        // Raw board id/name as seen from FW (optional, useful for debug)
        [ObservableProperty] private string boardRawId = "";

        // Channels read from FW (comma-separated) – opzionale per UI/debug
        [ObservableProperty] private string channelsDisplay = "(none)";

        // Badge text for the card ("EXG" or "IMU")
        public string BoardKindLabel => IsExg ? "EXG" : "IMU";

        partial void OnIsExgChanged(bool value)
        {
            // Aggiorna badge
            OnPropertyChanged(nameof(BoardKindLabel));

            // Se diventa EXG e non c'è una scelta ancora, imposta ECG come default
            if (value && !(IsExgModeECG || IsExgModeEMG || IsExgModeTest || IsExgModeRespiration))
            {
                IsExgModeECG = true;
            }
        }

        // ===== UI helpers =====

        public string PortDisplay =>
#if ANDROID
            $"MAC: {Port1}";
#else
            $"Port: {Port1}";
#endif
        partial void OnPort1Changed(string value) => OnPropertyChanged(nameof(PortDisplay));

        // ===== EXG mode (solo se IsExg == true) – 4 scelte esclusive =====
        //
        // In XAML userai:
        // - IsVisible="{Binding IsExg}" per mostrare la riga solo per EXG
        // - 4 RadioButton con GroupName=ExgModeGroupName
        // - binding TwoWay alle IsExgMode* qui sotto
        //

        /// <summary>
        /// Unique radio group name per card: evita che i radio di dispositivi diversi interferiscano.
        /// </summary>
        public string ExgModeGroupName { get; } = "EXG_" + Guid.NewGuid().ToString("N");

        /// <summary>
        /// Modalità EXG selezionata ("ECG", "EMG", "EXG Test", "Respiration").
        /// Utile se vuoi leggere rapidamente la scelta nel ViewModel/streaming.
        /// </summary>
        [ObservableProperty] private string selectedExgMode = "ECG";

        // Radio: ECG
        [ObservableProperty] private bool isExgModeECG; // default impostato quando IsExg diventa true
        partial void OnIsExgModeECGChanged(bool value)
        {
            if (!value) return;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeTest) IsExgModeTest = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "ECG";
        }

        // Radio: EMG
        [ObservableProperty] private bool isExgModeEMG;
        partial void OnIsExgModeEMGChanged(bool value)
        {
            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeTest) IsExgModeTest = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "EMG";
        }

        // Radio: EXG Test
        [ObservableProperty] private bool isExgModeTest;
        partial void OnIsExgModeTestChanged(bool value)
        {
            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "EXG Test";
        }

        // Radio: Respiration
        [ObservableProperty] private bool isExgModeRespiration;
        partial void OnIsExgModeRespirationChanged(bool value)
        {
            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeTest) IsExgModeTest = false;
            SelectedExgMode = "Respiration";
        }
    }
}
