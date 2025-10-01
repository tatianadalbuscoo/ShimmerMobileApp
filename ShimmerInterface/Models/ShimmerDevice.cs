/*
 * ShimmerDevice model for MVVM binding.
 * Tracks EXG/IMU state, sensor flags, and selected modes with auto notifications.
 * UI labels/helpers are computed.
 * On Windows, ExgModeEnum maps mode flags to the SDK enum.
 */


using System;
using CommunityToolkit.Mvvm.ComponentModel;

#if WINDOWS
using ShimmerSDK.EXG; 
#endif


namespace ShimmerInterface.Models
{

    /// <summary>
    /// Represents a Shimmer device with configuration options for each available sensor.
    /// Used for UI binding and runtime control of sensor activation and metadata.
    /// </summary>
    public partial class ShimmerDevice : ObservableObject
    {

        // Display name shown in the UI (e.g., "Shimmer E123 (COM4)")
        [ObservableProperty] private string displayName = "";

        // Internal Shimmer identifier (e.g., "E123")
        [ObservableProperty] private string shimmerName = "";

        // Serial port used for communication
        [ObservableProperty] private string port1 = "";

        // Whether this device is selected for connection (checkbox)
        [ObservableProperty] private bool isSelected;

        // True => EXG, False => IMU (set by scanner)
        [ObservableProperty] private bool isExg;

        // Raw board id/name
        [ObservableProperty] private string boardRawId = "";

        // UI channels
        [ObservableProperty] private string channelsDisplay = "(none)";

        // Status badge (EXG/IMU/device off)
        [ObservableProperty] private string rightBadge = string.Empty;

        // Selected EXG mode ("ECG", "EMG", "EXG Test", "Respiration").
        [ObservableProperty] private string selectedExgMode = "ECG";

        // ECG mode
        [ObservableProperty] private bool isExgModeECG;

        // EMG mode
        [ObservableProperty] private bool isExgModeEMG;

        // Respiration mode
        [ObservableProperty] private bool isExgModeRespiration;

        // EXG Test mode
        [ObservableProperty] private bool isExgModeTest;

        // Sensors (defaults enabled)
        [ObservableProperty] private bool enableLowNoiseAccelerometer = true;
        [ObservableProperty] private bool enableWideRangeAccelerometer = true;
        [ObservableProperty] private bool enableGyroscope = true;
        [ObservableProperty] private bool enableMagnetometer = true;
        [ObservableProperty] private bool enablePressureTemperature = true;
        [ObservableProperty] private bool enableBattery = true;
        [ObservableProperty] private bool enableExtA6 = true;
        [ObservableProperty] private bool enableExtA7 = true;
        [ObservableProperty] private bool enableExtA15 = true;
        [ObservableProperty] private bool enableExg = true;


        /// <summary>
        /// Gets the badge label for the device kind.
        /// </summary>
        /// <returns>
        /// "EXG" if <see cref="IsExg"/> is true; otherwise "IMU".
        /// </returns>
        public string BoardKindLabel => IsExg ? "EXG" : "IMU";


        /// <summary>
        /// Gets the platform-specific port/MAC label shown in the UI.
        /// </summary>
        /// <returns>
        /// On Android: "MAC: {Port1}"; on other platforms: "Port: {Port1}".
        /// </returns>
        public string PortDisplay =>

#if ANDROID

            $"MAC: {Port1}";

#else

            $"Port: {Port1}";

#endif


        /// <summary>
        /// Raises a change notification for <see cref="PortDisplay"/> whenever <see cref="Port1"/> changes.
        /// </summary>
        /// <param name="value">The new value of <see cref="Port1"/>.</param>
        partial void OnPort1Changed(string value) => OnPropertyChanged(nameof(PortDisplay));


        /// <summary>
        /// Indicates whether EXG streaming should be opened (EXG board + toggle enabled).
        /// </summary>
        /// <returns><c>true</c> if <see cref="IsExg"/> and <see cref="EnableExg"/> are both true; otherwise <c>false</c>.</returns>
        public bool WantsExg => IsExg && EnableExg;


        /// <summary>
        /// Indicates whether EXG channel 1 is required (ECG/EMG/Test modes).
        /// </summary>
        /// <returns><c>true</c> if <see cref="WantsExg"/> is true and any of
        /// <see cref="IsExgModeECG"/>, <see cref="IsExgModeEMG"/>, or <see cref="IsExgModeTest"/> is true; otherwise <c>false</c>.</returns>
        public bool WantExg1 =>
            WantsExg && (IsExgModeECG || IsExgModeEMG || IsExgModeTest);


        /// <summary>
        /// Indicates whether EXG channel 2 is required (ECG/EMG/Test modes).
        /// </summary>
        /// <returns><c>true</c> if <see cref="WantsExg"/> is true and any of
        /// <see cref="IsExgModeECG"/>, <see cref="IsExgModeEMG"/>, or <see cref="IsExgModeTest"/> is true; otherwise <c>false</c>.</returns>
        public bool WantExg2 =>
            WantsExg && (IsExgModeECG || IsExgModeEMG || IsExgModeTest);


        /// <summary>
        /// Handles changes to <see cref="IsExg"/>:
        /// updates dependent UI properties and sets a default EXG mode if none is selected.
        /// </summary>
        /// <param name="value">New value of <see cref="IsExg"/>.</param>
        partial void OnIsExgChanged(bool value)
        {

            // Update badge label
            OnPropertyChanged(nameof(BoardKindLabel));

            // If switching to EXG and no mode is selected yet, default to ECG
            if (value && !(IsExgModeECG || IsExgModeEMG || IsExgModeTest || IsExgModeRespiration))
            {
                IsExgModeECG = true;
            }

            // Refresh related computed properties
            OnPropertyChanged(nameof(PortDisplay));
            OnPropertyChanged(nameof(WantsExg));
            OnPropertyChanged(nameof(WantExg1));
            OnPropertyChanged(nameof(WantExg2));

#if WINDOWS

            OnPropertyChanged(nameof(ExgModeEnum));

#endif

        }


        /// <summary>
        /// Handles ECG mode toggle: enforces mutual exclusivity, updates selection and helpers.
        /// </summary>
        /// <param name="value">New value of <see cref="IsExgModeECG"/>.</param>
        partial void OnIsExgModeECGChanged(bool value)
        {

            if (!value) return;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeTest) IsExgModeTest = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "ECG";

#if WINDOWS

            OnPropertyChanged(nameof(ExgModeEnum));

#endif

            RaiseExgHelpersChanged();

        }


        /// <summary>
        /// Handles EMG mode toggle: enforces mutual exclusivity, updates selection and helpers.
        /// </summary>
        /// <param name="value">New value of <see cref="IsExgModeEMG"/>.</param>
        partial void OnIsExgModeEMGChanged(bool value)
        {

            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeTest) IsExgModeTest = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "EMG";

#if WINDOWS

            OnPropertyChanged(nameof(ExgModeEnum));

#endif

            RaiseExgHelpersChanged();

        }


        /// <summary>
        /// Handles EXG Test mode toggle: enforces mutual exclusivity, updates selection and helpers.
        /// </summary>
        /// <param name="value">New value of <see cref="IsExgModeTest"/>.</param>
        partial void OnIsExgModeTestChanged(bool value)
        {

            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeRespiration) IsExgModeRespiration = false;
            SelectedExgMode = "EXG Test";

#if WINDOWS

            OnPropertyChanged(nameof(ExgModeEnum));

#endif

            RaiseExgHelpersChanged();

        }


        /// <summary>
        /// Handles Respiration mode toggle: enforces mutual exclusivity, updates selection and helpers.
        /// </summary>
        /// <param name="value">New value of <see cref="IsExgModeRespiration"/>.</param>
        partial void OnIsExgModeRespirationChanged(bool value)
        {

            if (!value) return;
            if (isExgModeECG) IsExgModeECG = false;
            if (isExgModeEMG) IsExgModeEMG = false;
            if (isExgModeTest) IsExgModeTest = false;
            SelectedExgMode = "Respiration";

#if WINDOWS

            OnPropertyChanged(nameof(ExgModeEnum));

#endif

            RaiseExgHelpersChanged();

        }


        /// <summary>
        /// Updates dependent properties when the EXG toggle changes.
        /// </summary>
        /// <param name="value">New value of <see cref="EnableExg"/>.</param>
        partial void OnEnableExgChanged(bool value)
        {
            OnPropertyChanged(nameof(WantsExg));
            RaiseExgHelpersChanged();
        }


        /// <summary>
        /// Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>  
        /// for EXG helper properties after mode/toggle changes.
        /// </summary>
        private void RaiseExgHelpersChanged()
        {
            OnPropertyChanged(nameof(WantExg1));
            OnPropertyChanged(nameof(WantExg2));
        }


#if WINDOWS

        /// <summary>
        /// Maps the current EXG mode flags to <see cref="ExgMode"/> (Windows only).
        /// </summary>
        /// <returns>
        /// <see cref="ExgMode.EMG"/> if <see cref="IsExgModeEMG"/> is true;
        /// <see cref="ExgMode.Respiration"/> if <see cref="IsExgModeRespiration"/> is true;
        /// <see cref="ExgMode.ExGTest"/> if <see cref="IsExgModeTest"/> is true;
        /// otherwise <see cref="ExgMode.ECG"/>.
        /// </returns>
        public ExgMode ExgModeEnum
        {
            get
            {
                if (IsExgModeEMG) return ExgMode.EMG;
                if (IsExgModeRespiration) return ExgMode.Respiration;
                if (IsExgModeTest) return ExgMode.ExGTest;
                return ExgMode.ECG;
            }
        }

#endif

    }
}
