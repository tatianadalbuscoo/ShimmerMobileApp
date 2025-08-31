using System;
using CommunityToolkit.Mvvm.ComponentModel;
#if WINDOWS
using XR2Learn_ShimmerAPI.GSR; // ExgMode enum (wrapper EXG)
#endif

namespace ShimmerInterface.Models
{
    /// <summary>
    /// Represents a Shimmer device with configuration options for each available sensor.
    /// Used for UI binding and runtime control of sensor activation and metadata.
    /// </summary>
    public partial class ShimmerDevice : ObservableObject
    {
        // ===== Identità / selezione =====

        /// <summary>Display name shown in the UI (e.g., "Shimmer E123 (COM4)")</summary>
        [ObservableProperty] private string displayName = "";

        /// <summary>Internal Shimmer identifier (e.g., "E123")</summary>
        [ObservableProperty] private string shimmerName = "";

        /// <summary>Serial port used for communication</summary>
        [ObservableProperty] private string port1 = "";

        /// <summary>Whether this device is selected for connection (checkbox)</summary>
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

        /// <summary>Accensione/spegnimento streaming EXG (indipendente dal fatto che la board sia EXG)</summary>
        [ObservableProperty] private bool enableExg = true;

        // ===== Risultati scan (IMU/EXG) =====

        /// <summary>True =&gt; EXG, False =&gt; IMU (set by scanner in VM)</summary>
        [ObservableProperty] private bool isExg;

        /// <summary>Raw board id/name as seen from FW (optional, useful for debug)</summary>
        [ObservableProperty] private string boardRawId = "";

        /// <summary>Channels read from FW (comma-separated) – opzionale per UI/debug</summary>
        [ObservableProperty] private string channelsDisplay = "(none)";

        /// <summary>Badge text for the card ("EXG" or "IMU")</summary>
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

            // Aggiorna helper correlati
            OnPropertyChanged(nameof(PortDisplay));
            OnPropertyChanged(nameof(WantsExg));
            OnPropertyChanged(nameof(WantExgCh1));
            OnPropertyChanged(nameof(WantExgCh2));
            OnPropertyChanged(nameof(WantRespiration));
#if WINDOWS
            OnPropertyChanged(nameof(ExgModeEnum));
#endif
            OnPropertyChanged(nameof(SuggestedExgSamplingHz));
        }

        // ===== UI helpers =====

        /// <summary>Testo visualizzato per la porta (o MAC su Android)</summary>
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
#if WINDOWS
            OnPropertyChanged(nameof(ExgModeEnum));
#endif
            RaiseExgHelpersChanged();
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
#if WINDOWS
            OnPropertyChanged(nameof(ExgModeEnum));
#endif
            RaiseExgHelpersChanged();
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
#if WINDOWS
            OnPropertyChanged(nameof(ExgModeEnum));
#endif
            RaiseExgHelpersChanged();
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
#if WINDOWS
            OnPropertyChanged(nameof(ExgModeEnum));
#endif
            RaiseExgHelpersChanged();
        }

        /// <summary>Aggiorna helper quando l’utente accende/spegne EXG</summary>
        partial void OnEnableExgChanged(bool value)
        {
            OnPropertyChanged(nameof(WantsExg));
            RaiseExgHelpersChanged();
        }

        // ===== Computed & helper per EXG =====

#if WINDOWS
        /// <summary>
        /// Mapping dei radio button alla enum ExgMode del wrapper (solo Windows).
        /// Usa questa property nel codice di connessione:
        ///   shimmer.EnableExg = device.WantsExg;
        ///   shimmer.ExgMode   = device.ExgModeEnum;
        /// </summary>
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

        /// <summary>True se ha senso aprire lo stream EXG (board EXG + toggle attivo)</summary>
        public bool WantsExg => IsExg && EnableExg;

        /// <summary>Ch1 richiesto per ECG/EMG/Test</summary>
        public bool WantExgCh1 =>
            WantsExg && (IsExgModeECG || IsExgModeEMG || IsExgModeTest);

        /// <summary>Ch2 richiesto per ECG/EMG/Test</summary>
        public bool WantExgCh2 =>
            WantsExg && (IsExgModeECG || IsExgModeEMG || IsExgModeTest);

        /// <summary>Segnale respirazione richiesto in modalità Respiration</summary>
        public bool WantRespiration =>
            WantsExg && IsExgModeRespiration;

        /// <summary>Sampling suggerito (puoi ignorarlo se preferisci)</summary>
        public int SuggestedExgSamplingHz =>
            IsExgModeRespiration ? 256 : 512;

        /// <summary>Alza le PropertyChanged per gli helper EXG</summary>
        private void RaiseExgHelpersChanged()
        {
            OnPropertyChanged(nameof(WantExgCh1));
            OnPropertyChanged(nameof(WantExgCh2));
            OnPropertyChanged(nameof(WantRespiration));
            OnPropertyChanged(nameof(SuggestedExgSamplingHz));
        }
    }
}
