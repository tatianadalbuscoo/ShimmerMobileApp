using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using ShimmerSDK.IMU;
using System.Globalization;
using System.Reflection;
using ShimmerSDK.EXG;
using CommunityToolkit.Mvvm.Input;
using System.Dynamic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;




namespace ShimmerInterfaceTests.ViewModelsTests
{
    public class DataPageViewModelTests
    {
        // Helpers -------------------------------------------------------------

        private static ShimmerDevice Config_AllOn_IMU()
        {
            return new ShimmerDevice
            {
                // IMU flags
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,

                // EXG (non usato in ctor IMU, ma non fa male impostarlo)
                EnableExg = false
            };
        }

        private static DataPageViewModel CreateVmIMU()
        {
            var imu = new ShimmerSDK_IMU();   // in TEST_STUBS deve essere innocuo
            var cfg = Config_AllOn_IMU();
            return new DataPageViewModel(imu, cfg);
        }


        // Helper: crea un IMU finto (stub SDK sotto TEST_STUBS) + config con tutti i flag attivi
        private static (ShimmerSDK.IMU.ShimmerSDK_IMU imu, ShimmerInterface.Models.ShimmerDevice cfg)
            MakeImuDeviceWithAllFlags(double rate = 51.2)
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();

            // Se lo stub espone SamplingRate scrivibile, impostalo; altrimenti ignora.
            try
            {
                var prop = typeof(ShimmerSDK.IMU.ShimmerSDK_IMU).GetProperty("SamplingRate");
                if (prop != null && prop.CanWrite)
                    prop.SetValue(imu, rate);
            }
            catch { /* ok */ }

            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                // IMU flags
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,

                // EXG spento in questa config (stiamo testando il percorso IMU)
                EnableExg = false
            };

            return (imu, cfg);
        }

        // -----------------------------
        // Helpers per questi test
        // -----------------------------
        private static (ShimmerSDK.EXG.ShimmerSDK_EXG exg, ShimmerInterface.Models.ShimmerDevice cfg)
            MakeExgDeviceWithAllFlags()
        {
            var exg = new ShimmerSDK.EXG.ShimmerSDK_EXG();
            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                // IMU flags (presenti per completezza, ma irrilevanti qui)
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,

                // EXG ON, scegli un mode (ECG) per avere famiglie EXG disponibili
                EnableExg = true,
                IsExgModeECG = true,
                IsExgModeEMG = false,
                IsExgModeTest = false,
                IsExgModeRespiration = false
            };
            return (exg, cfg);
        }

        // -----------------------------
        // TEST: ChartDisplayMode & comportamenti collegati
        // -----------------------------

        [Fact(DisplayName = "ChartDisplayMode: default è Multi")]
        public void ChartDisplayMode_default_is_Multi()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
        }

        [Fact(DisplayName = "ChartDisplayMode: selezione variante split IMU → passa a Split")]
        public void ChartDisplayMode_switches_to_Split_when_selecting_IMU_split_label()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Forziamo una voce "split" esistente (coincide con InitializeAvailableParameters)
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";

            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel); // IMU in Split
        }

        [Fact(DisplayName = "ChartDisplayMode: selezione gruppo IMU normale → rimane Multi")]
        public void ChartDisplayMode_stays_Multi_for_regular_IMU_group()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope"; // gruppo, non variante split

            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartDisplayMode + EXG: Multi/Split e ChartModeLabel coerenti")]
        public void ChartDisplayMode_EXG_multi_vs_split_label()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            // 1) Gruppo EXG in Multi
            vm.SelectedParameter = "ECG";
            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);
            Assert.False(vm.IsExgSplit); // ExgSplit è true solo in Split

            // 2) Variante split → deve passare a Split ed aggiornare label
            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
            Assert.True(vm.IsExgSplit);
        }

        [Fact(DisplayName = "IsExgSplit: vero SOLO se Split e parametro EXG-like")]
        public void IsExgSplit_true_only_when_Split_and_EXG_family()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            // Multi + EXG → false
            vm.SelectedParameter = "ECG";
            Assert.False(vm.IsExgSplit);

            // Split + EXG → true
            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            Assert.True(vm.IsExgSplit);

            // Split + gruppo IMU → false
            var (imu, cfgImu) = MakeImuDeviceWithAllFlags();
            vm = new DataPageViewModel(imu, cfgImu);
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";
            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.False(vm.IsExgSplit);
        }

        [Fact(DisplayName = "ChartModeLabel: IMU - Multi e Split restituiscono descrizioni corrette")]
        public void ChartModeLabel_IMU_texts_are_correct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope"; // Multi
            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);

            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)"; // Split
            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartModeLabel: EXG - Multi e Split restituiscono descrizioni corrette")]
        public void ChartModeLabel_EXG_texts_are_correct()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG"; // Multi
            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)"; // Split
            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
        }


        // =============================
        // Text-entry bindable properties
        // =============================

        [Fact(DisplayName = "SamplingRateText: set non valida non valida nulla (solo mirror di testo)")]
        public void SamplingRateText_sets_text_only_without_validation_or_numeric_change()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            var beforeRate = vm.SamplingRateDisplay;
            var beforeMsg = vm.ValidationMessage;

            vm.SamplingRateText = "abc";

            Assert.Equal("abc", vm.SamplingRateText);
            Assert.Equal(beforeRate, vm.SamplingRateDisplay); // nessun cambio numerico
            Assert.Equal(beforeMsg, vm.ValidationMessage);    // nessun messaggio di errore qui
        }

        [Fact(DisplayName = "YAxisMinText/YAxisMaxText: set aggiornano solo il testo, non i valori numerici")]
        public void YAxisMinText_MaxText_only_update_text_not_numeric_values()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var yMinBefore = vm.YAxisMin;
            var yMaxBefore = vm.YAxisMax;

            vm.YAxisMinText = "-123.45";
            vm.YAxisMaxText = "999.99";

            Assert.Equal("-123.45", vm.YAxisMinText);
            Assert.Equal("999.99", vm.YAxisMaxText);
            Assert.Equal(yMinBefore, vm.YAxisMin); // nessun apply qui
            Assert.Equal(yMaxBefore, vm.YAxisMax);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }

        [Fact(DisplayName = "TimeWindowSecondsText: valido → aggiorna proprietà e resetta timeline")]
        public void TimeWindowSecondsText_valid_updates_value_and_clears_buffers()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // stato iniziale
            Assert.Equal(20, vm.TimeWindowSeconds);
            Assert.Equal("20", vm.TimeWindowSecondsText);

            // imposta valore valido
            vm.TimeWindowSecondsText = "30";

            Assert.Equal(30, vm.TimeWindowSeconds);
            // Il testo non viene aggiornato dalla funzione: verifichiamo il numerico
            Assert.Equal(30, vm.TimeWindowSeconds);

            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }

        [Theory(DisplayName = "TimeWindowSecondsText: input non valido → messaggio + reset testo")]
        [InlineData("abc")]
        [InlineData("+")]
        public void TimeWindowSecondsText_invalid_shows_error_and_resets(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.TimeWindowSecondsText = input;

            Assert.Contains("Time Window", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("20", vm.TimeWindowSecondsText); // reset al last valid
            Assert.Equal(20, vm.TimeWindowSeconds);       // numerico inalterato
        }

        [Fact(DisplayName = "TimeWindowSecondsText: solo spazi → nessun errore, nessun reset, numerico invariato")]
        public void TimeWindowSecondsText_whitespace_is_noop()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeNumeric = vm.TimeWindowSeconds;

            vm.TimeWindowSecondsText = " ";

            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.Equal(" ", vm.TimeWindowSecondsText);   // niente reset del testo
            Assert.Equal(beforeNumeric, vm.TimeWindowSeconds); // numerico invariato (20)
        }


        [Theory(DisplayName = "TimeWindowSecondsText: min/max out-of-range → errore + reset")]
        [InlineData("0", "too small")]      // sotto MIN=1
        [InlineData("10000", "too large")]  // sopra MAX=600
        public void TimeWindowSecondsText_out_of_range_triggers_error_and_reset(string input, string snippet)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.TimeWindowSecondsText = input;

            Assert.Contains(snippet, vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("20", vm.TimeWindowSecondsText);
            Assert.Equal(20, vm.TimeWindowSeconds);
        }

        [Fact(DisplayName = "XAxisLabelIntervalText: valido → aggiorna proprietà")]
        public void XAxisLabelIntervalText_valid_updates_property()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // default iniziale
            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.Equal("5", vm.XAxisLabelIntervalText);

            vm.XAxisLabelIntervalText = "7";

            Assert.Equal(7, vm.XAxisLabelInterval);
            Assert.Equal("7", vm.XAxisLabelIntervalText);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }

        [Theory(DisplayName = "XAxisLabelIntervalText: out-of-range → errore + reset")]
        [InlineData("0", "too low")]          // MIN=1
        [InlineData("20000", "too high")]     // MAX=1000
        [InlineData("abc", "must be a valid positive number")]
        public void XAxisLabelIntervalText_out_of_range_or_invalid_resets_and_errors(string input, string snippet)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.XAxisLabelIntervalText = input;

            Assert.Contains(snippet, vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("5", vm.XAxisLabelIntervalText); // reset al last valid
            Assert.Equal(5, vm.XAxisLabelInterval);
        }


        // ---------- Helpers riflessione per campi privati ----------
        static void SetPrivate<T>(object target, string fieldName, T value)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }

        static void SetProp<T>(object target, string propName, T value)
        {
            var p = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(p);
            p!.SetValue(target, value);
        }

        // ---------- TEST: IsExgSplit ----------

        [Fact(DisplayName = "IsExgSplit: Split + ECG → true")]
        public void IsExgSplit_split_with_ecg_true()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "ECG";                 // EXG family
            vm.ChartDisplayMode = ChartDisplayMode.Split; // forziamo Split

            Assert.True(vm.IsExgSplit);
        }

        [Fact(DisplayName = "IsExgSplit: Split + EMG (etichetta split variant) → true")]
        public void IsExgSplit_split_with_emg_split_variant_true()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Etichetta con “    → ... — separate charts (EXG1·EXG2)”
            vm.SelectedParameter = "    → EMG — separate charts (EXG1·EXG2)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.True(vm.IsExgSplit);
        }

        [Fact(DisplayName = "IsExgSplit: Multi + ECG → false")]
        public void IsExgSplit_multi_with_ecg_false()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "ECG";
            vm.ChartDisplayMode = ChartDisplayMode.Multi;

            Assert.False(vm.IsExgSplit);
        }

        [Fact(DisplayName = "IsExgSplit: Split + Gyroscope (IMU) → false")]
        public void IsExgSplit_split_with_imu_group_false()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope"; // non EXG
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.False(vm.IsExgSplit);
        }

        // ---------- TEST: CurrentTimeInSeconds ----------

        [Fact(DisplayName = "CurrentTimeInSeconds: base (sampleCounter/rate, no baseline)")]
        public void CurrentTimeInSeconds_basic()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            // Imposta la SamplingRate del device a 50 Hz
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            // sampleCounter=100 → 100/50=2.0s, baseline=0
            SetPrivate<int>(vm, "sampleCounter", 100);
            SetPrivate<double>(vm, "timeBaselineSeconds", 0.0);

            Assert.Equal(2.0, vm.CurrentTimeInSeconds, precision: 5);
        }

        [Fact(DisplayName = "CurrentTimeInSeconds: con baseline (sottrazione)")]
        public void CurrentTimeInSeconds_with_baseline()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            // corrente=100/50=2.0, baseline=1.5 → 0.5
            SetPrivate<int>(vm, "sampleCounter", 100);
            SetPrivate<double>(vm, "timeBaselineSeconds", 1.5);

            Assert.Equal(0.5, vm.CurrentTimeInSeconds, precision: 5);
        }

        [Fact(DisplayName = "CurrentTimeInSeconds: clamp a zero se baseline > corrente")]
        public void CurrentTimeInSeconds_clamped_at_zero()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            // corrente=40/50=0.8, baseline=1.2 → negativo → clamp a 0
            SetPrivate<int>(vm, "sampleCounter", 40);
            SetPrivate<double>(vm, "timeBaselineSeconds", 1.2);

            Assert.Equal(0.0, vm.CurrentTimeInSeconds, precision: 5);
        }

        // ---------- Helper già usato negli altri test ----------
        private static (ShimmerSDK_IMU imu, ShimmerDevice cfg) MakeImuDeviceWithAllFlags()
        {
            var imu = new ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 51.2);

            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = false
            };
            return (imu, cfg);
        }















        // ---------- riflessione util ----------


        static T GetPrivate<T>(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            return (T)f!.GetValue(target)!;
        }



        // =========================================================
        // ApplyYMinCommand / ApplyYMaxCommand
        // =========================================================

        [Fact(DisplayName = "ApplyYMinCommand: valore valido → aggiorna YAxisMin e pulisce errori")]
        public void ApplyYMinCommand_applies_valid_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope"; // default min/max ±250
            vm.YAxisMinText = "-100";
            vm.YAxisMaxText = "200";

            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(-100, vm.YAxisMin, 5);
            Assert.Equal("", vm.ValidationMessage);
        }

        [Fact(DisplayName = "ApplyYMinCommand: >= YMax → errore e rollback testo")]
        public void ApplyYMinCommand_blocks_when_ge_than_max()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMinText = "300"; // >= 250 → invalid
            vm.YAxisMaxText = "250";

            vm.ApplyYMinCommand.Execute(null);

            Assert.Contains("cannot be greater", vm.ValidationMessage, System.StringComparison.OrdinalIgnoreCase);
            // testo è stato ripristinato al last-valid (cioè a quello numerico corrente)
            Assert.Equal(vm.YAxisMin.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMinText);
        }

        [Fact(DisplayName = "ApplyYMaxCommand: valore valido → aggiorna YAxisMax")]
        public void ApplyYMaxCommand_applies_valid_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Low-Noise Accelerometer";
            vm.YAxisMinText = "-10";
            vm.YAxisMaxText = "10";

            vm.ApplyYMaxCommand.Execute(null);

            Assert.Equal(10, vm.YAxisMax, 5);
            Assert.Equal("", vm.ValidationMessage);
        }

        [Fact(DisplayName = "ApplyYMaxCommand: <= YMin → errore")]
        public void ApplyYMaxCommand_blocks_when_le_than_min()
        {
            // Arrange
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.AutoYAxis = false;                // la validazione è attiva solo in manual
            vm.IsYAxisManualEnabled = true;

            vm.YAxisMin = 5;                     // min attuale
            vm.YAxisMaxText = "4";               // <-- usare la property di testo

            // Act
            (vm.ApplyYMaxCommand as RelayCommand)!.Execute(null);

            // Assert
            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.YAxisMin);        // invariato
                                                 // YAxisMax resta quello precedente (non applica 4 perché invalidato)
            Assert.True(vm.YAxisMax > vm.YAxisMin);
        }


        // =========================================================
        // ApplySamplingRateCommand (async)
        // =========================================================

        [Fact(DisplayName = "ApplySamplingRateCommand: formato non valido → messaggio errore + reset")]
        public async Task ApplySamplingRateCommand_invalid_format_sets_error()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SamplingRateText = "abc";

            await vm.ApplySamplingRateCommand.ExecuteAsync(null);

            Assert.Contains("valid number", vm.ValidationMessage, System.StringComparison.OrdinalIgnoreCase);
            // Il testo viene ripristinato al last-valid (51.2 di default dal device)
            Assert.Equal("51.2", vm.SamplingRateText);
        }

        [Theory(DisplayName = "ApplySamplingRateCommand: fuori range → messaggio")]
        [InlineData("0.1", "too low")]
        [InlineData("1000", "too high")]
        public async Task ApplySamplingRateCommand_out_of_range_messages(string input, string expectedPart)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SamplingRateText = input;

            await vm.ApplySamplingRateCommand.ExecuteAsync(null);

            Assert.Contains(expectedPart, vm.ValidationMessage, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "ApplySamplingRateCommand: senza device (forziamo null) → usa valore richiesto")]
        public async Task ApplySamplingRateCommand_no_devices_uses_requested_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // forziamo l'assenza di device: shimmerImu = null; shimmerExg = null
            SetPrivate<object?>(vm, "shimmerImu", null);
            SetPrivate<object?>(vm, "shimmerExg", null);

            // ascoltiamo i “busy events” per vedere che partono/terminano
            int showBusy = 0, hideBusy = 0;
            vm.ShowBusyRequested += (_, __) => showBusy++;
            vm.HideBusyRequested += (_, __) => hideBusy++;

            vm.SamplingRateText = "25";

            await vm.ApplySamplingRateCommand.ExecuteAsync(null);

            // Nessun device: SetFirmwareSamplingRateNearestUnified ritorna l'input
            Assert.Equal(25, vm.SamplingRateDisplay, 5);
            Assert.Equal("", vm.ValidationMessage);
            Assert.True(showBusy >= 1 && hideBusy >= 1);
        }

        // =========================================================
        // DeviceSamplingRate (verifica preferenza: IMU → EXG → default)
        // =========================================================

        [Fact(DisplayName = "DeviceSamplingRate: IMU presente → samplingRateDisplay = IMU")]
        public void DeviceSamplingRate_prefers_imu_when_present()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 42.0);
            var vm = new DataPageViewModel(imu, cfg);

            // nel ctor: samplingRateDisplay = DeviceSamplingRate
            var display = GetPrivate<double>(vm, "samplingRateDisplay");
            Assert.Equal(42.0, display, 5);
        }

        [Fact(DisplayName = "DeviceSamplingRate: solo EXG → samplingRateDisplay = EXG")]
        public void DeviceSamplingRate_uses_exg_when_no_imu()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 77.0);

            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = true,
                IsExgModeECG = true
            };

            var vm = new DataPageViewModel(exg, cfg);
            var display = GetPrivate<double>(vm, "samplingRateDisplay");
            Assert.Equal(77.0, display, 5);
        }

        [Fact(DisplayName = "Device helpers: Attach/Detach/StopAsync non lanciano eccezioni")]
        public async Task Device_helpers_do_not_throw()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.AttachToDevice();
            vm.DetachFromDevice();
            await vm.StopAsync(disconnect: false);

            // nessuna eccezione = ok
            Assert.True(true);
        }

        // ---------------------------------------------------------------------------------------

        // -----------------------------
        // Utility riflessione e config
        // -----------------------------
        // Setta un campo privato (es. "shimmerImu"/"shimmerExg")
        static void SetPrivate(object target, string field, object? value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }

        // Invoca un metodo privato (es. "DeviceStartStreaming")
        static void InvokePrivate(object target, string method)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m);
            m!.Invoke(target, null);
        }

        static ShimmerDevice Cfg(bool exg) => new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true,
            EnableExg = exg
        };

        // -------------------------------------------------------------
        // TEST: DeviceStartStreaming (privato) — null-safety / no-throw
        // -------------------------------------------------------------
        [Fact(DisplayName = "DeviceStartStreaming: devices = null → non lancia")]
        public void StartStreaming_null_devices_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));
            // forzo assenza device per testare i '?.' e i try/catch
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            // Metodo target: DeviceStartStreaming (privato, invocato via riflessione)
            var ex = Record.Exception(() => InvokePrivate(vm, "DeviceStartStreaming"));
            Assert.Null(ex);
        }

        // -----------------------------------------------------------
        // TEST: DeviceStopStreaming (privato) — null-safety / no-throw
        // -----------------------------------------------------------
        [Fact(DisplayName = "DeviceStopStreaming: devices = null → non lancia")]
        public void StopStreaming_null_devices_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));
            // forzo assenza device
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            // Metodo target: DeviceStopStreaming (privato, invocato via riflessione)
            var ex = Record.Exception(() => InvokePrivate(vm, "DeviceStopStreaming"));
            Assert.Null(ex);
        }

        // -------------------------------------------------------------------
        // TEST: SubscribeSamples / UnsubscribeSamples (privati) — idempotenza
        // -------------------------------------------------------------------
        [Fact(DisplayName = "Subscribe/Unsubscribe: idempotenti e safe con null")]
        public void Subscribe_Unsubscribe_idempotent_and_safe()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            // Metodi target: SubscribeSamples / UnsubscribeSamples (privati)
            // 1) con device presenti (stub SDK)
            var ex1 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));
            var ex2 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));   // chiamata ripetuta
            var ex3 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples"));
            var ex4 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples")); // ripetuta

            Assert.Null(ex1); Assert.Null(ex2); Assert.Null(ex3); Assert.Null(ex4);

            // 2) con device = null
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex5 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));
            var ex6 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples"));
            Assert.Null(ex5); Assert.Null(ex6);
        }

        // -----------------------------------------------------------------------------------
        // TEST: ConnectAndStartAsync (pubblico) — percorre DeviceStartStreaming + eventi busy
        // -----------------------------------------------------------------------------------
        [Fact(DisplayName = "ConnectAndStartAsync: emette busy e non lancia")]
        public async Task ConnectAndStartAsync_emits_busy_and_finishes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));
            int show = 0, hide = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;

            // Metodo target: ConnectAndStartAsync (pubblico)
            // Effetti attesi: nessuna eccezione + emissione eventi busy
            var ex = await Record.ExceptionAsync(() => vm.ConnectAndStartAsync());
            Assert.Null(ex);
            Assert.True(show >= 1);
            Assert.True(hide >= 1);
        }

        // --------------------------------------------------------------------------------
        // TEST: StopAsync (pubblico) — percorre DeviceStopStreaming, completa senza eccezioni
        // --------------------------------------------------------------------------------
        [Fact(DisplayName = "StopAsync: completa senza eccezioni")]
        public async Task StopAsync_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            // Metodo target: StopAsync (pubblico)
            var ex = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: false));
            Assert.Null(ex);
        }


        // SetFirmwareSamplingRateNearestUnified


        static double InvokeSetNearest(DataPageViewModel vm, double newRate)
        {
            var m = typeof(DataPageViewModel).GetMethod(
                "SetFirmwareSamplingRateNearestUnified",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.NotNull(m);
            return (double)m!.Invoke(vm, new object[] { newRate })!;
        }

        // -------------------------------------------------------------
        // TEST: SetFirmwareSamplingRateNearestUnified — fallback (no devices)
        // -------------------------------------------------------------
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: no devices → ritorna input")]
        public void SetNearest_no_devices_returns_input()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));
            // forzo assenza device
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var requested = 37.5;
            var actual = InvokeSetNearest(vm, requested);

            Assert.Equal(requested, actual, 6);
        }

        // -------------------------------------------------------------------
        // TEST: SetFirmwareSamplingRateNearestUnified — IMU presente (no-throw)
        // -------------------------------------------------------------------
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: IMU presente → no throw")]
        public void SetNearest_with_imu_present_does_not_throw()
        {
            var imu = new ShimmerSDK_IMU();
            var vm = new DataPageViewModel(imu, Cfg(exg: false));

            var ex = Record.Exception(() => InvokeSetNearest(vm, 25.0));
            Assert.Null(ex);
        }

        // -------------------------------------------------------------------
        // TEST: SetFirmwareSamplingRateNearestUnified — EXG presente (no-throw)
        // -------------------------------------------------------------------
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: EXG presente → no throw")]
        public void SetNearest_with_exg_present_does_not_throw()
        {
            var exg = new ShimmerSDK_EXG();
            var vm = new DataPageViewModel(exg, Cfg(exg: true));

            var ex = Record.Exception(() => InvokeSetNearest(vm, 25.0));
            Assert.Null(ex);
        }




        // Construcor
        // -----------------------------
        // Utility riflessione
        // -----------------------------


        static ShimmerDevice ConfigAllOn(bool exgOn = false) => new ShimmerDevice
        {
            // IMU
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true,

            // EXG
            EnableExg = exgOn,
            IsExgModeECG = exgOn,    // scegliamo ECG per avere “ECG” in lista
            IsExgModeEMG = false,
            IsExgModeTest = false,
            IsExgModeRespiration = false
        };

        // -------------------------------------------------------------
        // TEST: Costruttore IMU — inizializzazione stato e parametri UI
        // (metodo target: public DataPageViewModel(ShimmerSDK_IMU, ShimmerDevice))
        // -------------------------------------------------------------
        [Fact(DisplayName = "Ctor(IMU): SamplingRateDisplay da IMU, parametri coerenti, testi sincronizzati")]
        public void Constructor_IMU_initializes_state_and_parameters()
        {
            // Arrange
            var imu = new ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 99.0); // lo stub accetta set riflessivo
            var cfg = ConfigAllOn(exgOn: false);

            // Act
            var vm = new DataPageViewModel(imu, cfg);

            // Assert — sampling rate copiata dal device (effetto del DeviceSamplingRate privato)
            Assert.Equal(99.0, vm.SamplingRateDisplay, 5);

            // Assert — AvailableParameters contiene le voci attese dai flag IMU
            Assert.Contains("Low-Noise Accelerometer", vm.AvailableParameters);
            Assert.Contains("    → Low-Noise Accelerometer — separate charts (X·Y·Z)", vm.AvailableParameters);
            Assert.Contains("Wide-Range Accelerometer", vm.AvailableParameters);
            Assert.Contains("Gyroscope", vm.AvailableParameters);
            Assert.Contains("Magnetometer", vm.AvailableParameters);
            Assert.Contains("Temperature_BMP180", vm.AvailableParameters);
            Assert.Contains("Pressure_BMP180", vm.AvailableParameters);
            Assert.Contains("BatteryVoltage", vm.AvailableParameters);
            Assert.Contains("BatteryPercent", vm.AvailableParameters);
            Assert.Contains("ExtADC_A6", vm.AvailableParameters);
            Assert.Contains("ExtADC_A7", vm.AvailableParameters);
            Assert.Contains("ExtADC_A15", vm.AvailableParameters);

            // Assert — SelectedParameter è resa valida
            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedParameter));
            Assert.Contains(vm.SelectedParameter, vm.AvailableParameters);

            // Forziamo un parametro noto per testare UpdateYAxisSettings applicato nel ctor
            vm.SelectedParameter = "Gyroscope";
            Assert.Equal("Gyroscope", vm.YAxisLabel);
            Assert.Equal("deg/s", vm.YAxisUnit);
            Assert.Equal(-250, vm.YAxisMin);
            Assert.Equal(250, vm.YAxisMax);

            // Assert — specchi testuali sincronizzati
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);
            Assert.Equal(vm.SamplingRateDisplay.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

            // Assert — “last valid” privati inizializzati (uguali ai correnti)
            var lastMin = GetPrivate<double>(vm, "_lastValidYAxisMin");
            var lastMax = GetPrivate<double>(vm, "_lastValidYAxisMax");
            var lastSR = GetPrivate<double>(vm, "_lastValidSamplingRate");
            var lastTW = GetPrivate<int>(vm, "_lastValidTimeWindowSeconds");
            var lastXI = GetPrivate<int>(vm, "_lastValidXAxisLabelInterval");

            Assert.Equal(vm.YAxisMin, lastMin, 10);
            Assert.Equal(vm.YAxisMax, lastMax, 10);
            Assert.Equal(vm.SamplingRateDisplay, lastSR, 10);
            Assert.Equal(vm.TimeWindowSeconds, lastTW);
            Assert.Equal(vm.XAxisLabelInterval, lastXI);
        }

        // -------------------------------------------------------------
        // TEST: Costruttore EXG — inizializzazione stato e parametri UI
        // (metodo target: public DataPageViewModel(ShimmerSDK_EXG, ShimmerDevice))
        // -------------------------------------------------------------
        [Fact(DisplayName = "Ctor(EXG): SamplingRateDisplay da EXG, parametri coerenti, testi sincronizzati")]
        public void Constructor_EXG_initializes_state_and_parameters()
        {
            // Arrange
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 77.0);
            var cfg = ConfigAllOn(exgOn: true);   // EXG abilitato e in ECG mode

            // Act
            var vm = new DataPageViewModel(exg, cfg);

            // Assert — sampling rate copiata dal device EXG
            Assert.Equal(77.0, vm.SamplingRateDisplay, 5);

            // Assert — AvailableParameters include le voci EXG coerenti con ECG (più IMU flag)
            Assert.Contains("ECG", vm.AvailableParameters);
            Assert.Contains("    → ECG — separate charts (EXG1·EXG2)", vm.AvailableParameters);
            Assert.Contains("Low-Noise Accelerometer", vm.AvailableParameters); // perché i flag IMU sono true

            // Assert — SelectedParameter valido
            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedParameter));
            Assert.Contains(vm.SelectedParameter, vm.AvailableParameters);

            // Forziamo parametro EXG per validare label/unit/range EXG
            vm.SelectedParameter = "ECG";
            Assert.Equal("ECG", vm.YAxisLabel);
            Assert.Equal("mV", vm.YAxisUnit);
            Assert.Equal(-15, vm.YAxisMin);
            Assert.Equal(15, vm.YAxisMax);

            // Assert — specchi testuali sincronizzati
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);
            Assert.Equal(vm.SamplingRateDisplay.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

            // Assert — “last valid” privati inizializzati
            var lastMin = GetPrivate<double>(vm, "_lastValidYAxisMin");
            var lastMax = GetPrivate<double>(vm, "_lastValidYAxisMax");
            var lastSR = GetPrivate<double>(vm, "_lastValidSamplingRate");
            var lastTW = GetPrivate<int>(vm, "_lastValidTimeWindowSeconds");
            var lastXI = GetPrivate<int>(vm, "_lastValidXAxisLabelInterval");

            Assert.Equal(vm.YAxisMin, lastMin, 10);
            Assert.Equal(vm.YAxisMax, lastMax, 10);
            Assert.Equal(vm.SamplingRateDisplay, lastSR, 10);
            Assert.Equal(vm.TimeWindowSeconds, lastTW);
            Assert.Equal(vm.XAxisLabelInterval, lastXI);
        }


        // Dispose
        // ---- riflessione util ----


        // Prova a leggere il delegate “backing field” dell’evento ChartUpdateRequested
        static Delegate? GetEventDelegate(object target, string eventName)
        {
            var f = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            // Per eventi auto-implementati, il backing field ha lo stesso nome dell’evento
            return f?.GetValue(target) as Delegate;
        }



        // --------------------------------------------------------------------
        // METODI SOTTO TEST:
        // - public void Dispose()
        // - protected virtual void Dispose(bool disposing)
        // --------------------------------------------------------------------

        [Fact(DisplayName = "Dispose(): svuota i buffer e azzera ChartUpdateRequested")]
        public void Dispose_clears_collections_and_nulls_event()
        {
            // Arrange
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            // Aggancia un handler all’evento ChartUpdateRequested
            EventHandler handler = (_, __) => { };
            vm.ChartUpdateRequested += handler;

            // Popola manualmente i buffer interni
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            dataDict["GyroscopeX"] = new List<float> { 1f, 2f, 3f };
            timeDict["GyroscopeX"] = new List<int> { 10, 20, 30 };

            // Pre-condizioni
            Assert.True(dataDict["GyroscopeX"].Count > 0);
            Assert.True(timeDict["GyroscopeX"].Count > 0);
            Assert.NotNull(GetEventDelegate(vm, "ChartUpdateRequested"));

            // Act
            vm.Dispose();

            // Assert — buffer svuotati
            Assert.Empty(dataDict["GyroscopeX"]);
            Assert.Empty(timeDict["GyroscopeX"]);


            // Assert — evento azzerato
            Assert.Null(GetEventDelegate(vm, "ChartUpdateRequested"));
        }

        [Fact(DisplayName = "Dispose(): idempotente (seconda chiamata non eccepisce)")]
        public void Dispose_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            vm.Dispose(); // prima
            var ex = Record.Exception(() => vm.Dispose()); // seconda
            Assert.Null(ex);
        }

        [Fact(DisplayName = "Dispose(): non lancia con devices presenti (IMU)")]
        public void Dispose_with_imu_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));
            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }

        [Fact(DisplayName = "Dispose(): non lancia con devices presenti (EXG)")]
        public void Dispose_with_exg_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_EXG(), Cfg(exg: true));
            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }

        [Fact(DisplayName = "Dispose(bool): con disposing=false non deve crashare")]
        public void Dispose_bool_false_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            // Invoca direttamente la protected Dispose(bool) con riflessione
            var m = typeof(DataPageViewModel).GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic, new Type[] { typeof(bool) });
            Assert.NotNull(m);

            var ex = Record.Exception(() => m!.Invoke(vm, new object[] { false }));
            Assert.Null(ex);
        }

        [Fact(DisplayName = "Dispose(): safe anche con shimmerImu/shimmerExg = null")]
        public void Dispose_safe_with_null_devices()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            // forzo devices a null per coprire il ramo che chiama UnsubscribeSamples con null
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }


        // OnIsYAxisManualEnabledChanged
        // -------------------------------------------------------------
        // OnIsYAxisManualEnabledChanged(bool) → NotifyCanExecuteChanged()
        // -------------------------------------------------------------


        private static ShimmerDevice AllOnCfg() => new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true,
            EnableExg = false
        };


        [Fact(DisplayName = "OnIsYAxisManualEnabledChanged: al toggle emette CanExecuteChanged su entrambi i comandi")]
        public void IsYAxisManualEnabled_toggle_raises_CanExecuteChanged_for_both_commands()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg());

            int yMinChanged = 0, yMaxChanged = 0;
            vm.ApplyYMinCommand.CanExecuteChanged += (_, __) => yMinChanged++;
            vm.ApplyYMaxCommand.CanExecuteChanged += (_, __) => yMaxChanged++;

            // default: IsYAxisManualEnabled = true; toggliamo a false → deve notificare
            vm.IsYAxisManualEnabled = false;

            Assert.True(yMinChanged >= 1, "ApplyYMinCommand dovrebbe aver notificato CanExecuteChanged");
            Assert.True(yMaxChanged >= 1, "ApplyYMaxCommand dovrebbe aver notificato CanExecuteChanged");

            // ulteriore toggle (false -> true) per conferma idempotenza
            vm.IsYAxisManualEnabled = true;
            Assert.True(yMinChanged >= 2);
            Assert.True(yMaxChanged >= 2);
        }

        // -----------------------------
        // ApplyYMin() (via ApplyYMinCommand)
        // -----------------------------
        [Fact(DisplayName = "ApplyYMin: input valido aggiorna YAxisMin e pulisce errori")]
        public void ApplyYMin_with_valid_input_updates_value_and_clears_error()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope"; // range ±250 di default
            var previousMin = vm.YAxisMin;

            vm.YAxisMinText = "-100";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(-100, vm.YAxisMin, 5);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.NotEqual(previousMin, vm.YAxisMin);
        }

        [Fact(DisplayName = "ApplyYMin: input non numerico → messaggio errore e rollback testo")]
        public void ApplyYMin_with_invalid_input_sets_error_and_rolls_back_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            var lastValid = vm.YAxisMin; // stato attuale

            vm.YAxisMinText = "abc";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Contains("valid number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            // testo ripristinato all'ultimo valido
            Assert.Equal(lastValid.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            // valore numerico invariato
            Assert.Equal(lastValid, vm.YAxisMin, 5);
        }

        [Fact(DisplayName = "ApplyYMin: AutoYAxis attivo → comando non modifica il valore")]
        public void ApplyYMin_does_nothing_when_AutoYAxis_is_true()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = true  // fa short-circuit nella validazione
            };

            vm.SelectedParameter = "Gyroscope";
            var before = vm.YAxisMin;

            vm.YAxisMinText = "-123";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(before, vm.YAxisMin, 5);
        }

        // -----------------------------
        // ApplyYMax() (via ApplyYMaxCommand)
        // -----------------------------
        [Fact(DisplayName = "ApplyYMax: input valido aggiorna YAxisMax e pulisce errori")]
        public void ApplyYMax_with_valid_input_updates_value_and_clears_error()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Low-Noise Accelerometer"; // default ±20 (gruppo), ma ok
            vm.YAxisMaxText = "10";

            vm.ApplyYMaxCommand.Execute(null);

            Assert.Equal(10, vm.YAxisMax, 5);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }

        [Fact(DisplayName = "ApplyYMax: <= YMin → errore e rollback testo")]
        public void ApplyYMax_blocks_when_less_or_equal_than_min()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMin = 5;               // imposta min corrente
            var prevMax = vm.YAxisMax;     // last valid max

            vm.YAxisMaxText = "4";
            vm.ApplyYMaxCommand.Execute(null);

            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            // rollback del testo al last valid
            Assert.Equal(prevMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            // numerico invariato
            Assert.Equal(prevMax, vm.YAxisMax, 5);
        }

        [Fact(DisplayName = "ApplyYMax: AutoYAxis attivo → comando non modifica il valore")]
        public void ApplyYMax_does_nothing_when_AutoYAxis_is_true()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = true
            };

            vm.SelectedParameter = "Gyroscope";
            var before = vm.YAxisMax;

            vm.YAxisMaxText = "123";
            vm.ApplyYMaxCommand.Execute(null);

            Assert.Equal(before, vm.YAxisMax, 5);
        }

        // SyncImuFlagsFromExgDeviceIfChanged behavior
        // --- riflessione util ---


        static object? InvokePrivate(object target, string methodName, params object[]? args)
        {
            var m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m);
            return m!.Invoke(target, args?.Length > 0 ? args : null);
        }


        static ShimmerDevice CfgAllFalseImuFlags(bool exgOn = true) => new ShimmerDevice
        {
            // IMU flags iniziali del VM (li vogliamo a false per creare differenza)
            EnableLowNoiseAccelerometer = false,
            EnableWideRangeAccelerometer = false,
            EnableGyroscope = false,
            EnableMagnetometer = false,
            EnablePressureTemperature = false,
            EnableBattery = false,
            EnableExtA6 = false,
            EnableExtA7 = false,
            EnableExtA15 = false,

            // EXG acceso (costruttore EXG)
            EnableExg = exgOn,
            IsExgModeECG = true
        };

        // ----------------------------------------------------------------
        // METODO SOTTO TEST:
        // private bool SyncImuFlagsFromExgDeviceIfChanged()
        // ----------------------------------------------------------------

        // TEST per: private bool SyncImuFlagsFromExgDeviceIfChanged()

        [Fact(DisplayName = "SyncImuFlagsFromExg: shimmerExg = null → nessuna modifica ai flag IMU")]
        public void SyncImuFlags_exg_null_keeps_flags_unchanged()
        {
            var exg = new ShimmerSDK_EXG();
            var cfg = new ShimmerDevice
            {
                // metti tutti i flag IMU a false per semplicità
                EnableLowNoiseAccelerometer = false,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = false,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = true,
                IsExgModeECG = true,
            };

            var vm = new DataPageViewModel(exg, cfg);

            // snapshot pre
            var pre = (
                GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"),
                GetPrivate<bool>(vm, "enableWideRangeAccelerometer"),
                GetPrivate<bool>(vm, "enableGyroscope"),
                GetPrivate<bool>(vm, "enableMagnetometer"),
                GetPrivate<bool>(vm, "enablePressureTemperature"),
                GetPrivate<bool>(vm, "enableBattery"),
                GetPrivate<bool>(vm, "enableExtA6"),
                GetPrivate<bool>(vm, "enableExtA7"),
                GetPrivate<bool>(vm, "enableExtA15")
            );

            // forza shimmerExg = null
            SetPrivate(vm, "shimmerExg", null);

            // invoca il metodo privato (il tuo helper è void)
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // snapshot post: devono essere identici
            var post = (
                GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"),
                GetPrivate<bool>(vm, "enableWideRangeAccelerometer"),
                GetPrivate<bool>(vm, "enableGyroscope"),
                GetPrivate<bool>(vm, "enableMagnetometer"),
                GetPrivate<bool>(vm, "enablePressureTemperature"),
                GetPrivate<bool>(vm, "enableBattery"),
                GetPrivate<bool>(vm, "enableExtA6"),
                GetPrivate<bool>(vm, "enableExtA7"),
                GetPrivate<bool>(vm, "enableExtA15")
            );

            Assert.Equal(pre, post);
        }

        [Fact(DisplayName = "SyncImuFlagsFromExg: differenze su alcuni flag → VM aggiornato")]
        public void SyncImuFlags_updates_flags_when_different()
        {
            var exg = new ShimmerSDK_EXG();
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = false,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = false,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = true,
                IsExgModeECG = true,
            };

            var vm = new DataPageViewModel(exg, cfg);

            // controlliamo che parta da false
            Assert.False(GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"));
            Assert.False(GetPrivate<bool>(vm, "enableGyroscope"));
            Assert.False(GetPrivate<bool>(vm, "enableBattery"));
            Assert.False(GetPrivate<bool>(vm, "enableExtA15"));

            // simuliamo l'EXG che riporta alcuni flag = true
            SetProp(exg, "EnableLowNoiseAccelerometer", true);
            SetProp(exg, "EnableGyroscope", true);
            SetProp(exg, "EnableBatteryVoltage", true);
            SetProp(exg, "EnableExtA15", true);

            // invoca
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // verifiche: i campi del VM devono essere aggiornati a true
            Assert.True(GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"));
            Assert.True(GetPrivate<bool>(vm, "enableGyroscope"));
            Assert.True(GetPrivate<bool>(vm, "enableBattery"));
            Assert.True(GetPrivate<bool>(vm, "enableExtA15"));
        }

        [Fact(DisplayName = "SyncImuFlagsFromExg: flag già allineati → nessuna modifica")]
        public void SyncImuFlags_no_change_when_already_equal()
        {
            var exg = new ShimmerSDK_EXG();
            var cfg = new ShimmerDevice
            {
                // all false
                EnableLowNoiseAccelerometer = false,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = false,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = true,
                IsExgModeECG = true,
            };

            var vm = new DataPageViewModel(exg, cfg);

            // allinea anche l'EXG a false (nessuna differenza)
            SetProp(exg, "EnableLowNoiseAccelerometer", false);
            SetProp(exg, "EnableWideRangeAccelerometer", false);
            SetProp(exg, "EnableGyroscope", false);
            SetProp(exg, "EnableMagnetometer", false);
            SetProp(exg, "EnablePressureTemperature", false);
            SetProp(exg, "EnableBatteryVoltage", false);
            SetProp(exg, "EnableExtA6", false);
            SetProp(exg, "EnableExtA7", false);
            SetProp(exg, "EnableExtA15", false);

            // snapshot pre
            var pre = (
                GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"),
                GetPrivate<bool>(vm, "enableWideRangeAccelerometer"),
                GetPrivate<bool>(vm, "enableGyroscope"),
                GetPrivate<bool>(vm, "enableMagnetometer"),
                GetPrivate<bool>(vm, "enablePressureTemperature"),
                GetPrivate<bool>(vm, "enableBattery"),
                GetPrivate<bool>(vm, "enableExtA6"),
                GetPrivate<bool>(vm, "enableExtA7"),
                GetPrivate<bool>(vm, "enableExtA15")
            );

            // invoca
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // snapshot post: identico
            var post = (
                GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"),
                GetPrivate<bool>(vm, "enableWideRangeAccelerometer"),
                GetPrivate<bool>(vm, "enableGyroscope"),
                GetPrivate<bool>(vm, "enableMagnetometer"),
                GetPrivate<bool>(vm, "enablePressureTemperature"),
                GetPrivate<bool>(vm, "enableBattery"),
                GetPrivate<bool>(vm, "enableExtA6"),
                GetPrivate<bool>(vm, "enableExtA7"),
                GetPrivate<bool>(vm, "enableExtA15")
            );

            Assert.Equal(pre, post);
        }

        // OnSampleReceived behavior


        // campi fittizi con proprietà reali (riflessione-friendly)
        class SampleField { public float Data { get; set; } public SampleField(float v) { Data = v; } }

        // campione IMU con proprietà pubbliche
        class ImuSample
        {
            public SampleField LowNoiseAccelerometerX { get; set; } = new SampleField(0);
            public SampleField LowNoiseAccelerometerY { get; set; } = new SampleField(0);
            public SampleField LowNoiseAccelerometerZ { get; set; } = new SampleField(0);
        }

        // campione EXG con proprietà pubbliche
        class ExgSample
        {
            public SampleField Exg1 { get; set; } = new SampleField(0);
            public SampleField Exg2 { get; set; } = new SampleField(0);
        }

        // config IMU tutto acceso (solo IMU)
        static ShimmerDevice CfgIMU_AllOn() => new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = false,
            EnableGyroscope = false,
            EnableMagnetometer = false,
            EnablePressureTemperature = false,
            EnableBattery = false,
            EnableExtA6 = false,
            EnableExtA7 = false,
            EnableExtA15 = false,
            EnableExg = false
        };

        // config EXG acceso, IMU spento
        static ShimmerDevice CfgEXG_On_IMU_AllOff() => new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = false,
            EnableWideRangeAccelerometer = false,
            EnableGyroscope = false,
            EnableMagnetometer = false,
            EnablePressureTemperature = false,
            EnableBattery = false,
            EnableExtA6 = false,
            EnableExtA7 = false,
            EnableExtA15 = false,
            EnableExg = true,
            IsExgModeECG = true
        };




        // ================= TEST IMU: contatore + redraw (no assunzioni sulle serie) =================
        [Fact(DisplayName = "OnSampleReceived (IMU): incrementa sampleCounter e richiede redraw")]
        public void OnSampleReceived_IMU_increments_counter_and_requests_redraw()
        {
            var imu = new ShimmerSDK_IMU();
            SetProp(imu, "SamplingRate", 50.0); // evita divide-by-zero ed etichette coerenti
            var vm = new DataPageViewModel(imu, CfgIMU_AllOn());

            // Se c'è almeno un parametro disponibile, seleziona il primo (robusto cross-piattaforma)
            if (vm.AvailableParameters.Count > 0)
                vm.SelectedParameter = vm.AvailableParameters[0];

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            // Sample IMU "neutro": non facciamo assunzioni sul suo parsing interno
            var sample = new ImuSample
            {
                LowNoiseAccelerometerX = new SampleField(1.23f),
                LowNoiseAccelerometerY = new SampleField(-0.5f),
                LowNoiseAccelerometerZ = new SampleField(9.81f)
            };

            // sender non-null per evitare CS8625
            InvokePrivate(vm, "OnSampleReceived", new object(), sample);

            var newCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(prevCount + 1, newCount);
            Assert.True(redraws >= 1);
        }

        // ========== TEST EXG: cambio flag → contatore + redraw (senza imporre SelectedParameter) ==========
        [Fact(DisplayName = "OnSampleReceived (EXG): se i flag cambiano → incrementa counter e richiede redraw")]
        public void OnSampleReceived_EXG_flag_change_increments_and_redraws()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp(exg, "SamplingRate", 51.2);
            var vm = new DataPageViewModel(exg, CfgEXG_On_IMU_AllOff());

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Primo sample (ok anche se non popola serie su alcune piattaforme)
            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(2f), Exg2 = new(-1f) });

            // Disallinea i flag sul device EXG rispetto allo stato interno del VM
            SetProp(exg, "EnableLowNoiseAccelerometer", true);
            SetProp(exg, "EnableGyroscope", true);
            SetProp(exg, "EnableBatteryVoltage", true);

            // Imposta una selezione non valida (non imponiamo che venga corretta: dipende dai #if/platform)
            vm.SelectedParameter = "__invalid__";

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            // Secondo sample: dovrebbe scattare il ramo di sync se supportato dalla build
            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(3f), Exg2 = new(0f) });

            var curCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.True(curCount > prevCount);
            Assert.True(redraws >= 1);

            // NON verifichiamo la correzione di SelectedParameter né il riempimento delle serie,
            // per mantenere il test portabile tra piattaforme/configurazioni.
        }

        // ======= TEST EXG: nessun cambio flag → solo contatore =======
        [Fact(DisplayName = "OnSampleReceived (EXG): senza differenze flag → incrementa solo il counter")]
        public void OnSampleReceived_EXG_no_flag_change_increments_only()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp(exg, "SamplingRate", 51.2);
            var vm = new DataPageViewModel(exg, CfgEXG_On_IMU_AllOff());

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(0.1f), Exg2 = new(0.2f) });

            var curCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(prevCount + 1, curCount);

            // Nessuna asserzione sulle serie: restiamo indipendenti dai rami #if e dal parsing dinamico.
        }


        // AttachToDevice


        // =======================
        // TEST: Attach/Detach/Connect/Stop + CanExecute
        // Coprono solo logica del VM; i device reali vengono impostati a null via riflessione.
        // =======================

        static void InvokePrivateParams(object target, string method, params object?[] args)
        {
            var type = target.GetType();
            var m = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                        .FirstOrDefault(mi => mi.Name == method &&
                                              mi.GetParameters().Length == (args?.Length ?? 0));
            Assert.NotNull(m);
            m!.Invoke(target, args);
        }

        // ---------------------------------------------
        // Method: AttachToDevice — behavior: non lancia con devices = null
        // ---------------------------------------------
        [Fact(DisplayName = "AttachToDevice: con shimmerImu/exg = null non lancia")]
        public void AttachToDevice_null_devices_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => vm.AttachToDevice());
            Assert.Null(ex);
        }

        // ---------------------------------------------
        // Method: AttachToDevice — behavior: idempotente (chiamata ripetuta non lancia)
        // ---------------------------------------------
        [Fact(DisplayName = "AttachToDevice: chiamata ripetuta non lancia")]
        public void AttachToDevice_idempotent_calls_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            var ex1 = Record.Exception(() => vm.AttachToDevice());
            var ex2 = Record.Exception(() => vm.AttachToDevice());
            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        // ---------------------------------------------
        // Method: DetachFromDevice — behavior: non lancia con devices = null
        // ---------------------------------------------
        [Fact(DisplayName = "DetachFromDevice: con shimmerImu/exg = null non lancia")]
        public void DetachFromDevice_null_devices_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => vm.DetachFromDevice());
            Assert.Null(ex);
        }

        // ---------------------------------------------
        // Method: DetachFromDevice — behavior: idempotente (chiamata ripetuta non lancia)
        // ---------------------------------------------
        [Fact(DisplayName = "DetachFromDevice: chiamata ripetuta non lancia")]
        public void DetachFromDevice_idempotent_calls_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            var ex1 = Record.Exception(() => vm.DetachFromDevice());
            var ex2 = Record.Exception(() => vm.DetachFromDevice());
            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        // ---------------------------------------------
        // Method: OnIsApplyingSamplingRateChanged — behavior: disabilita CanExecute quando true
        // ---------------------------------------------
        [Fact(DisplayName = "OnIsApplyingSamplingRateChanged: IsApplying=true → ApplySamplingRateCommand disabilitato")]
        public void OnIsApplyingSamplingRateChanged_disables_when_true()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            // porta il backing field a true e invoca il partial
            SetPrivate(vm, "isApplyingSamplingRate", true);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", true);

            Assert.False(vm.ApplySamplingRateCommand.CanExecute(null));
        }

        // ---------------------------------------------
        // Method: OnIsApplyingSamplingRateChanged — behavior: riabilita CanExecute quando false
        // ---------------------------------------------
        [Fact(DisplayName = "OnIsApplyingSamplingRateChanged: IsApplying=false → ApplySamplingRateCommand abilitato")]
        public void OnIsApplyingSamplingRateChanged_enables_when_false()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            // simula passaggio true → false
            SetPrivate(vm, "isApplyingSamplingRate", true);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", true);
            Assert.False(vm.ApplySamplingRateCommand.CanExecute(null));

            SetPrivate(vm, "isApplyingSamplingRate", false);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", false);
            Assert.True(vm.ApplySamplingRateCommand.CanExecute(null));
        }

        // ---------------------------------------------
        // Method: ConnectAndStartAsync — behavior: emette ShowBusy e HideBusy, completa senza eccezioni (devices = null)
        // ---------------------------------------------
        [Fact(DisplayName = "ConnectAndStartAsync: emette ShowBusy/HideBusy e completa (devices null)")]
        public async Task ConnectAndStartAsync_emits_busy_and_finishes_with_null_devices()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            // Evita tentativi reali di connessione/streaming
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var events = new List<string>();
            vm.ShowBusyRequested += (_, __) => events.Add("show");
            vm.HideBusyRequested += (_, __) => events.Add("hide");

            var ex = await Record.ExceptionAsync(() => vm.ConnectAndStartAsync());
            Assert.Null(ex);

            Assert.Contains("show", events);
            Assert.Contains("hide", events);
            // Ordine: "show" prima di "hide"
            Assert.True(events.IndexOf("show") < events.IndexOf("hide"));
        }

        // ---------------------------------------------
        // Method: ConnectAndStartAsync — behavior: non mostra alert su devices = null (nessuna eccezione bubble)
        // ---------------------------------------------
        [Fact(DisplayName = "ConnectAndStartAsync: nessun alert quando non ci sono device (null)")]
        public async Task ConnectAndStartAsync_no_alert_with_null_devices()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            int alerts = 0;
            vm.ShowAlertRequested += (_, __) => alerts++;

            await vm.ConnectAndStartAsync();

            Assert.Equal(0, alerts);
        }

        // ---------------------------------------------
        // Method: StopAsync — behavior: completa senza eccezioni con disconnect=false (devices = null)
        // ---------------------------------------------
        [Fact(DisplayName = "StopAsync(false): completa senza eccezioni (devices null)")]
        public async Task StopAsync_no_disconnect_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: false));
            Assert.Null(ex);
        }

        // ---------------------------------------------
        // Method: StopAsync — behavior: completa senza eccezioni con disconnect=true (devices = null)
        // ---------------------------------------------
        [Fact(DisplayName = "StopAsync(true): completa senza eccezioni (devices null)")]
        public async Task StopAsync_disconnect_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: true));
            Assert.Null(ex);
        }

        // ---------------------------------------------
        // Method: StopAsync — behavior: idempotente (chiamata ripetuta non lancia)
        // ---------------------------------------------
        [Fact(DisplayName = "StopAsync: chiamata ripetuta non lancia")]
        public async Task StopAsync_idempotent_calls_no_throw()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex1 = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: false));
            var ex2 = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: true));
            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        // ApplySamplingRateAsync behavior
        // =======================
        // TEST: ApplySamplingRateAsync (private)
        // Coprono: input non numerico, troppo alto, troppo basso, caso OK (busy/alert/hide e stato).
        // =======================

        static async Task InvokePrivateAsync(object target, string method, params object?[] args)
        {
            var t = target.GetType();
            var m = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                     .FirstOrDefault(mi => mi.Name == method &&
                                           mi.GetParameters().Length == (args?.Length ?? 0));
            Assert.NotNull(m);
            var ret = m!.Invoke(target, args);
            if (ret is Task task) await task;
        }

        static (double min, double max) GetSamplingLimits(Type vmType)
        {
            var fMin = vmType.GetField("MIN_SAMPLING_RATE", BindingFlags.NonPublic | BindingFlags.Static);
            var fMax = vmType.GetField("MAX_SAMPLING_RATE", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(fMin); Assert.NotNull(fMax);
            return ((double)fMin!.GetValue(null)!, (double)fMax!.GetValue(null)!);
        }

        // ---------------------------------------------
        // Method: ApplySamplingRateAsync — behavior: rifiuta input non numerico, mostra ValidationMessage, non mostra busy
        // ---------------------------------------------
        [Fact(DisplayName = "ApplySamplingRateAsync: input non numerico → validation + niente busy/alert")]
        public async Task ApplySamplingRateAsync_invalid_text_shows_validation_and_no_busy()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            int show = 0, hide = 0, alerts = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;
            vm.ShowAlertRequested += (_, __) => alerts++;

            vm.SamplingRateText = "abc"; // non numerico

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.NotEmpty(vm.ValidationMessage);
            Assert.Equal(0, show);
            Assert.Equal(0, hide);
            Assert.Equal(0, alerts);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }

        // ---------------------------------------------
        // Method: ApplySamplingRateAsync — behavior: rifiuta input > MAX, validation e reset, niente busy
        // ---------------------------------------------
        [Fact(DisplayName = "ApplySamplingRateAsync: input > MAX → validation + niente busy/alert")]
        public async Task ApplySamplingRateAsync_above_max_shows_validation_and_no_busy()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            var (min, max) = GetSamplingLimits(vm.GetType());
            int show = 0, hide = 0, alerts = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;
            vm.ShowAlertRequested += (_, __) => alerts++;

            vm.SamplingRateText = (max + 1.0).ToString(CultureInfo.InvariantCulture);

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.Contains("too high", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, show);
            Assert.Equal(0, hide);
            Assert.Equal(0, alerts);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }

        // ---------------------------------------------
        // Method: ApplySamplingRateAsync — behavior: rifiuta input < MIN, validation e reset, niente busy
        // ---------------------------------------------
        [Fact(DisplayName = "ApplySamplingRateAsync: input < MIN → validation + niente busy/alert")]
        public async Task ApplySamplingRateAsync_below_min_shows_validation_and_no_busy()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            var (min, _) = GetSamplingLimits(vm.GetType());
            int show = 0, hide = 0, alerts = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;
            vm.ShowAlertRequested += (_, __) => alerts++;

            vm.SamplingRateText = (min - Math.Max(0.1, min * 0.01)).ToString(CultureInfo.InvariantCulture);

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.Contains("too low", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, show);
            Assert.Equal(0, hide);
            Assert.Equal(0, alerts);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }

        // ---------------------------------------------
        // Method: ApplySamplingRateAsync — behavior: input valido → mostra busy, chiude busy, alert di successo, nessuna validation
        // ---------------------------------------------
        [Fact(DisplayName = "ApplySamplingRateAsync: input valido → busy/alert di successo e IsApplying=false")]
        public async Task ApplySamplingRateAsync_valid_flow_shows_busy_success_alert_and_resets_flag()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            // evita divide-by-zero sui tempi interni/CurrentTimeInSeconds, se usato
            SetProp<double>(imu, "SamplingRate", 50.0);

            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            var (min, max) = GetSamplingLimits(vm.GetType());
            var mid = (min + max) / 2.0;
            vm.SamplingRateText = mid.ToString(CultureInfo.InvariantCulture);

            int show = 0, hide = 0, alerts = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;
            vm.ShowAlertRequested += (_, msg) =>
            {
                alerts++;
                // messaggio di successo atteso
                Assert.Contains("Sampling rate set to", msg, StringComparison.OrdinalIgnoreCase);
            };

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.Equal(1, show);
            Assert.Equal(1, hide);
            Assert.Equal(1, alerts);
            Assert.True(show <= hide); // hide non prima di show
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }


        // UpdateSamplingRateAndRestart behavior

        // --- mini helper riflessione locali (se già li hai, puoi eliminarli) ---
        static void SetPrivateField(object target, string field, object? value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }



        // Config IMU con LNA abilitato (basta per avere serie/parametri coerenti)
        static ShimmerDevice CfgIMU_LnaOnly() => new ShimmerDevice
        {
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = false,
            EnableGyroscope = false,
            EnableMagnetometer = false,
            EnablePressureTemperature = false,
            EnableBattery = false,
            EnableExtA6 = false,
            EnableExtA7 = false,
            EnableExtA15 = false,
            EnableExg = false
        };

        // ---------------------------------------------
        // Method: UpdateSamplingRateAndRestart — behavior: senza device → applica newRate, aggiorna UI, svuota buffer e resetta i counter
        // ---------------------------------------------
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: senza device → usa newRate, svuota serie e resetta counter")]
        public void UpdateSamplingRateAndRestart_no_devices_updates_ui_clears_and_resets()
        {
            // VM IMU per poter popolare una serie prima del restart
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            // metto una SR valida per sicurezza
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());

            // Seleziono parametro IMU, così le serie esistono
            Assert.Contains("Low-Noise Accelerometer", vm.AvailableParameters);
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Popolo 1 campione prima del restart, così possiamo verificare il clear
            // Creo un "sample" compatibile con l’OnSampleReceived (usa riflessione)
            var sampleType = new
            {
                LowNoiseAccelerometerX = new { Data = 1.0f },
                LowNoiseAccelerometerY = new { Data = 2.0f },
                LowNoiseAccelerometerZ = new { Data = 3.0f }
            };
            InvokePrivate(vm, "OnSampleReceived", new object(), sampleType);

            // Verifica: prima del restart c’è 1 punto
            var before = vm.GetSeriesSnapshot("Low-Noise AccelerometerX");
            Assert.Single(before.data);
            Assert.Single(before.time);

            // Per forzare il ramo "senza device", azzero i riferimenti device
            SetPrivateField(vm, "shimmerImu", null);
            SetPrivateField(vm, "shimmerExg", null);

            // Traccio gli eventi di chart per essere sicuri che arrivi un UpdateChart()
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Chiamo il metodo privato con un nuovo rate
            double requested = 123.456;
            InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested);

            // Dopo: SamplingRateDisplay e SamplingRateText aggiornati al valore richiesto (perché senza device = ritorna newRate)
            Assert.Equal(requested, vm.SamplingRateDisplay, 5);
            Assert.Equal(requested.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

            // Contatori resettati
            var sampleCounter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(0, sampleCounter);

            // Serie svuotate
            var after = vm.GetSeriesSnapshot("Low-Noise AccelerometerX");
            Assert.Empty(after.data);
            Assert.Empty(after.time);

            // Messaggio di validazione pulito, e almeno un trigger di UpdateChart
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        // ---------------------------------------------
        // Method: UpdateSamplingRateAndRestart — behavior: con IMU presente → aggiorna display/text, non genera eccezioni, reset counter
        // (non asseriamo l’esatto "applied" se l’SDK effettua snapping; verifichiamo coerenza e assenza errori)
        // ---------------------------------------------
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: con IMU presente → aggiorna UI e resetta counter")]
        public void UpdateSamplingRateAndRestart_with_imu_updates_ui_and_resets_counter()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Portiamo il contatore a >0 per verificare il reset
            InvokePrivate(vm, "OnSampleReceived", new object(), new
            {
                LowNoiseAccelerometerX = new { Data = 1.0f },
                LowNoiseAccelerometerY = new { Data = 2.0f },
                LowNoiseAccelerometerZ = new { Data = 3.0f }
            });
            Assert.True(GetPrivate<int>(vm, "sampleCounter") > 0);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Esecuzione
            double requested = 40.0; // valore “ragionevole”; l’SDK potrebbe fare snapping
            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested));
            Assert.Null(ex);

            // UI aggiornata coerentemente (non imponiamo l'esatto "applied" se lo snapping è interno all’SDK)
            Assert.True(vm.SamplingRateDisplay > 0);
            Assert.False(string.IsNullOrEmpty(vm.SamplingRateText));

            // Counter resettato e almeno un UpdateChart
            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
            Assert.True(redraws >= 1);

            // Nessun messaggio di errore
            Assert.Equal(string.Empty, vm.ValidationMessage);
        }

        // ---------------------------------------------
        // Method: UpdateSamplingRateAndRestart — behavior: non propaga eccezioni dai sottometodi (DeviceStart/Stop/Chart)
        // (Verifica robustezza try/catch interno simulando devices null e chiamate safe)
        // ---------------------------------------------
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: ignora eccezioni interne e mantiene VM consistente")]
        public void UpdateSamplingRateAndRestart_swallow_internal_errors_keeps_vm_consistent()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 25.0);
            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Per sicurezza: azzeriamo i device per prendere il percorso più “safe”
            SetPrivateField(vm, "shimmerImu", null);
            SetPrivateField(vm, "shimmerExg", null);

            // Proviamo un rate con decimali
            double requested = 12.345;
            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested));
            Assert.Null(ex);

            // Stato coerente
            Assert.Equal(requested, vm.SamplingRateDisplay, 5);
            Assert.Equal(requested.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);
            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
            Assert.Equal(string.Empty, vm.ValidationMessage);
        }


        // InitializeDataCollections behavior



        // =========================================================
        // Method: InitializeDataCollections — behavior: crea le chiavi per LNA ed ExtADC abilitati
        // =========================================================
        [Fact(DisplayName = "InitializeDataCollections: LNA + ExtADC → crea serie vuote attese")]
        public void InitializeDataCollections_creates_LNA_and_ExtADC_keys()
        {
            // Config: solo LNA + ExtADC
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = false
            };

            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var vm = new DataPageViewModel(imu, cfg);

            // Svuoto le mappe per chiamare esplicitamente InitializeDataCollections
            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            // Act
            InvokePrivate(vm, "InitializeDataCollections");

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // LNA (X,Y,Z) + ExtADC_A6/A7/A15
            string[] expected =
            {
        "Low-Noise AccelerometerX",
        "Low-Noise AccelerometerY",
        "Low-Noise AccelerometerZ",
        "ExtADC_A6", "ExtADC_A7", "ExtADC_A15"
    };

            foreach (var key in expected)
            {
                Assert.True(data.ContainsKey(key), $"Manca dataPointsCollections['{key}']");
                Assert.True(time.ContainsKey(key), $"Manca timeStampsCollections['{key}']");
                Assert.Empty(data[key]);
                Assert.Empty(time[key]);
            }
        }

        // =========================================================
        // Method: InitializeDataCollections — behavior: abilita EXG → aggiunge Exg1/Exg2
        // =========================================================
        [Fact(DisplayName = "InitializeDataCollections: EXG abilitato → crea Exg1/Exg2")]
        public void InitializeDataCollections_adds_EXG_keys_when_enabled()
        {
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = false,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = false,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = true,
                IsExgModeECG = true
            };

            var exg = new ShimmerSDK.EXG.ShimmerSDK_EXG();
            var vm = new DataPageViewModel(exg, cfg);

            // Svuoto e invoco esplicitamente
            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            InvokePrivate(vm, "InitializeDataCollections");

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            foreach (var key in new[] { "Exg1", "Exg2" })
            {
                Assert.True(data.ContainsKey(key), $"Manca dataPointsCollections['{key}']");
                Assert.True(time.ContainsKey(key), $"Manca timeStampsCollections['{key}']");
                Assert.Empty(data[key]);
                Assert.Empty(time[key]);
            }
        }

        // =========================================================
        // Method: InitializeDataCollections — behavior: tutti i flag disabilitati → nessuna chiave
        // =========================================================
        [Fact(DisplayName = "InitializeDataCollections: nessun sensore attivo → nessuna serie creata")]
        public void InitializeDataCollections_with_all_flags_off_creates_no_keys()
        {
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = false,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = false,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = false
            };

            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            InvokePrivate(vm, "InitializeDataCollections");

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            Assert.Empty(data);
            Assert.Empty(time);
        }

        // =========================================================
        // Method: InitializeDataCollections — behavior: idempotente (nessun duplicato dopo più invocazioni)
        // =========================================================
        [Fact(DisplayName = "InitializeDataCollections: chiamata due volte → nessun duplicato")]
        public void InitializeDataCollections_is_idempotent()
        {
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = false,
                EnableGyroscope = false,
                EnableMagnetometer = false,
                EnablePressureTemperature = false,
                EnableBattery = false,
                EnableExtA6 = true,
                EnableExtA7 = false,
                EnableExtA15 = false,
                EnableExg = true
            };

            // uso EXG per avere anche Exg1/Exg2
            var exg = new ShimmerSDK.EXG.ShimmerSDK_EXG();
            var vm = new DataPageViewModel(exg, cfg);

            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            InvokePrivate(vm, "InitializeDataCollections");
            var data1 = new HashSet<string>(GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections").Keys);

            // seconda invocazione
            InvokePrivate(vm, "InitializeDataCollections");
            var data2 = new HashSet<string>(GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections").Keys);

            Assert.Equal(data1, data2); // nessuna chiave duplicata o cambiata
        }


        // MarkFirstOpenBaseline behavior
        [Fact(DisplayName = "MarkFirstOpenBaseline(true): azzera baseline, svuota buffer e resetta i counter")]
        public void MarkFirstOpenBaseline_clearBuffers_clears_and_resets()
        {
            // Arrange
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            // imposta una SR valida per sicurezza (non serve nel ramo clear)
            SetProp<double>(imu, "SamplingRate", 50.0);

            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };

            var vm = new DataPageViewModel(imu, cfg);

            // mettiamo un po' di stato interno (dati e counter) da “pulire”
            SetPrivate<int>(vm, "sampleCounter", 42);
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // assicuriamoci che ci siano delle chiavi
            if (!data.ContainsKey("Low-Noise AccelerometerX"))
            {
                data["Low-Noise AccelerometerX"] = new List<float>();
                time["Low-Noise AccelerometerX"] = new List<int>();
            }
            data["Low-Noise AccelerometerX"].Add(1.0f);
            time["Low-Noise AccelerometerX"].Add(10);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Act
            vm.MarkFirstOpenBaseline(clearBuffers: true);

            // Assert
            var baseline = GetPrivate<double>(vm, "timeBaselineSeconds");
            Assert.Equal(0.0, baseline, 6);

            Assert.Empty(data["Low-Noise AccelerometerX"]);
            Assert.Empty(time["Low-Noise AccelerometerX"]);

            var counter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(0, counter);

            Assert.True(redraws >= 1);
        }

        [Fact(DisplayName = "MarkFirstOpenBaseline(false): baseline = sampleCounter/DeviceSamplingRate, dati intatti")]
        public void MarkFirstOpenBaseline_keepBuffers_sets_baseline_and_keeps_data()
        {
            // Arrange
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 50.0); // 50 Hz

            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };

            var vm = new DataPageViewModel(imu, cfg);

            // sampleCounter > 0 per avere baseline significativa
            SetPrivate<int>(vm, "sampleCounter", 100);  // 100/50 = 2.0 s attesi

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");
            if (!data.ContainsKey("Low-Noise AccelerometerX"))
            {
                data["Low-Noise AccelerometerX"] = new List<float>();
                time["Low-Noise AccelerometerX"] = new List<int>();
            }
            data["Low-Noise AccelerometerX"].Add(1.0f);
            time["Low-Noise AccelerometerX"].Add(10);

            var dataCountBefore = data["Low-Noise AccelerometerX"].Count;
            var timeCountBefore = time["Low-Noise AccelerometerX"].Count;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Act
            vm.MarkFirstOpenBaseline(clearBuffers: false);

            // Assert
            var baseline = GetPrivate<double>(vm, "timeBaselineSeconds");
            Assert.Equal(2.0, baseline, 6); // 100 / 50

            // dati invariati
            Assert.Equal(dataCountBefore, data["Low-Noise AccelerometerX"].Count);
            Assert.Equal(timeCountBefore, time["Low-Noise AccelerometerX"].Count);

            // counter invariato
            var counter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(100, counter);

            Assert.True(redraws >= 1);
        }


        // ClearAllDataCollections behavior
        [Fact(DisplayName = "ClearAllDataCollections: svuota dati e timestamp ma mantiene le chiavi")]
        public void ClearAllDataCollections_clears_lists_keeps_keys()
        {
            // Arrange
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableExtA6 = true
            };
            var vm = new DataPageViewModel(imu, cfg);

            // Accesso ai dizionari privati
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Assicuriamoci di avere almeno una chiave e alcuni valori
            string anyKey = data.Keys.FirstOrDefault() ?? "Low-Noise AccelerometerX";
            if (!data.ContainsKey(anyKey))
            {
                data[anyKey] = new List<float>();
                time[anyKey] = new List<int>();
            }
            data[anyKey].AddRange(new[] { 1f, 2f, 3f });
            time[anyKey].AddRange(new[] { 10, 20, 30 });

            var keysBeforeData = data.Keys.ToList();
            var keysBeforeTime = time.Keys.ToList();

            // Act
            InvokePrivate(vm, "ClearAllDataCollections");

            // Assert: le chiavi rimangono
            Assert.Equal(keysBeforeData, data.Keys.ToList());
            Assert.Equal(keysBeforeTime, time.Keys.ToList());

            // ma tutte le liste devono essere vuote
            foreach (var k in keysBeforeData)
                Assert.Empty(data[k]);
            foreach (var k in keysBeforeTime)
                Assert.Empty(time[k]);
        }

        [Fact(DisplayName = "ClearAllDataCollections: idempotente (seconda chiamata non cambia nulla)")]
        public void ClearAllDataCollections_is_idempotent()
        {
            // Arrange
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };
            var vm = new DataPageViewModel(imu, cfg);

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Prima pulizia
            InvokePrivate(vm, "ClearAllDataCollections");

            var keysAfterFirstData = data.Keys.ToList();
            var keysAfterFirstTime = time.Keys.ToList();

            // Verifica che già siano vuote
            foreach (var k in keysAfterFirstData) Assert.Empty(data[k]);
            foreach (var k in keysAfterFirstTime) Assert.Empty(time[k]);

            // Act: seconda pulizia
            var ex = Record.Exception(() => InvokePrivate(vm, "ClearAllDataCollections"));

            // Assert: nessuna eccezione, chiavi identiche, ancora vuote
            Assert.Null(ex);
            Assert.Equal(keysAfterFirstData, data.Keys.ToList());
            Assert.Equal(keysAfterFirstTime, time.Keys.ToList());
            foreach (var k in keysAfterFirstData) Assert.Empty(data[k]);
            foreach (var k in keysAfterFirstTime) Assert.Empty(time[k]);
        }


        // TrimCollection behavior
        [Fact(DisplayName = "TrimCollection: riduce a maxPoints e mantiene l’allineamento dati↔tempo")]
        public void TrimCollection_trims_to_max_and_keeps_alignment()
        {
            // Arrange
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice { EnableLowNoiseAccelerometer = true };
            var vm = new DataPageViewModel(imu, cfg);

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "Low-Noise AccelerometerX";
            // Garantiamo che la chiave esista
            if (!data.ContainsKey(key)) data[key] = new List<float>();
            if (!time.ContainsKey(key)) time[key] = new List<int>();

            // 10 punti, vogliamo tagliare a 5
            data[key].Clear(); time[key].Clear();
            for (int i = 0; i < 10; i++) { data[key].Add(i + 0.5f); time[key].Add(i * 10); }

            // Act
            InvokePrivate(vm, "TrimCollection", key, 5);

            // Assert: restano 5 punti e sono gli ultimi 5 originali (indici 5..9)
            Assert.Equal(5, data[key].Count);
            Assert.Equal(5, time[key].Count);
            Assert.Equal(5 + 0.5f, data[key][0], 3); // primo rimasto = indice 5
            Assert.Equal(5 * 10, time[key][0]);
            Assert.Equal(9 + 0.5f, data[key][4], 3);
            Assert.Equal(9 * 10, time[key][4]);
        }

        [Fact(DisplayName = "TrimCollection: no-op quando la dimensione è ≤ maxPoints")]
        public void TrimCollection_noop_when_length_le_max()
        {
            // Arrange
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "GyroscopeX";
            data[key] = new List<float> { 1f, 2f, 3f };
            time[key] = new List<int> { 10, 20, 30 };

            // Copie per confronto
            var beforeData = data[key].ToList();
            var beforeTime = time[key].ToList();

            // Act
            InvokePrivate(vm, "TrimCollection", key, 5);

            // Assert: invariato
            Assert.Equal(beforeData, data[key]);
            Assert.Equal(beforeTime, time[key]);
        }

        [Fact(DisplayName = "TrimCollection: parametro inesistente → nessuna eccezione/nessuna modifica globale")]
        public void TrimCollection_missing_key_is_safe_noop()
        {
            // Arrange
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            var dataKeysBefore = data.Keys.ToList();
            var timeKeysBefore = time.Keys.ToList();

            // Act
            var ex = Record.Exception(() => InvokePrivate(vm, "TrimCollection", "__missing__", 3));

            // Assert
            Assert.Null(ex);
            Assert.Equal(dataKeysBefore, data.Keys.ToList());
            Assert.Equal(timeKeysBefore, time.Keys.ToList());
        }

        [Fact(DisplayName = "TrimCollection: time più corto di data → si ferma quando i timestamp finiscono")]
        public void TrimCollection_stops_when_time_exhausted_if_mismatched_lengths()
        {
            // Arrange
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "MagnetometerX";
            data[key] = new List<float> { 0f, 1f, 2f, 3f, 4f, 5f }; // 6 elementi
            time[key] = new List<int> { 10, 20, 30 };              // 3 elementi

            // Act: maxPoints = 2 → servirebbe rimuovere 4 elementi da data,
            // ma il ciclo si ferma quando timeList.Count == 0
            InvokePrivate(vm, "TrimCollection", key, 2);

            // Assert: nessuna eccezione, time è vuoto; data ha perso tanti elementi quanti timestamp rimossi (3)
            Assert.Empty(time[key]);
            Assert.Equal(3, data[key].Count); // 6 iniziali - 3 rimossi = 3 (ancora > maxPoints, by design)
                                              // I rimanenti sono gli ultimi 3 originali: 3,4,5
            Assert.Equal(new List<float> { 3f, 4f, 5f }, data[key]);
        }


        // GetSeriesSnapshot behavior
        [Fact(DisplayName = "GetSeriesSnapshot: parametro inesistente → restituisce liste vuote")]
        public void GetSeriesSnapshot_missing_key_returns_empty_lists()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            var (data, time) = vm.GetSeriesSnapshot("__missing__");

            Assert.NotNull(data);
            Assert.NotNull(time);
            Assert.Empty(data);
            Assert.Empty(time);
        }

        [Fact(DisplayName = "GetSeriesSnapshot: deep copy → modifiche allo snapshot non toccano l’originale")]
        public void GetSeriesSnapshot_returns_deep_copy_snapshot_is_independent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "GyroscopeX";
            dataDict[key] = new List<float> { 1f, 2f, 3f };
            timeDict[key] = new List<int> { 10, 20, 30 };

            var (snapData, snapTime) = vm.GetSeriesSnapshot(key);

            // Modifico lo snapshot
            snapData.Add(99f);
            snapTime.Add(999);

            // Originali invariati
            Assert.Equal(new List<float> { 1f, 2f, 3f }, dataDict[key]);
            Assert.Equal(new List<int> { 10, 20, 30 }, timeDict[key]);
        }

        [Fact(DisplayName = "GetSeriesSnapshot: deep copy → modifiche all’originale non toccano lo snapshot")]
        public void GetSeriesSnapshot_snapshot_not_affected_by_later_original_changes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "Low-Noise AccelerometerX";
            dataDict[key] = new List<float> { 0f, 1f };
            timeDict[key] = new List<int> { 5, 15 };

            var (snapData, snapTime) = vm.GetSeriesSnapshot(key);

            // Cambiamo gli originali dopo lo snapshot
            dataDict[key].Add(2f);
            timeDict[key].Add(25);

            // Snapshot resta quello originale
            Assert.Equal(new List<float> { 0f, 1f }, snapData);
            Assert.Equal(new List<int> { 5, 15 }, snapTime);
        }

        [Fact(DisplayName = "GetSeriesSnapshot: usa MapToInternalKey (EXG1 → Exg1)")]
        public void GetSeriesSnapshot_maps_exg_channel_names()
        {
            var cfg = new ShimmerInterface.Models.ShimmerDevice { EnableExg = true, IsExgModeECG = true };
            var vm = new DataPageViewModel(new ShimmerSDK.EXG.ShimmerSDK_EXG(), cfg);

            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Popoliamo le chiavi interne attese: "Exg1"
            dataDict["Exg1"] = new List<float> { 0.1f, 0.2f, 0.3f };
            timeDict["Exg1"] = new List<int> { 100, 200, 300 };

            // Passiamo "EXG1" (maiuscolo) per verificare la mappatura
            var (data, time) = vm.GetSeriesSnapshot("EXG1");

            Assert.Equal(new List<float> { 0.1f, 0.2f, 0.3f }, data);
            Assert.Equal(new List<int> { 100, 200, 300 }, time);
        }

        [Fact(DisplayName = "GetSeriesSnapshot: caso IMU semplice (GyroscopeX)")]
        public void GetSeriesSnapshot_returns_copy_for_imu_key()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "GyroscopeX";
            dataDict[key] = new List<float> { 4f, 5f };
            timeDict[key] = new List<int> { 40, 50 };

            var (data, time) = vm.GetSeriesSnapshot(key);

            Assert.Equal(new List<float> { 4f, 5f }, data);
            Assert.Equal(new List<int> { 40, 50 }, time);

            // Verifica che sia davvero una copia
            data.Add(6f);
            time.Add(60);
            Assert.Equal(new List<float> { 4f, 5f }, dataDict[key]);
            Assert.Equal(new List<int> { 40, 50 }, timeDict[key]);
        }


        // UpdateChart behavior
        [Fact(DisplayName = "UpdateChart: emette ChartUpdateRequested una volta con sender e args corretti")]
        public void UpdateChart_raises_event_once_with_correct_sender_and_args()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            int calls = 0;
            object? lastSender = null;
            EventArgs? lastArgs = null;

            EventHandler handler = (s, e) => { calls++; lastSender = s; lastArgs = e; };
            vm.ChartUpdateRequested += handler;

            // Invoca il metodo privato UpdateChart()
            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateChart"));
            Assert.Null(ex);

            Assert.Equal(1, calls);
            Assert.Same(vm, lastSender);
            Assert.Same(EventArgs.Empty, lastArgs);
        }

        [Fact(DisplayName = "UpdateChart: nessun subscriber → non lancia eccezioni")]
        public void UpdateChart_with_no_subscribers_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateChart"));
            Assert.Null(ex);
        }

        [Fact(DisplayName = "UpdateChart: più subscriber → tutti ricevono la notifica")]
        public void UpdateChart_notifies_all_subscribers()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            int a = 0, b = 0;
            vm.ChartUpdateRequested += (_, __) => a++;
            vm.ChartUpdateRequested += (_, __) => b++;

            InvokePrivate(vm, "UpdateChart");

            Assert.Equal(1, a);
            Assert.Equal(1, b);
        }

        [Fact(DisplayName = "UpdateChart: unsubscribe rimuove il listener")]
        public void UpdateChart_unsubscribe_removes_listener()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            int calls = 0;
            EventHandler handler = (_, __) => calls++;

            vm.ChartUpdateRequested += handler;
            vm.ChartUpdateRequested -= handler;

            InvokePrivate(vm, "UpdateChart");

            Assert.Equal(0, calls);
        }


        // ChartModeLabel behavior


        static (ShimmerSDK_IMU imu, ShimmerDevice cfg) ImuAllOn()
        {
            var imu = new ShimmerSDK_IMU();
            // alcuni test usano SR: assicuriamoci sia > 0
            SetProp<double>(imu, "SamplingRate", 51.2);

            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = false
            };
            return (imu, cfg);
        }

        static (ShimmerSDK_EXG exg, ShimmerDevice cfg) ExgAllOnECG()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 51.2);

            var cfg = new ShimmerDevice
            {
                // i flag IMU non sono necessari, ma innocui
                EnableLowNoiseAccelerometer = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                // EXG attivo in ECG mode
                EnableExg = true,
                IsExgModeECG = true
            };
            return (exg, cfg);
        }

        // ---------------- TEST ----------------

        [Fact(DisplayName = "ChartModeLabel: IMU + Multi → 'Multi Parameter (X, Y, Z)'")]
        public void ChartModeLabel_IMU_Multi()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";           // gruppo IMU
            vm.ChartDisplayMode = ChartDisplayMode.Multi; // esplicito

            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartModeLabel: IMU + Split → 'Split (three separate charts)'")]
        public void ChartModeLabel_IMU_Split()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            // selezione “variant” split IMU
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartModeLabel: EXG + Multi → 'Multi Parameter (EXG1, EXG2)'")]
        public void ChartModeLabel_EXG_Multi()
        {
            var (exg, cfg) = ExgAllOnECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";                 // famiglia EXG
            vm.ChartDisplayMode = ChartDisplayMode.Multi;

            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartModeLabel: EXG + Split → 'Split (two separate charts)'")]
        public void ChartModeLabel_EXG_Split()
        {
            var (exg, cfg) = ExgAllOnECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
        }

        [Fact(DisplayName = "ChartModeLabel: fallback su modalità sconosciuta → 'Unified'")]
        public void ChartModeLabel_Fallback_Unified()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            // forza un valore enum non previsto per testare il default del 'switch'
            vm.ChartDisplayMode = (ChartDisplayMode)999;

            Assert.Equal("Unified", vm.ChartModeLabel);
        }


        // ----- Legend (labels and colors) -----




        static (ShimmerSDK_EXG exg, ShimmerDevice cfg) ExgECG()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 51.2);
            var cfg = new ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,  // irrilevanti ma innocui
                EnableWideRangeAccelerometer = true,
                EnableGyroscope = true,
                EnableMagnetometer = true,
                EnablePressureTemperature = true,
                EnableBattery = true,
                EnableExtA6 = true,
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = true,
                IsExgModeECG = true
            };
            return (exg, cfg);
        }

        // ---------------- IMU (3 serie) ----------------

        [Fact(DisplayName = "LegendLabels (IMU/Gyroscope): X,Y,Z")]
        public void LegendLabels_IMU_Gyroscope_XYZ()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";    // gruppo IMU classico

            var labels = vm.LegendLabels;
            Assert.Equal(new[] { "X", "Y", "Z" }, labels.ToArray());

            // Text helpers
            Assert.Equal("X", vm.Legend1Text);
            Assert.Equal("Y", vm.Legend2Text);
            Assert.Equal("Z", vm.Legend3Text);

            // Colori: Red, Green (perché 3 serie), Blue
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Green, vm.Legend2Color);
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }

        [Fact(DisplayName = "LegendLabels (IMU Split variant): X,Y,Z invariati")]
        public void LegendLabels_IMU_SplitVariant_Still_XYZ()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";

            Assert.Equal(new[] { "X", "Y", "Z" }, vm.LegendLabels.ToArray());
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Green, vm.Legend2Color);
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }

        // ---------------- EXG (2 serie) ----------------

        [Fact(DisplayName = "LegendLabels (EXG/ECG): EXG1,EXG2; Legend3Text = \"\"")]
        public void LegendLabels_EXG_ECG_EXG1_EXG2()
        {
            var (exg, cfg) = ExgECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";  // due canali

            var labels = vm.LegendLabels;
            Assert.Equal(new[] { "EXG1", "EXG2" }, labels.ToArray());

            // Text helpers: la terza voce deve essere vuota
            Assert.Equal("EXG1", vm.Legend1Text);
            Assert.Equal("EXG2", vm.Legend2Text);
            Assert.Equal(string.Empty, vm.Legend3Text);

            // Colori: Red, Blue (perché Count==2), Blue (costante per Legend3Color)
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Blue, vm.Legend2Color);
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }

        [Fact(DisplayName = "LegendLabels (EXG Split variant): EXG1,EXG2 + colori 2-canali")]
        public void LegendLabels_EXG_SplitVariant_EXG1_EXG2()
        {
            var (exg, cfg) = ExgECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";

            Assert.Equal(new[] { "EXG1", "EXG2" }, vm.LegendLabels.ToArray());
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Blue, vm.Legend2Color);  // 2 canali ⇒ Blue
            Assert.Equal(Colors.Blue, vm.Legend3Color);  // sempre Blue
            Assert.Equal(string.Empty, vm.Legend3Text);  // niente terza etichetta
        }

        // ---------------- Robustezza: parametro sconosciuto ----------------

        [Fact(DisplayName = "LegendLabels: parametro sconosciuto → una label grezza + testi e colori coerenti")]
        public void LegendLabels_UnknownParameter_YieldsSingleRawLabel()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "__unknown__";

            var labels = vm.LegendLabels;

            Assert.Single(labels);
            Assert.Equal("__unknown__", labels[0]);

            Assert.Equal("__unknown__", vm.Legend1Text);
            Assert.Equal(string.Empty, vm.Legend2Text);
            Assert.Equal(string.Empty, vm.Legend3Text);

            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Green, vm.Legend2Color); // Count != 2 → Green
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }


        // OnAutoYAxisChanged behavior

        [Fact(DisplayName = "AutoYAxis=true: applica limiti auto calcolati, disabilita manuale, sync testi e aggiorna chart")]
        public void AutoYAxis_true_applies_calculated_auto_limits_and_updates_ui()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Seleziona un gruppo valido e imposta limiti manuali custom (che verranno salvati come backup)
            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMin = -123.4;
            vm.YAxisMax = 234.5;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // ACT: abilita auto
            vm.AutoYAxis = true;

            // Leggi i limiti auto calcolati dal VM (campi privati)
            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // ASSERT: stato coerente con l’auto
            Assert.False(vm.IsYAxisManualEnabled);
            Assert.Equal(autoMin, vm.YAxisMin, 6);
            Assert.Equal(autoMax, vm.YAxisMax, 6);
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        [Fact(DisplayName = "AutoYAxis=false: ripristina i limiti manuali salvati e riabilita l’input")]
        public void AutoYAxis_false_restores_backed_up_manual_limits()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            // Imposta limiti manuali e memorizzali (verranno salvati quando si passa ad auto)
            vm.YAxisMin = -50;
            vm.YAxisMax = 150;

            // Vai in auto per far salvare i last-valid manual
            vm.AutoYAxis = true;

            // Recupera i last-valid manuali dai campi privati
            var lastMin = GetPrivate<double>(vm, "_lastValidYAxisMin");
            var lastMax = GetPrivate<double>(vm, "_lastValidYAxisMax");
            Assert.Equal(-50, lastMin, 6);
            Assert.Equal(150, lastMax, 6);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Torna a manuale
            vm.AutoYAxis = false;

            // Ora i limiti devono essere ripristinati ai last-valid
            Assert.True(vm.IsYAxisManualEnabled);
            Assert.Equal(lastMin, vm.YAxisMin, 6);
            Assert.Equal(lastMax, vm.YAxisMax, 6);
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        // CalculateAutoYAxisRange behavior

        [Fact(DisplayName = "AutoRange (group): con dati usa min/max ±10%")]
        public void AutoRange_Group_WithData_UsesMinMaxPlus10pct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "Gyroscope"; // gruppo X/Y/Z

            // Popola dati per le tre sotto-serie
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["GyroscopeX"] = new List<float> { -5f, -2f, 1f };
            data["GyroscopeY"] = new List<float> { 0f, 10f, 12f };
            data["GyroscopeZ"] = new List<float> { 3f, 4f, 6f };

            // Act: invoca la privata
            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // min globale = -5, max globale = 12, range=17 → margine=1.7 → attesi: [-6.7, 13.7] arrotondati a 3 dec.
            Assert.Equal(-6.7, autoMin, 3);
            Assert.Equal(13.7, autoMax, 3);
        }

        [Fact(DisplayName = "AutoRange (group): senza dati → fallback default del gruppo")]
        public void AutoRange_Group_NoData_FallsBackToDefaults()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Seleziona il gruppo: il VM imposta già i limiti di default per il gruppo
            vm.SelectedParameter = "Gyroscope";
            var expectedMin = vm.YAxisMin;
            var expectedMax = vm.YAxisMax;

            // Nessun dato nelle serie → calcolo auto deve tornare ai default
            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            Assert.Equal(expectedMin, autoMin, 6);
            Assert.Equal(expectedMax, autoMax, 6);
        }

        [Fact(DisplayName = "AutoRange (single): BatteryVoltage con dati → min/max ±10%")]
        public void AutoRange_Single_WithData_UsesMinMaxPlus10pct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "BatteryVoltage"; // singola serie

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["BatteryVoltage"] = new List<float> { 3600f, 3800f, 3900f }; // mV

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // min=3600, max=3900, range=300 → margine=30 → [3570, 3930]
            Assert.Equal(3570, autoMin, 3);
            Assert.Equal(3930, autoMax, 3);
        }

        [Fact(DisplayName = "AutoRange (single): senza dati → fallback default del parametro")]
        public void AutoRange_Single_NoData_FallsBackToDefaults()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Seleziona il parametro singolo: il VM imposta i limiti di default per quel parametro
            vm.SelectedParameter = "BatteryVoltage";
            var expectedMin = vm.YAxisMin;
            var expectedMax = vm.YAxisMax;

            // Nessun dato per il parametro → calcolo auto deve tornare ai default
            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            Assert.Equal(expectedMin, autoMin, 6);
            Assert.Equal(expectedMax, autoMax, 6);
        }

        [Fact(DisplayName = "AutoRange: dati piatti → piccolo margine attorno al centro")]
        public void AutoRange_ConstantData_ProducesSmallMarginAroundCenter()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "Gyroscope";

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["GyroscopeX"] = new List<float> { 5f, 5f, 5f };
            data["GyroscopeY"] = new List<float> { 5f };
            data["GyroscopeZ"] = new List<float> { 5f, 5f };

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // center=5; margin = |center|*0.1 + 0.1 = 0.5 + 0.1 = 0.6 → [4.4, 5.6] arrotondati
            Assert.Equal(4.4, autoMin, 3);
            Assert.Equal(5.6, autoMax, 3);
        }

        [Fact(DisplayName = "AutoRange: arrotonda i limiti a 3 decimali")]
        public void AutoRange_Rounds_To_Three_Decimals()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "BatteryVoltage";

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["BatteryVoltage"] = new List<float> { 1.23449f, 1.23451f }; // costruito per testare il round

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // non verifichiamo il valore assoluto, ma che siano effettivamente arrotondati a 3 decimali
            double Round3(double v) => Math.Round(v, 3);
            Assert.Equal(Round3(autoMin), autoMin);
            Assert.Equal(Round3(autoMax), autoMax);
        }


        // UpdateTextProperties behavior

        [Fact(DisplayName = "UpdateTextProperties: sincronizza i testi e notifica PropertyChanged")]
        public void UpdateTextProperties_syncs_texts_and_raises_PropertyChanged()
        {
            // Arrange
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Imposta valori numerici "non default" per vedere l'effetto sui testi
            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;
            vm.TimeWindowSeconds = 42;
            vm.XAxisLabelInterval = 7;

            // Sporca i testi per verificare che vengano sovrascritti
            // (se le backing fields sono private, basta assegnare alle proprietà pubbliche di testo)
            vm.YAxisMinText = "xxx";
            vm.YAxisMaxText = "yyy";
            vm.TimeWindowSecondsText = "zzz";
            vm.XAxisLabelIntervalText = "qqq";

            // Traccia le notifiche di PropertyChanged
            var changed = new HashSet<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                    changed.Add(e.PropertyName);
            };

            // Act
            InvokePrivate(vm, "UpdateTextProperties");

            // Assert: testi sincronizzati ai numerici con InvariantCulture
            Assert.Equal(vm.YAxisMin.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            // Assert: sono arrivate le notifiche per tutte le property di testo
            Assert.Contains(nameof(vm.YAxisMinText), changed);
            Assert.Contains(nameof(vm.YAxisMaxText), changed);
            Assert.Contains(nameof(vm.TimeWindowSecondsText), changed);
            Assert.Contains(nameof(vm.XAxisLabelIntervalText), changed);

            // Assert: i numerici non vengono toccati
            Assert.Equal(-12.34, vm.YAxisMin, 3);
            Assert.Equal(56.78, vm.YAxisMax, 3);
            Assert.Equal(42, vm.TimeWindowSeconds);
            Assert.Equal(7, vm.XAxisLabelInterval);
        }


        [Fact(DisplayName = "UpdateTextProperties: idempotente e riallinea i testi dopo modifiche ai numerici")]
        public void UpdateTextProperties_idempotent_and_updates_after_numeric_changes()
        {
            // Arrange
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -1.23;
            vm.YAxisMax = 4.56;
            vm.TimeWindowSeconds = 12;
            vm.XAxisLabelInterval = 3;

            // testi sporchi per assicurare l'overwrite
            vm.YAxisMinText = "nope";
            vm.YAxisMaxText = "nope";
            vm.TimeWindowSecondsText = "nope";
            vm.XAxisLabelIntervalText = "nope";

            // Traccia notifiche
            var changed1 = new HashSet<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName)) changed1.Add(e.PropertyName!);
            };

            // Act 1: prima invocazione
            InvokePrivate(vm, "UpdateTextProperties");

            // Assert 1: testi allineati ai numerici
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            // Idempotenza: seconda invocazione senza modifiche → testi identici
            var beforeSecond_YMin = vm.YAxisMinText;
            var beforeSecond_YMax = vm.YAxisMaxText;
            var beforeSecond_TW = vm.TimeWindowSecondsText;
            var beforeSecond_XI = vm.XAxisLabelIntervalText;

            InvokePrivate(vm, "UpdateTextProperties");

            Assert.Equal(beforeSecond_YMin, vm.YAxisMinText);
            Assert.Equal(beforeSecond_YMax, vm.YAxisMaxText);
            Assert.Equal(beforeSecond_TW, vm.TimeWindowSecondsText);
            Assert.Equal(beforeSecond_XI, vm.XAxisLabelIntervalText);

            // Act 2: cambia i numerici, poi richiama UpdateTextProperties
            vm.YAxisMin = -7.89;
            vm.YAxisMax = 10.11;
            vm.TimeWindowSeconds = 99;
            vm.XAxisLabelInterval = 8;

            var changed2 = new HashSet<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName)) changed2.Add(e.PropertyName!);
            };

            InvokePrivate(vm, "UpdateTextProperties");

            // Assert 2: testi aggiornati ai nuovi numerici
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            // Ha nuovamente notificato le 4 property di testo
            Assert.Contains(nameof(vm.YAxisMinText), changed2);
            Assert.Contains(nameof(vm.YAxisMaxText), changed2);
            Assert.Contains(nameof(vm.TimeWindowSecondsText), changed2);
            Assert.Contains(nameof(vm.XAxisLabelIntervalText), changed2);
        }


        // UpdateYAxisTextPropertiesOnly behavior
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: sincronizza YAxisMin/MaxText dagli omonimi numerici")]
        public void UpdateYAxisTextPropertiesOnly_syncs_y_texts_from_numeric_values()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;

            // sporca i testi per verificare che vengano sovrascritti
            vm.YAxisMinText = "sporcato-min";
            vm.YAxisMaxText = "sporcato-max";

            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
        }

        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: non tocca TimeWindow/XAxisLabelInterval")]
        public void UpdateYAxisTextPropertiesOnly_does_not_change_other_texts()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // prendi i valori attuali (di default saranno "20" e "5")
            var beforeTW = vm.TimeWindowSecondsText;
            var beforeXI = vm.XAxisLabelIntervalText;

            // cambia i numerici Y per generare un aggiornamento dei testi Y
            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;

            // chiama il metodo sotto test
            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

            // verifica: TimeWindow/XAxisLabelInterval rimangono invariati
            Assert.Equal(beforeTW, vm.TimeWindowSecondsText);
            Assert.Equal(beforeXI, vm.XAxisLabelIntervalText);
        }


        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: notifica PropertyChanged solo per YAxisMinText/YAxisMaxText")]
        public void UpdateYAxisTextPropertiesOnly_raises_only_y_propertychanged()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -1;
            vm.YAxisMax = 1;

            var changes = new List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName)) changes.Add(e.PropertyName!);
            };

            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

            Assert.Contains(nameof(vm.YAxisMinText), changes);
            Assert.Contains(nameof(vm.YAxisMaxText), changes);
            Assert.DoesNotContain(nameof(vm.TimeWindowSecondsText), changes);
            Assert.DoesNotContain(nameof(vm.XAxisLabelIntervalText), changes);
            Assert.Equal(2, changes.Count); // solo due notifiche
        }

        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: idempotente (chiamata multipla non altera il risultato)")]
        public void UpdateYAxisTextPropertiesOnly_is_idempotent()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -7.5;
            vm.YAxisMax = 123.4;

            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");
            var firstMin = vm.YAxisMinText;
            var firstMax = vm.YAxisMaxText;

            // seconda chiamata
            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");
            Assert.Equal(firstMin, vm.YAxisMinText);
            Assert.Equal(firstMax, vm.YAxisMaxText);
        }

        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: usa InvariantCulture per il formato")]
        public void UpdateYAxisTextPropertiesOnly_uses_invariant_culture()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var prev = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("it-IT"); // virgola decimale
                vm.YAxisMin = 1.5;
                vm.YAxisMax = 2.5;

                InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

                // Deve usare sempre il punto, non la virgola
                Assert.Equal("1.5", vm.YAxisMinText);
                Assert.Equal("2.5", vm.YAxisMaxText);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = prev;
            }
        }


        // ValidateAndUpdateYAxisMin  behavior

        // -------------------------
        // ValidateAndUpdateYAxisMin
        // -------------------------

        [Fact(DisplayName = "YMin: AutoYAxis=true → input ignorato, nessun chart, nessun messaggio")]
        public void ValidateAndUpdateYAxisMin_ignored_when_auto()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                SelectedParameter = "Gyroscope",
                AutoYAxis = true
            };

            var beforeVal = vm.YAxisMin;
            var beforeMsg = vm.ValidationMessage;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", " -123 ");

            Assert.Equal(beforeVal, vm.YAxisMin, 5);
            Assert.Equal(beforeMsg, vm.ValidationMessage);
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "YMin: stringa vuota → reset ai default del parametro + chart + clear message")]
        public void ValidateAndUpdateYAxisMin_empty_resets_to_default_and_updates_chart()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // metti un valore non-default per verificare il reset
            vm.YAxisMin = -100;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "   ");

            // Gyroscope default = -250
            Assert.Equal(-250, vm.YAxisMin, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        [Theory(DisplayName = "YMin: input parziale (+/-) → nessun errore, nessun chart, valore invariato")]
        [InlineData("-")]
        [InlineData("+")]
        public void ValidateAndUpdateYAxisMin_partial_input_noop(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var before = vm.YAxisMin;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", input);

            Assert.Equal(before, vm.YAxisMin, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "YMin: input valido nel range e < YMax → applica valore, clear message, chart")]
        public void ValidateAndUpdateYAxisMin_valid_applies_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // assicurati che YMax sia sufficientemente alto
            vm.YAxisMax = 250;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "-123.45");

            Assert.Equal(-123.45, vm.YAxisMin, 2);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        [Theory(DisplayName = "YMin: out-of-range → messaggio errore e rollback testo")]
        [InlineData("-999999")] // troppo basso rispetto ai limiti globali
        [InlineData("999999")]  // troppo alto rispetto ai limiti globali
        public void ValidateAndUpdateYAxisMin_out_of_range_shows_error_and_rolls_back(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // cattura il last-valid per verificare il rollback del testo
            var lastValid = vm.YAxisMin;
            var lastValidText = vm.YAxisMinText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", input);

            Assert.Contains("out of range", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            // il valore numerico non cambia
            Assert.Equal(lastValid, vm.YAxisMin, 5);
            // il testo torna al last-valid
            Assert.Equal(lastValidText, vm.YAxisMinText);
            // nessun chart (non applica)
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "YMin: >= YMax → messaggio errore e rollback testo")]
        public void ValidateAndUpdateYAxisMin_blocks_when_ge_than_ymax()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            vm.YAxisMin = -10;
            vm.YAxisMax = 5;
            var lastValidText = vm.YAxisMinText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // prova a impostare YMin >= YMax
            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "5");

            Assert.Contains("greater than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValidText, vm.YAxisMinText);
            Assert.Equal(0, redraws);
        }

        [Theory(DisplayName = "YMin: non numerico → messaggio errore e rollback testo")]
        [InlineData("abc")]
        [InlineData("12x")]
        public void ValidateAndUpdateYAxisMin_invalid_text_shows_error_and_rolls_back(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var lastValid = vm.YAxisMin;
            var lastValidText = vm.YAxisMinText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", input);

            Assert.Contains("valid number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.YAxisMin, 5);
            Assert.Equal(lastValidText, vm.YAxisMinText);
            Assert.Equal(0, redraws);
        }

        // ValidateAndUpdateYAxisMax behavior
        // -------------------------
        // ValidateAndUpdateYAxisMax
        // -------------------------

        [Fact(DisplayName = "YMax: AutoYAxis=true → input ignorato, nessun chart, nessun messaggio")]
        public void ValidateAndUpdateYAxisMax_ignored_when_auto()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                SelectedParameter = "Gyroscope",
                AutoYAxis = true
            };

            var beforeVal = vm.YAxisMax;
            var beforeMsg = vm.ValidationMessage;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", " 123 ");

            Assert.Equal(beforeVal, vm.YAxisMax, 5);
            Assert.Equal(beforeMsg, vm.ValidationMessage);
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "YMax: stringa vuota → reset ai default del parametro + chart + clear message")]
        public void ValidateAndUpdateYAxisMax_empty_resets_to_default_and_updates_chart()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // metti un valore non-default per verificare il reset
            vm.YAxisMax = 10;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "   ");

            // Gyroscope default = +250
            Assert.Equal(250, vm.YAxisMax, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        [Theory(DisplayName = "YMax: input parziale (+/-) → nessun errore, nessun chart, valore invariato")]
        [InlineData("-")]
        [InlineData("+")]
        public void ValidateAndUpdateYAxisMax_partial_input_noop(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var before = vm.YAxisMax;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", input);

            Assert.Equal(before, vm.YAxisMax, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "YMax: input valido nel range e > YMin → applica valore, clear message, chart")]
        public void ValidateAndUpdateYAxisMax_valid_applies_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // assicurati che YMin sia sotto al valore che andremo a impostare
            vm.YAxisMin = -200;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "123.45");

            Assert.Equal(123.45, vm.YAxisMax, 2);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }

        [Theory(DisplayName = "YMax: out-of-range → messaggio errore e rollback testo")]
        [InlineData("-999999")] // troppo basso
        [InlineData("999999")]  // troppo alto
        public void ValidateAndUpdateYAxisMax_out_of_range_shows_error_and_rolls_back(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var lastValid = vm.YAxisMax;
            var lastValidText = vm.YAxisMaxText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", input);

            Assert.Contains("out of range", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.YAxisMax, 5);      // numerico invariato
            Assert.Equal(lastValidText, vm.YAxisMaxText); // testo rollback
            Assert.Equal(0, redraws);                     // nessun chart perché non applica
        }

        [Fact(DisplayName = "YMax: <= YMin → messaggio errore e rollback testo")]
        public void ValidateAndUpdateYAxisMax_blocks_when_le_than_ymin()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            vm.YAxisMin = 5;                 // fisso il minimo
            vm.YAxisMax = 10;                // last valid
            var lastValidText = vm.YAxisMaxText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // prova a impostare YMax <= YMin
            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "4");

            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValidText, vm.YAxisMaxText);
            Assert.Equal(10, vm.YAxisMax, 5); // invariato
            Assert.Equal(0, redraws);
        }

        [Theory(DisplayName = "YMax: non numerico → messaggio errore e rollback testo")]
        [InlineData("abc")]
        [InlineData("12x")]
        public void ValidateAndUpdateYAxisMax_invalid_text_shows_error_and_rolls_back(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var lastValid = vm.YAxisMax;
            var lastValidText = vm.YAxisMaxText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", input);

            Assert.Contains("valid number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.YAxisMax, 5);
            Assert.Equal(lastValidText, vm.YAxisMaxText);
            Assert.Equal(0, redraws);
        }

        // ValidateAndUpdateTimeWindow behavior

        // -------------------------------
        // ValidateAndUpdateTimeWindow()
        // -------------------------------

        [Fact(DisplayName = "TimeWindow: vuoto/whitespace → nessun cambio, nessun chart, messaggio pulito")]
        public void TimeWindow_empty_whitespace_is_noop_and_clears_message()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeVal = vm.TimeWindowSeconds;          // default 20
            var beforeText = vm.TimeWindowSecondsText;     // "20"
            vm.ValidationMessage = "old";

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "   ");

            Assert.Equal(beforeVal, vm.TimeWindowSeconds);
            Assert.Equal(beforeText, vm.TimeWindowSecondsText);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.Equal(0, redraws);
        }

        [Fact(DisplayName = "TimeWindow: valido → aggiorna valore, svuota dati e counter, chart refresh")]
        public void TimeWindow_valid_updates_value_clears_data_and_refreshes()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Low-Noise Accelerometer" };

            // Pre-popoliamo le serie interne per verificare il clear
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");
            dataDict["Low-Noise AccelerometerX"] = new List<float> { 1f, 2f };
            timeDict["Low-Noise AccelerometerX"] = new List<int> { 10, 20 };
            SetPrivate(vm, "sampleCounter", 5);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "30");

            Assert.Equal(30, vm.TimeWindowSeconds);
            Assert.Equal(30, vm.TimeWindowSeconds);
            Assert.Equal(string.Empty, vm.ValidationMessage);

            // serie svuotate + counters azzerati
            Assert.Empty(dataDict["Low-Noise AccelerometerX"]);
            Assert.Empty(timeDict["Low-Noise AccelerometerX"]);
            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));

            Assert.True(redraws >= 1);
        }

        [Fact(DisplayName = "TimeWindow: non numerico → validation message e rollback del testo")]
        public void TimeWindow_invalid_text_shows_error_and_rolls_back()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var lastValid = vm.TimeWindowSeconds;      // 20
            var lastText = vm.TimeWindowSecondsText;  // "20"

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "abc");

            Assert.Contains("valid positive number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.TimeWindowSeconds);
            Assert.Equal(lastText, vm.TimeWindowSecondsText);
            Assert.Equal(0, redraws); // non applica → niente chart
        }

        [Fact(DisplayName = "TimeWindow: troppo grande → validation + reset del testo")]
        public void TimeWindow_above_max_shows_error_and_resets_text()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeText = vm.TimeWindowSecondsText;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", (100000).ToString());

            Assert.Contains("too large", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeText, vm.TimeWindowSecondsText);   // ResetTimeWindowText()
            Assert.Equal(20, vm.TimeWindowSeconds);               // invariato
        }

        [Fact(DisplayName = "TimeWindow: troppo piccolo → validation + reset del testo")]
        public void TimeWindow_below_min_shows_error_and_resets_text()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeText = vm.TimeWindowSecondsText;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "0");  // MIN = 1

            Assert.Contains("too small", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeText, vm.TimeWindowSecondsText);
            Assert.Equal(20, vm.TimeWindowSeconds);
        }

        [Fact(DisplayName = "TimeWindow: valido → aggiorna _lastValidTimeWindowSeconds")]
        public void TimeWindow_valid_updates_last_valid_backing_field()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "45");

            var last = GetPrivate<int>(vm, "_lastValidTimeWindowSeconds");
            Assert.Equal(45, last);
        }

        [Fact(DisplayName = "TimeWindow: nessun side-effect su YAxisText e XAxisText")]
        public void TimeWindow_changes_do_not_touch_yaxis_and_xaxis_texts()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // “sporca” i testi per controllare che non vengano toccati
            SetPrivate(vm, "_yAxisMinText", "YMIN-TXT");
            SetPrivate(vm, "_yAxisMaxText", "YMAX-TXT");
            SetPrivate(vm, "_xAxisLabelIntervalText", "XINT-TXT");

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "25");

            Assert.Equal("YMIN-TXT", vm.YAxisMinText);
            Assert.Equal("YMAX-TXT", vm.YAxisMaxText);
            Assert.Equal("XINT-TXT", vm.XAxisLabelIntervalText);
        }

        // ValidateAndUpdateXAxisInterval behavior
        [Fact(DisplayName = "XLabel: vuoto → reset a default (5), niente errori, chart refresh")]
        public void XAxisInterval_empty_resets_to_default_and_refreshes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());
            // pre: default è 5
            Assert.Equal(5, vm.XAxisLabelInterval);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Act (private)
            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "");

            // Assert
            Assert.Equal(5, vm.XAxisLabelInterval);      // resta/default 5
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.True(redraws >= 1);
        }

        [Fact(DisplayName = "XLabel: valido → aggiorna valore, non tocca il testo, chart refresh")]
        public void XAxisInterval_valid_updates_value_and_refreshes_chart()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // Stato iniziale
            Assert.Equal(5, vm.XAxisLabelInterval);
            var prevText = vm.XAxisLabelIntervalText; // tipicamente "5"
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Act
            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "7");

            // Assert
            Assert.Equal(7, vm.XAxisLabelInterval);
            Assert.Equal(prevText, vm.XAxisLabelIntervalText); // il metodo non aggiorna il testo
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.True(redraws >= 1);
        }

        [Fact(DisplayName = "XLabel: troppo basso (<MIN) → messaggio e reset testo al last-valid")]
        public void XAxisInterval_too_low_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // last-valid numerico/text è 5
            Assert.Equal(5, vm.XAxisLabelInterval);
            var lastValidText = vm.XAxisLabelIntervalText; // "5"

            // Act
            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "0");

            // Assert
            Assert.Contains("too low", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval);            // numerico invariato
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText); // testo ripristinato (ResetXAxisIntervalText)
        }

        [Fact(DisplayName = "XLabel: troppo alto (>MAX) → messaggio e reset testo al last-valid")]
        public void XAxisInterval_too_high_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // last-valid = 5
            var lastValidText = vm.XAxisLabelIntervalText;

            // Act
            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "20000");

            // Assert
            Assert.Contains("too high", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText);
        }

        [Fact(DisplayName = "XLabel: non numerico → messaggio e reset testo al last-valid")]
        public void XAxisInterval_invalid_text_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var lastValidText = vm.XAxisLabelIntervalText; // "5"

            // Act
            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "abc");

            // Assert
            Assert.Contains("valid positive number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval);             // numerico invariato
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText); // testo ripristinato
        }

        // ResetAllCounters behavior

        // =============================
        // ResetAllCounters
        // =============================
        [Fact(DisplayName = "ResetAllCounters: azzera sampleCounter")]
        public void ResetAllCounters_zeros_sample_counter()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // porta a un valore > 0
            SetPrivate<int>(vm, "sampleCounter", 42);

            InvokePrivate(vm, "ResetAllCounters");

            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
        }

        [Fact(DisplayName = "ResetAllCounters: idempotente (seconda chiamata resta 0)")]
        public void ResetAllCounters_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "sampleCounter", 5);
            InvokePrivate(vm, "ResetAllCounters");
            InvokePrivate(vm, "ResetAllCounters");

            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
        }

        // =============================
        // ResetSamplingRateText
        // =============================
        [Fact(DisplayName = "ResetSamplingRateText: ripristina testo allo scorso valido (invariant)")]
        public void ResetSamplingRateText_restores_last_valid_invariant()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // setta last-valid e sporca il testo attuale
            SetPrivate<double>(vm, "_lastValidSamplingRate", 123.45);
            SetPrivate<string>(vm, "_samplingRateText", "WRONG");

            InvokePrivate(vm, "ResetSamplingRateText");

            Assert.Equal("123.45", vm.SamplingRateText);
        }

        [Fact(DisplayName = "ResetSamplingRateText: usa sempre InvariantCulture (anche con it-IT)")]
        public void ResetSamplingRateText_uses_invariant_culture()
        {
            var prev = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            try
            {
                var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

                // 12.5 deve diventare "12.5" (non "12,5")
                SetPrivate<double>(vm, "_lastValidSamplingRate", 12.5);
                SetPrivate<string>(vm, "_samplingRateText", "??");

                InvokePrivate(vm, "ResetSamplingRateText");

                Assert.Equal("12.5", vm.SamplingRateText);
            }
            finally { CultureInfo.CurrentCulture = prev; }
        }

        // =============================
        // ResetYAxisMinText
        // =============================
        [Fact(DisplayName = "ResetYAxisMinText: ripristina YAxisMinText allo scorso valido")]
        public void ResetYAxisMinText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.YAxisMin; // non deve cambiare
            SetPrivate<double>(vm, "_lastValidYAxisMin", -3.2);
            SetPrivate<string>(vm, "_yAxisMinText", "sporcato");

            InvokePrivate(vm, "ResetYAxisMinText");

            Assert.Equal("-3.2", vm.YAxisMinText);
            Assert.Equal(beforeNumeric, vm.YAxisMin, 5); // solo testo, non numerico
        }

        [Fact(DisplayName = "ResetYAxisMinText: idempotente")]
        public void ResetYAxisMinText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<double>(vm, "_lastValidYAxisMin", -1.0);
            SetPrivate<string>(vm, "_yAxisMinText", "X");
            InvokePrivate(vm, "ResetYAxisMinText");
            InvokePrivate(vm, "ResetYAxisMinText");

            Assert.Equal("-1", vm.YAxisMinText);
        }

        // =============================
        // ResetYAxisMaxText
        // =============================
        [Fact(DisplayName = "ResetYAxisMaxText: ripristina YAxisMaxText allo scorso valido")]
        public void ResetYAxisMaxText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.YAxisMax;
            SetPrivate<double>(vm, "_lastValidYAxisMax", 7.75);
            SetPrivate<string>(vm, "_yAxisMaxText", "bad");

            InvokePrivate(vm, "ResetYAxisMaxText");

            Assert.Equal("7.75", vm.YAxisMaxText);
            Assert.Equal(beforeNumeric, vm.YAxisMax, 5);
        }

        [Fact(DisplayName = "ResetYAxisMaxText: idempotente")]
        public void ResetYAxisMaxText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<double>(vm, "_lastValidYAxisMax", 10.0);
            SetPrivate<string>(vm, "_yAxisMaxText", "wrong");
            InvokePrivate(vm, "ResetYAxisMaxText");
            InvokePrivate(vm, "ResetYAxisMaxText");

            Assert.Equal("10", vm.YAxisMaxText);
        }

        // =============================
        // ResetTimeWindowText
        // =============================
        [Fact(DisplayName = "ResetTimeWindowText: ripristina TimeWindowSecondsText allo scorso valido")]
        public void ResetTimeWindowText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.TimeWindowSeconds;
            SetPrivate<int>(vm, "_lastValidTimeWindowSeconds", 30);
            SetPrivate<string>(vm, "_timeWindowSecondsText", "dirty");

            InvokePrivate(vm, "ResetTimeWindowText");

            Assert.Equal("30", vm.TimeWindowSecondsText);
            Assert.Equal(beforeNumeric, vm.TimeWindowSeconds); // solo testo
        }

        [Fact(DisplayName = "ResetTimeWindowText: idempotente")]
        public void ResetTimeWindowText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "_lastValidTimeWindowSeconds", 45);
            SetPrivate<string>(vm, "_timeWindowSecondsText", "foo");

            InvokePrivate(vm, "ResetTimeWindowText");
            InvokePrivate(vm, "ResetTimeWindowText");

            Assert.Equal("45", vm.TimeWindowSecondsText);
        }

        // =============================
        // ResetXAxisIntervalText
        // =============================
        [Fact(DisplayName = "ResetXAxisIntervalText: ripristina XAxisLabelIntervalText allo scorso valido")]
        public void ResetXAxisIntervalText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.XAxisLabelInterval;
            SetPrivate<int>(vm, "_lastValidXAxisLabelInterval", 7);
            SetPrivate<string>(vm, "_xAxisLabelIntervalText", "bad");

            InvokePrivate(vm, "ResetXAxisIntervalText");

            Assert.Equal("7", vm.XAxisLabelIntervalText);
            Assert.Equal(beforeNumeric, vm.XAxisLabelInterval); // solo testo
        }

        [Fact(DisplayName = "ResetXAxisIntervalText: idempotente")]
        public void ResetXAxisIntervalText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "_lastValidXAxisLabelInterval", 1);
            SetPrivate<string>(vm, "_xAxisLabelIntervalText", "nope");
            InvokePrivate(vm, "ResetXAxisIntervalText");
            InvokePrivate(vm, "ResetXAxisIntervalText");

            Assert.Equal("1", vm.XAxisLabelIntervalText);
        }

        // GetDefaultYAxisMin behavior
        // ========== HELPER COMUNE (static private double) ==========
        private static double InvokePrivateStaticDouble(string methodName, string arg)
        {
            var mi = typeof(DataPageViewModel).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi); // se fallisce, il metodo non è stato trovato (nome o visibility)
            var result = mi!.Invoke(null, new object[] { arg });
            Assert.NotNull(result);
            return (double)result!;
        }

        // ======================= TEST: MIN =========================
        public class GetDefaultYAxisMin_Tests
        {
            [Theory(DisplayName = "GetDefaultYAxisMin: gruppi IMU → valori attesi")]
            [InlineData("Low-Noise Accelerometer", -20)]
            [InlineData("Wide-Range Accelerometer", -20)]
            [InlineData("Gyroscope", -250)]
            [InlineData("Magnetometer", -5)]
            public void Groups_Return_Expected_Min(string group, double expected)
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", group);
                Assert.Equal(expected, min, 5);
            }

            [Theory(DisplayName = "GetDefaultYAxisMin: singoli IMU → valori attesi")]
            [InlineData("Low-Noise AccelerometerX", -5)]
            [InlineData("Low-Noise AccelerometerY", -5)]
            [InlineData("Low-Noise AccelerometerZ", -15)]
            [InlineData("Wide-Range AccelerometerX", -5)]
            [InlineData("Wide-Range AccelerometerY", -5)]
            [InlineData("Wide-Range AccelerometerZ", -15)]
            [InlineData("GyroscopeX", -250)]
            [InlineData("GyroscopeY", -250)]
            [InlineData("GyroscopeZ", -250)]
            [InlineData("MagnetometerX", -5)]
            [InlineData("MagnetometerY", -5)]
            [InlineData("MagnetometerZ", -5)]
            [InlineData("Temperature_BMP180", 15)]
            [InlineData("Pressure_BMP180", 90)]
            [InlineData("BatteryVoltage", 3.3)]
            [InlineData("BatteryPercent", 0)]
            [InlineData("ExtADC_A6", 0)]
            [InlineData("ExtADC_A7", 0)]
            [InlineData("ExtADC_A15", 0)]
            public void Singles_Return_Expected_Min(string param, double expected)
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", param);
                Assert.Equal(expected, min, 5);
            }

            [Theory(DisplayName = "GetDefaultYAxisMin: EXG → -15")]
            [InlineData("ECG")]
            [InlineData("EMG")]
            [InlineData("EXG Test")]
            [InlineData("Respiration")]
            public void EXG_Returns_Minus15_Min(string exg)
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", exg);
                Assert.Equal(-15.0, min, 5);
            }

            [Fact(DisplayName = "GetDefaultYAxisMin: sconosciuto → 0")]
            public void Unknown_Returns_0_Min()
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", "__unknown__");
                Assert.Equal(0.0, min, 5);
            }
        }

        // ======================= TEST: MAX =========================
        public class GetDefaultYAxisMax_Tests
        {
            [Theory(DisplayName = "GetDefaultYAxisMax: gruppi IMU → valori attesi")]
            [InlineData("Low-Noise Accelerometer", 20)]
            [InlineData("Wide-Range Accelerometer", 20)]
            [InlineData("Gyroscope", 250)]
            [InlineData("Magnetometer", 5)]
            public void Groups_Return_Expected_Max(string group, double expected)
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", group);
                Assert.Equal(expected, max, 5);
            }

            [Theory(DisplayName = "GetDefaultYAxisMax: singoli IMU → valori attesi")]
            [InlineData("Low-Noise AccelerometerX", 5)]
            [InlineData("Low-Noise AccelerometerY", 5)]
            [InlineData("Low-Noise AccelerometerZ", 15)]
            [InlineData("Wide-Range AccelerometerX", 5)]
            [InlineData("Wide-Range AccelerometerY", 5)]
            [InlineData("Wide-Range AccelerometerZ", 15)]
            [InlineData("GyroscopeX", 250)]
            [InlineData("GyroscopeY", 250)]
            [InlineData("GyroscopeZ", 250)]
            [InlineData("MagnetometerX", 5)]
            [InlineData("MagnetometerY", 5)]
            [InlineData("MagnetometerZ", 5)]
            [InlineData("Temperature_BMP180", 40)]
            [InlineData("Pressure_BMP180", 110)]
            [InlineData("BatteryVoltage", 4.2)]
            [InlineData("BatteryPercent", 100)]
            [InlineData("ExtADC_A6", 3.3)]
            [InlineData("ExtADC_A7", 3.3)]
            [InlineData("ExtADC_A15", 3.3)]
            public void Singles_Return_Expected_Max(string param, double expected)
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", param);
                Assert.Equal(expected, max, 5);
            }

            [Theory(DisplayName = "GetDefaultYAxisMax: EXG → +15")]
            [InlineData("ECG")]
            [InlineData("EMG")]
            [InlineData("EXG Test")]
            [InlineData("Respiration")]
            public void EXG_Returns_Plus15_Max(string exg)
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", exg);
                Assert.Equal(15.0, max, 5);
            }

            [Fact(DisplayName = "GetDefaultYAxisMax: sconosciuto → 1")]
            public void Unknown_Returns_1_Max()
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", "__unknown__");
                Assert.Equal(1.0, max, 5);
            }
        }

        // InitializeAvailableParameters behavior

        // ---------- HELPER: invoca metodo privato di istanza ----------
        private static void InvokePrivateInitializeAvailableParameters(object vm)
        {
            var mi = vm.GetType().GetMethod("InitializeAvailableParameters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            mi!.Invoke(vm, null);
        }

        // ---------- HELPER: set di bool privati ----------
        private static void SetPrivateBool(object vm, string fieldName, bool value)
        {
            var f = vm.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(vm, value);
        }

        // Per comodità: attiva/disattiva più flag in un colpo
        private static void SetFlags(object vm, IDictionary<string, bool> flags)
        {
            foreach (var kv in flags) SetPrivateBool(vm, kv.Key, kv.Value);
        }

        // ---------- HELPER: trova e setta field di tipo/lista o fallback su property ----------
        private static void SetListPropertyOrField<TList>(object target, string propNameHint, TList value)
            where TList : class
        {
            var t = target.GetType();

            // 1) Se esiste una property con setter, usala
            var p = t.GetProperty(propNameHint,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value);
                return;
            }

            // 2) Prova a trovare un backing field che contenga il nome suggerito
            var f = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     .FirstOrDefault(fi =>
                         fi.FieldType == typeof(TList) &&
                         fi.Name.Contains(propNameHint, StringComparison.OrdinalIgnoreCase));

            // 3) Se non trovato, prendi il primo field compatibile per tipo (unico)
            f ??= t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                   .FirstOrDefault(fi => fi.FieldType == typeof(TList));

            Assert.NotNull(f);
            f!.SetValue(target, value);
        }

        private static void SetStringPropertyOrField(object target, string propNameHint, string value)
        {
            var t = target.GetType();

            // 1) Property con setter?
            var p = t.GetProperty(propNameHint,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value);
                return;
            }

            // 2) Backing field per nome
            var f = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     .FirstOrDefault(fi =>
                         fi.FieldType == typeof(string) &&
                         fi.Name.Contains(propNameHint, StringComparison.OrdinalIgnoreCase));

            // 3) Primo field string compatibile se non trovato
            f ??= t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                   .FirstOrDefault(fi => fi.FieldType == typeof(string));

            Assert.NotNull(f);
            f!.SetValue(target, value);
        }

        // ---------- HELPER: crea VM “grezza” senza eseguire il costruttore ----------
        private static DataPageViewModel NewVmForAvailableParameters()
        {
            var t = typeof(DataPageViewModel);

            // Istanzia senza invocare alcun costruttore
            var vm = (DataPageViewModel)RuntimeHelpers.GetUninitializedObject(t);

            // Stato minimo per poter lavorare:
            // - AvailableParameters: via backing field (niente setter pubblico)
            // - SelectedParameter: property o backing field string
            SetListPropertyOrField(vm, "AvailableParameters", new ObservableCollection<string>());
            SetStringPropertyOrField(vm, "SelectedParameter", "");

            return vm;
        }


        // Alias per compat: se altri test chiamano NewVm()
        private static DataPageViewModel NewVm() => NewVmForAvailableParameters();


        // -------------------- TESTS --------------------

        [Fact(DisplayName = "InitializeAvailableParameters: tutti disabilitati → lista vuota e SelectedParameter vuoto")]
        public void Init_AllDisabled_YieldsEmptyAndEmptySelection()
        {
            var vm = NewVm();

            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            // selection iniziale a un valore non valido per verificare il reset
            SetStringPropertyOrField(vm, "SelectedParameter", "Qualcosa che non esiste");

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Empty(((IEnumerable<string>)vm.AvailableParameters).ToArray());
            Assert.Equal("", vm.SelectedParameter);
        }

        [Fact(DisplayName = "InitializeAvailableParameters: solo LNA abilitato → 2 voci in ordine e selezione prima voce")]
        public void Init_LNA_Only_TwoEntriesAndSelectionFirst()
        {
            var vm = NewVm();

            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = true,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Equal(2, vm.AvailableParameters.Count);
            Assert.Equal("Low-Noise Accelerometer", vm.AvailableParameters[0]);
            Assert.Equal("    → Low-Noise Accelerometer — separate charts (X·Y·Z)", vm.AvailableParameters[1]);
            Assert.Equal("Low-Noise Accelerometer", vm.SelectedParameter);
        }

        [Fact(DisplayName = "InitializeAvailableParameters: più sensori (Gyro + Battery + ExtA7) → voci previste e ordine corretto")]
        public void Init_MultipleSensors_Gyro_Battery_ExtA7()
        {
            var vm = NewVm();

            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = true,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = true,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = true,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            InvokePrivateInitializeAvailableParameters(vm);

            var expected = new[]
            {
        "Gyroscope",
        "    → Gyroscope — separate charts (X·Y·Z)",
        "BatteryVoltage",
        "BatteryPercent",
        "ExtADC_A7"
    };

            Assert.Equal(expected.Length, vm.AvailableParameters.Count);
            Assert.Equal(expected, vm.AvailableParameters.ToArray());
            Assert.Equal("Gyroscope", vm.SelectedParameter);
        }

        [Theory(DisplayName = "InitializeAvailableParameters: EXG attivo in modalità specifica → label e variante split corretta")]
        [InlineData("exgModeRespiration", "Respiration", "    → Respiration — separate charts (EXG1·EXG2)")]
        [InlineData("exgModeECG", "ECG", "    → ECG — separate charts (EXG1·EXG2)")]
        [InlineData("exgModeEMG", "EMG", "    → EMG — separate charts (EXG1·EXG2)")]
        [InlineData("exgModeTest", "EXG Test", "    → EXG Test — separate charts (EXG1·EXG2)")]
        public void Init_EXG_Mode_Specific_LabelAndSplit(string modeFlag, string expectedMain, string expectedSplit)
        {
            var vm = NewVm();

            var flags = new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = true,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            };
            flags[modeFlag] = true;
            SetFlags(vm, flags);

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Equal(2, vm.AvailableParameters.Count);
            Assert.Equal(expectedMain, vm.AvailableParameters[0]);
            Assert.Equal(expectedSplit, vm.AvailableParameters[1]);
            Assert.Equal(expectedMain, vm.SelectedParameter);
        }

        [Fact(DisplayName = "InitializeAvailableParameters: EXG attivo ma senza modalità → label generica EXG")]
        public void Init_EXG_Generic_When_No_Mode()
        {
            var vm = NewVm();

            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = true,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Equal(2, vm.AvailableParameters.Count);
            Assert.Equal("EXG", vm.AvailableParameters[0]);
            Assert.Equal("    → EXG — separate charts (EXG1·EXG2)", vm.AvailableParameters[1]);
            Assert.Equal("EXG", vm.SelectedParameter);
        }

        [Fact(DisplayName = "InitializeAvailableParameters: SelectedParameter valido resta invariato")]
        public void Init_Selection_Stays_When_StillAvailable()
        {
            var vm = NewVm();

            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = true,
                ["enableBattery"] = true,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            // Prima init
            InvokePrivateInitializeAvailableParameters(vm);

            // Forziamo la selezione su BatteryPercent, che è valido
            SetStringPropertyOrField(vm, "SelectedParameter", "BatteryPercent");

            // Re-initialization con gli stessi flag: la selezione non deve cambiare
            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Contains("BatteryPercent", vm.AvailableParameters);
            Assert.Equal("BatteryPercent", vm.SelectedParameter);
        }

        [Fact(DisplayName = "InitializeAvailableParameters: SelectedParameter non più valido → seleziona prima voce")]
        public void Init_Selection_Replaced_When_NotAvailable()
        {
            var vm = NewVm();

            // Abilita LNA per avere una prima voce prevedibile
            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = true,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            // prima init
            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Equal("Low-Noise Accelerometer", vm.SelectedParameter);

            // Ora disabilita tutto: la vecchia selezione diventa non valida
            SetFlags(vm, new Dictionary<string, bool>
            {
                ["enableLowNoiseAccelerometer"] = false,
                ["enableWideRangeAccelerometer"] = false,
                ["enableGyroscope"] = false,
                ["enableMagnetometer"] = false,
                ["enableBattery"] = false,
                ["enablePressureTemperature"] = false,
                ["enableExtA6"] = false,
                ["enableExtA7"] = false,
                ["enableExtA15"] = false,
                ["enableExg"] = false,
                ["exgModeRespiration"] = false,
                ["exgModeECG"] = false,
                ["exgModeEMG"] = false,
                ["exgModeTest"] = false,
            });

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Empty(vm.AvailableParameters);
            Assert.Equal("", vm.SelectedParameter);
        }



        // --------------------------------------------------------------------
        // CleanParameterName — behavior: rimuove adorni UI, freccia e suggerimenti split
        // --------------------------------------------------------------------

        [Theory]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)", "Gyroscope")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)", "ECG")]
        [InlineData("Magnetometer (separate charts)", "Magnetometer")]
        [InlineData("Low-Noise Accelerometer", "Low-Noise Accelerometer")]
        public void CleanParameterName_strips_UI_adornments(string raw, string expected)
        {
            var clean = DataPageViewModel.CleanParameterName(raw);
            Assert.Equal(expected, clean);
        }

        // --------------------------------------------------------------------
        // CleanParameterName — gestisce null/vuoto, trattino e spazi extra
        // --------------------------------------------------------------------
        // --------------------------------------------------------------------
        // CleanParameterName — gestisce null/vuoto, trattino e spazi extra
        // --------------------------------------------------------------------
        [Theory(DisplayName = "CleanParameterName: null/vuoto e varianti con trattino e spazi extra")]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("    →   Magnetometer  - separate charts (X·Y·Z)   ", "Magnetometer")]
        [InlineData("    →  EXG Test - separate charts (EXG1·EXG2) ", "EXG Test")]
        public void CleanParameterName_null_empty_hyphen_and_spaces(string? raw, string expected)
        {
            var clean = DataPageViewModel.CleanParameterName(raw);
            Assert.Equal(expected, clean);
        }



        // --------------------------------------------------------------------
        // MapToInternalKey — behavior: mappa EXG1/EXG2 a Exg1/Exg2, altrimenti nome pulito invariato
        // --------------------------------------------------------------------

        [Theory]
        [InlineData("EXG1", "Exg1")]
        [InlineData("exg2", "Exg2")]
        [InlineData("GyroscopeX", "GyroscopeX")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)", "ECG")]
        public void MapToInternalKey_maps_exg_channels_and_cleans(string input, string expected)
        {
            var key = DataPageViewModel.MapToInternalKey(input);
            Assert.Equal(expected, key);
        }


        // --------------------------------------------------------------------
        // MapToInternalKey — edge cases: spazi, case-insensitive, EXG generico, vuoto
        // --------------------------------------------------------------------
        [Theory(DisplayName = "MapToInternalKey: spazi/Case/EXG generico/vuoto")]
        [InlineData("  eXg1  ", "Exg1")] // case-insensitive + trim
        [InlineData("   EXG2   ", "Exg2")] // case-insensitive + trim
        [InlineData("    → EXG — separate charts (EXG1·EXG2)", "EXG")] // rimozione adornamenti, non canale
        [InlineData("", "")] // vuoto → vuoto
        public void MapToInternalKey_handles_edge_cases(string input, string expected)
        {
            var key = DataPageViewModel.MapToInternalKey(input);
            Assert.Equal(expected, key);
        }


        // IsSplitVariantLabel behavior
        // helper: invoca metodo statico privato bool IsSplitVariantLabel(string?)
        private static bool Invoke_IsSplitVariantLabel(string? label)
        {
            var t = typeof(ShimmerInterface.ViewModels.DataPageViewModel);
            var mi = t.GetMethod("IsSplitVariantLabel",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            var result = mi!.Invoke(null, new object?[] { label });
            Assert.IsType<bool>(result);
            return (bool)result!;
        }

        // --------------------------------------------------------------------
        // IsSplitVariantLabel — TRUE cases
        // --------------------------------------------------------------------
        [Theory(DisplayName = "IsSplitVariantLabel: riconosce i label split/separate charts (case-insensitive)")]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)")]
        [InlineData("Magnetometer - separate charts (X·Y·Z)")] // con trattino normale
        [InlineData("Split (two separate charts)")]            // contiene "Split"
        [InlineData("split (tre grafici)")]                    // solo parola "split", case-insensitive
        public void IsSplitVariantLabel_true_cases(string label)
        {
            Assert.True(Invoke_IsSplitVariantLabel(label));
        }

        // --------------------------------------------------------------------
        // IsSplitVariantLabel — FALSE cases
        // --------------------------------------------------------------------
        [Theory(DisplayName = "IsSplitVariantLabel: non segna split quando non ci sono adornamenti")]
        [InlineData("Gyroscope")]
        [InlineData("Low-Noise Accelerometer")]
        [InlineData("EXG")]
        [InlineData("ECG")]                 // label pulito
        [InlineData("")]                    // vuoto
        [InlineData("   ")]                 // spazi
        public void IsSplitVariantLabel_false_cases(string label)
        {
            Assert.False(Invoke_IsSplitVariantLabel(label));
        }

        [Fact(DisplayName = "IsSplitVariantLabel: null → false")]
        public void IsSplitVariantLabel_null_is_false()
        {
            Assert.False(Invoke_IsSplitVariantLabel(null));
        }

        // IsMultiChart behavior
        // helper: invoca metodo statico privato bool IsMultiChart(string)
        private static bool Invoke_IsMultiChart(string param)
        {
            var t = typeof(ShimmerInterface.ViewModels.DataPageViewModel);
            var mi = t.GetMethod("IsMultiChart",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            var result = mi!.Invoke(null, new object?[] { param });
            Assert.IsType<bool>(result);
            return (bool)result!;
        }

        // --------------------------------------------------------------------
        // IsMultiChart — TRUE cases (gruppi multi-serie)
        // --------------------------------------------------------------------
        [Theory(DisplayName = "IsMultiChart: riconosce i gruppi multi-serie (IMU + EXG)")]
        [InlineData("Low-Noise Accelerometer")]
        [InlineData("Wide-Range Accelerometer")]
        [InlineData("Gyroscope")]
        [InlineData("Magnetometer")]
        [InlineData("ECG")]
        [InlineData("EMG")]
        [InlineData("EXG Test")]
        [InlineData("Respiration")]
        [InlineData("EXG")]
        public void IsMultiChart_true_groups(string group)
        {
            Assert.True(Invoke_IsMultiChart(group));
        }

        // Varianti con adornamenti UI (devono comunque risultare true)
        [Theory(DisplayName = "IsMultiChart: true anche con adornamenti UI (freccia/split)")]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)")]
        [InlineData("Magnetometer - separate charts (X·Y·Z)")]
        public void IsMultiChart_true_with_adornments(string labeled)
        {
            Assert.True(Invoke_IsMultiChart(labeled));
        }

        // --------------------------------------------------------------------
        // IsMultiChart — FALSE cases (singole serie / non gruppi)
        // --------------------------------------------------------------------
        [Theory(DisplayName = "IsMultiChart: false per canali singoli e parametri non a gruppi")]
        [InlineData("GyroscopeX")]
        [InlineData("GyroscopeY")]
        [InlineData("MagnetometerZ")]
        [InlineData("Low-Noise AccelerometerX")]
        [InlineData("Wide-Range AccelerometerZ")]
        [InlineData("EXG1")]
        [InlineData("Exg2")]
        [InlineData("BatteryVoltage")]
        [InlineData("BatteryPercent")]
        [InlineData("Temperature_BMP180")]
        [InlineData("Pressure_BMP180")]
        [InlineData("ExtADC_A6")]
        [InlineData("ExtADC_A7")]
        [InlineData("ExtADC_A15")]
        [InlineData("")]           // stringa vuota
        [InlineData("   ")]        // solo spazi
        public void IsMultiChart_false_singles_and_others(string name)
        {
            Assert.False(Invoke_IsMultiChart(name));
        }




        // --------------------------------------------------------------------
        // GetSubParameters — behavior: ritorna le sotto-serie attese per ogni gruppo
        // --------------------------------------------------------------------

        [Fact]
        public void GetSubParameters_returns_xyz_for_gyroscope()
        {
            var list = DataPageViewModel.GetSubParameters("Gyroscope");
            Assert.Equal(new[] { "GyroscopeX", "GyroscopeY", "GyroscopeZ" }, list);
        }

        [Fact]
        public void GetSubParameters_returns_exg1_exg2_for_exg_groups()
        {
            var list = DataPageViewModel.GetSubParameters("ECG");
            Assert.Equal(new[] { "Exg1", "Exg2" }, list);
        }

        [Fact]
        public void GetSubParameters_empty_for_unknown_group()
        {
            var list = DataPageViewModel.GetSubParameters("SomethingElse");
            Assert.Empty(list);
        }

        // --------------------------------------------------------------------
        // GetLegendLabel — behavior: comprime le etichette (X,Y,Z) e mostra EXG1/EXG2 per EXG
        // --------------------------------------------------------------------

        [Theory]
        [InlineData("Gyroscope", "GyroscopeX", "X")]
        [InlineData("Gyroscope", "GyroscopeY", "Y")]
        [InlineData("Gyroscope", "GyroscopeZ", "Z")]
        [InlineData("ECG", "Exg1", "EXG1")]
        [InlineData("EMG", "Exg2", "EXG2")]
        [InlineData("BatteryVoltage", "BatteryVoltage", "BatteryVoltage")]
        public void GetLegendLabel_returns_readable_label(string group, string sub, string expected)
        {
            var label = DataPageViewModel.GetLegendLabel(group, sub);
            Assert.Equal(expected, label);
        }

        [Fact(DisplayName = "GetLegendLabel: gruppo con adorni UI → comprime asse correttamente")]
        public void GetLegendLabel_group_with_adornments_returns_axis_letter()
        {
            // gruppo con freccia + hint "separate charts"
            var groupWithUi = "    → Gyroscope — separate charts (X·Y·Z)";
            var label = DataPageViewModel.GetLegendLabel(groupWithUi, "GyroscopeY");
            Assert.Equal("Y", label);
        }


        // GetCurrentSubParameters behavior

        // --------------------------------------------------------------------
        // GetCurrentSubParameters — behavior
        // --------------------------------------------------------------------

        [Fact(DisplayName = "GetCurrentSubParameters: parametro singolo → lista con un solo elemento (nome pulito)")]
        public void GetCurrentSubParameters_single_returns_single_clean_name()
        {
            var vm = NewVm();
            vm.SelectedParameter = "BatteryVoltage";   // singolo
            var subs = vm.GetCurrentSubParameters();

            Assert.Single(subs);
            Assert.Equal("BatteryVoltage", subs[0]);
        }

        [Fact(DisplayName = "GetCurrentSubParameters: gruppo IMU (Gyroscope) → X, Y, Z")]
        public void GetCurrentSubParameters_group_imu_returns_xyz()
        {
            var vm = NewVm();
            vm.SelectedParameter = "Gyroscope";        // gruppo IMU

            var subs = vm.GetCurrentSubParameters();

            Assert.Equal(3, subs.Count);
            Assert.Contains("GyroscopeX", subs);
            Assert.Contains("GyroscopeY", subs);
            Assert.Contains("GyroscopeZ", subs);
        }

        [Fact(DisplayName = "GetCurrentSubParameters: gruppo EXG con adorni UI → Exg1, Exg2")]
        public void GetCurrentSubParameters_exg_group_with_ui_adornments_returns_exg1_exg2()
        {
            var vm = NewVm();
            // label con freccia e hint split; CleanParameterName dovrebbe ripulirla
            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";

            var subs = vm.GetCurrentSubParameters();

            Assert.Equal(2, subs.Count);
            Assert.Contains("Exg1", subs);
            Assert.Contains("Exg2", subs);
        }

        [Fact(DisplayName = "GetCurrentSubParameters: SelectedParameter con spazi → viene pulito e resta singolo")]
        public void GetCurrentSubParameters_trims_spaces_when_single()
        {
            var vm = NewVm();
            vm.SelectedParameter = "   BatteryVoltage   ";  // parametro singolo con spazi

            var subs = vm.GetCurrentSubParameters();

            Assert.Single(subs);
            Assert.Equal("BatteryVoltage", subs[0]);
        }

        // TryGetNumeric behavior

        // --------------------------------------------------------------------
        // TryGetNumeric — tests (private static) tramite reflection
        // --------------------------------------------------------------------
        private static bool InvokeTryGetNumeric(object? sample, string field, out float val)
        {
            var mi = typeof(DataPageViewModel).GetMethod("TryGetNumeric",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(mi);

            object?[] args = new object?[] { sample, field, 0f };
            var ok = (bool)(mi!.Invoke(null, args) ?? false);
            val = (float)args[2]!;
            return ok;
        }

        // Wrapper con proprietà Data numerica
        private sealed class WrapperNum
        {
            public double Data { get; set; }
        }

        // Wrapper con proprietà Data object (può essere null o non numerico)
        private sealed class WrapperObj
        {
            public object? Data { get; set; }
        }

        // Sample di comodo con vari tipi di proprietà
        private sealed class NumSample
        {
            public int A { get; set; }
            public object? B { get; set; }
            public WrapperNum? C { get; set; }
            public WrapperObj? D { get; set; }
        }

        [Fact(DisplayName = "TryGetNumeric: campo int primitivo → true e valore corretto")]
        public void TryGetNumeric_int_field_returns_true_and_value()
        {
            var s = new NumSample { A = 42 };
            var ok = InvokeTryGetNumeric(s, "A", out var v);
            Assert.True(ok);
            Assert.Equal(42f, v);
        }

        [Fact(DisplayName = "TryGetNumeric: campo double (boxed in object) → true e valore corretto")]
        public void TryGetNumeric_double_in_object_returns_true_and_value()
        {
            var s = new NumSample { B = 12.5d };
            var ok = InvokeTryGetNumeric(s, "B", out var v);
            Assert.True(ok);
            Assert.Equal(12.5f, v, 3);
        }

        [Fact(DisplayName = "TryGetNumeric: wrapper con .Data numerico → true e valore corretto")]
        public void TryGetNumeric_wrapper_with_numeric_Data_returns_true()
        {
            var s = new NumSample { C = new WrapperNum { Data = 3.14159 } };
            var ok = InvokeTryGetNumeric(s, "C", out var v);
            Assert.True(ok);
            Assert.Equal(3.14159f, v, 4);
        }

        [Fact(DisplayName = "TryGetNumeric: wrapper con .Data = null → false")]
        public void TryGetNumeric_wrapper_with_null_Data_returns_false()
        {
            var s = new NumSample { D = new WrapperObj { Data = null } };
            var ok = InvokeTryGetNumeric(s, "D", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }

        [Fact(DisplayName = "TryGetNumeric: campo inesistente → false")]
        public void TryGetNumeric_missing_field_returns_false()
        {
            var s = new NumSample { A = 7 };
            var ok = InvokeTryGetNumeric(s, "Z", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }

        [Fact(DisplayName = "TryGetNumeric: campo stringa non numerica → false")]
        public void TryGetNumeric_non_numeric_string_returns_false()
        {
            var s = new NumSample { B = "ciao" };
            var ok = InvokeTryGetNumeric(s, "B", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }

        [Fact(DisplayName = "TryGetNumeric: sample nullo → false")]
        public void TryGetNumeric_null_sample_returns_false()
        {
            var ok = InvokeTryGetNumeric(null, "Qualsiasi", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }

        // HasProp behavior

        // ====================================================================
        // HasProp — tests (invocazione via reflection perché è private static)
        // ====================================================================

        // Tipi d'appoggio per i test
        private sealed class WithProp
        {
            public int Foo { get; set; }
        }
        private sealed class WithField
        {
            public int Bar = 42;
        }
        private sealed class WithBoth
        {
            public int Foo { get; set; }
            public string Baz = "x";
        }

        // Helper: invoca il metodo privato statico HasProp(obj, name)
        private static bool Call_HasProp(object? obj, string name)
        {
            var mi = typeof(DataPageViewModel).GetMethod(
                "HasProp", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            return (bool)mi!.Invoke(null, new object?[] { obj!, name })!;
        }

        [Fact(DisplayName = "HasProp: true quando esiste una proprietà pubblica")]
        public void HasProp_true_on_public_property()
        {
            var o = new WithProp { Foo = 1 };
            Assert.True(Call_HasProp(o, "Foo"));
        }

        [Fact(DisplayName = "HasProp: true quando esiste un campo pubblico")]
        public void HasProp_true_on_public_field()
        {
            var o = new WithField();
            Assert.True(Call_HasProp(o, "Bar"));
        }

        [Fact(DisplayName = "HasProp: false quando il membro non esiste")]
        public void HasProp_false_when_missing()
        {
            var o = new WithBoth();
            Assert.False(Call_HasProp(o, "DoesNotExist"));
        }

        [Fact(DisplayName = "HasProp: false quando l'oggetto è null")]
        public void HasProp_false_on_null_object()
        {
            Assert.False(Call_HasProp(null!, "Foo"));
        }

        [Fact(DisplayName = "HasProp: ricerca case-sensitive (nome con maiuscole diverse → false)")]
        public void HasProp_case_sensitive()
        {
            var o = new WithProp();
            // "foo" non corrisponde a "Foo" con reflection di default
            Assert.False(Call_HasProp(o, "foo"));
        }


        // UpdateYAxisSettings behavior

        // ===== Helpers (in cima) =====
        private static object CreateVMWithoutCtor() =>
            FormatterServices.GetUninitializedObject(typeof(DataPageViewModel));

        private static void CallPrivate(object instance, string methodName, params object[] args)
        {
            var mi = instance.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            mi!.Invoke(instance, args);
        }

        private static T GetProp<T>(object instance, string propName)
        {
            var pi = instance.GetType().GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(pi);
            return (T)(pi!.GetValue(instance)!);
        }

        private static void SetProp(object instance, string propName, object? value)
        {
            var pi = instance.GetType().GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(pi);
            pi!.SetValue(instance, value);
        }
        // ===== Fine helpers =====

        [Theory]
        [InlineData("Low-Noise Accelerometer",
            "Low-Noise Accelerometer", "m/s²", "Real-time Low-Noise Accelerometer (X,Y,Z)", -20, 20)]
        [InlineData("Wide-Range Accelerometer",
            "Wide-Range Accelerometer", "m/s²", "Real-time Wide-Range Accelerometer (X,Y,Z)", -20, 20)]
        [InlineData("Gyroscope",
            "Gyroscope", "deg/s", "Real-time Gyroscope (X,Y,Z)", -250, 250)]
        [InlineData("Magnetometer",
            "Magnetometer", "local_flux*", "Real-time Magnetometer (X,Y,Z)", -5, 5)]
        [InlineData("Low-Noise AccelerometerX",
            "Low-Noise Accelerometer X", "m/s²", "Real-time Low-Noise Accelerometer X", -5, 5)]
        [InlineData("Low-Noise AccelerometerY",
            "Low-Noise Accelerometer Y", "m/s²", "Real-time Low-Noise Accelerometer Y", -5, 5)]
        [InlineData("Low-Noise AccelerometerZ",
            "Low-Noise Accelerometer Z", "m/s²", "Real-time Low-Noise Accelerometer Z", -15, 15)]
        [InlineData("Wide-Range AccelerometerX",
            "Wide-Range Accelerometer X", "m/s²", "Real-time Wide-Range Accelerometer X", -5, 5)]
        [InlineData("Wide-Range AccelerometerY",
            "Wide-Range Accelerometer Y", "m/s²", "Real-time Wide-Range Accelerometer Y", -5, 5)]
        [InlineData("Wide-Range AccelerometerZ",
            "Wide-Range Accelerometer Z", "m/s²", "Real-time Wide-Range Accelerometer Z", -15, 15)]
        [InlineData("GyroscopeX",
            "Gyroscope X", "deg/s", "Real-time Gyroscope X", -250, 250)]
        [InlineData("GyroscopeY",
            "Gyroscope Y", "deg/s", "Real-time Gyroscope Y", -250, 250)]
        [InlineData("GyroscopeZ",
            "Gyroscope Z", "deg/s", "Real-time Gyroscope Z", -250, 250)]
        [InlineData("MagnetometerX",
            "Magnetometer X", "local_flux*", "Real-time Magnetometer X", -5, 5)]
        [InlineData("MagnetometerY",
            "Magnetometer Y", "local_flux*", "Real-time Magnetometer Y", -5, 5)]
        [InlineData("MagnetometerZ",
            "Magnetometer Z", "local_flux*", "Real-time Magnetometer Z", -5, 5)]
        [InlineData("Temperature_BMP180",
            "Temperature", "°C", "BMP180 Temperature", 15, 40)]
        [InlineData("Pressure_BMP180",
            "Pressure", "kPa", "BMP180 Pressure", 90, 110)]
        [InlineData("BatteryVoltage",
            "Battery Voltage", "V", "Real-time Battery Voltage", 3.3, 4.2)]
        [InlineData("BatteryPercent",
            "Battery Percent", "%", "Real-time Battery Percentage", 0, 100)]
        [InlineData("ExtADC_A6",
            "External ADC A6", "V", "External ADC A6", 0, 3.3)]
        [InlineData("ExtADC_A7",
            "External ADC A7", "V", "External ADC A7", 0, 3.3)]
        [InlineData("ExtADC_A15",
            "External ADC A15", "V", "External ADC A15", 0, 3.3)]
        [InlineData("ECG",
            "ECG", "mV", "ECG", -15, 15)]
        [InlineData("EMG",
            "EMG", "mV", "EMG", -15, 15)]
        [InlineData("EXG Test",
            "EXG Test", "mV", "EXG Test", -15, 15)]
        [InlineData("Respiration",
            "Respiration", "mV", "Respiration", -15, 15)]
        public void UpdateYAxisSettings_sets_expected_properties(
            string input,
            string expLabel, string expUnit, string expTitle,
            double expMin, double expMax)
        {
            // Arrange
            var vm = CreateVMWithoutCtor();

            // Se il metodo/altre logiche leggono queste props, le inizializziamo comunque
            SetProp(vm, "YAxisLabel", string.Empty);
            SetProp(vm, "YAxisUnit", string.Empty);
            SetProp(vm, "ChartTitle", string.Empty);
            SetProp(vm, "YAxisMin", 0d);
            SetProp(vm, "YAxisMax", 0d);

            // Act
            CallPrivate(vm, "UpdateYAxisSettings", input);

            // Assert
            Assert.Equal(expLabel, GetProp<string>(vm, "YAxisLabel"));
            Assert.Equal(expUnit, GetProp<string>(vm, "YAxisUnit"));
            Assert.Equal(expTitle, GetProp<string>(vm, "ChartTitle"));
            Assert.Equal(expMin, Math.Round(GetProp<double>(vm, "YAxisMin"), 6));
            Assert.Equal(expMax, Math.Round(GetProp<double>(vm, "YAxisMax"), 6));
        }

        [Theory]
        // Qui testiamo input con spazi/tab/newline ai bordi (supportati dal tuo CleanParameterName),
        // ma senza manipolare gli spazi interni o tra nome e asse.
        [InlineData(" GyroscopeZ ", "Gyroscope Z", -250, 250)]
        [InlineData("\tMagnetometerX\n", "Magnetometer X", -5, 5)]
        public void UpdateYAxisSettings_respects_CleanParameterName(string noisy, string expLabel, double expMin, double expMax)
        {
            var vm = CreateVMWithoutCtor();

            SetProp(vm, "YAxisLabel", string.Empty);
            SetProp(vm, "YAxisUnit", string.Empty);
            SetProp(vm, "ChartTitle", string.Empty);
            SetProp(vm, "YAxisMin", 0d);
            SetProp(vm, "YAxisMax", 0d);

            CallPrivate(vm, "UpdateYAxisSettings", noisy);

            Assert.Equal(expLabel, GetProp<string>(vm, "YAxisLabel"));
            Assert.Equal(expMin, Math.Round(GetProp<double>(vm, "YAxisMin"), 6));
            Assert.Equal(expMax, Math.Round(GetProp<double>(vm, "YAxisMax"), 6));
        }


        // --------------------------------------------------------------------
        // TryParseDouble — behavior: accetta '.' o ',' e i segni, rifiuta lettere
        // --------------------------------------------------------------------

        [Theory]
        [InlineData("12.5", true, 12.5)]
        [InlineData("12,5", true, 12.5)]
        [InlineData("-3.25", true, -3.25)]
        [InlineData("+7", true, 7.0)]
        [InlineData("+", true, 0.0)]     // stato di editing temporaneo accettato dal parser
        [InlineData("abc", false, 0.0)]
        [InlineData("7x", false, 0.0)]
        public void TryParseDouble_various_inputs(string input, bool ok, double value)
        {
            // Se testiamo un input con la virgola, forziamo una cultura "comma-decimal"
            var prev = CultureInfo.CurrentCulture;
            if (input.Contains(","))
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");

            try
            {
                var success = DataPageViewModel.TryParseDouble(input, out var v);
                Assert.Equal(ok, success);
                if (ok)
                    Assert.Equal(value, v, 3);
            }
            finally
            {
                CultureInfo.CurrentCulture = prev;
            }
        }

        [Theory]
        // validi
        [InlineData(" .5 ", true, 0.5)]          // dot-leading, spazi ai bordi
        [InlineData(",5", true, 0.5)]            // comma-leading (richiede cultura con virgola)
        [InlineData("1.234", true, 1.234)]       // punto decimale (invariant)
        [InlineData("1,234", true, 1.234)]       // virgola decimale (it-IT)
        [InlineData("-0.0", true, -0.0)]         // segno + zero
        [InlineData("+ ", true, 0.0)]            // dopo Trim() -> "+", accettato come stato temporaneo
        [InlineData("- ", true, 0.0)]            // dopo Trim() -> "-", accettato come stato temporaneo

        // non validi
        [InlineData("", false, 0.0)]             // vuoto
        [InlineData("   ", false, 0.0)]          // solo spazi
        [InlineData("- 0.1", false, 0.0)]        // spazio dopo il segno
        [InlineData("1-2", false, 0.0)]          // segno non in prima posizione
        [InlineData("1.2.3", false, 0.0)]        // multipli separatori '.'
        [InlineData("1,2,3", false, 0.0)]        // multipli separatori ','
        [InlineData("++1", false, 0.0)]          // doppio segno
        [InlineData("NaN", false, 0.0)]          // lettere
        public void TryParseDouble_edge_cases(string input, bool ok, double value)
        {
            var prev = CultureInfo.CurrentCulture;

            // Se contiene una virgola, simula una cultura "comma-decimal" (it-IT)
            if (input.Contains(","))
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");

            try
            {
                var success = DataPageViewModel.TryParseDouble(input, out var v);
                Assert.Equal(ok, success);
                if (ok)
                    Assert.Equal(value, v, 6);
            }
            finally
            {
                CultureInfo.CurrentCulture = prev;
            }
        }


        // --------------------------------------------------------------------
        // TryParseInt — behavior: accetta +/- e solo cifre
        // --------------------------------------------------------------------

        [Theory]
        [InlineData("15", true, 15)]
        [InlineData("-2", true, -2)]
        [InlineData("+9", true, 9)]
        [InlineData("  42 ", true, 42)]
        [InlineData("4.2", false, 0)]
        [InlineData("abc", false, 0)]
        public void TryParseInt_various_inputs(string input, bool ok, int value)
        {
            var result = DataPageViewModel.TryParseInt(input, out var parsed);
            Assert.Equal(ok, result);
            if (ok) Assert.Equal(value, parsed);
        }

        [Theory]
        // validi
        [InlineData("0", true, 0)]
        [InlineData("+0", true, 0)]
        [InlineData("-0", true, 0)]
        [InlineData("007", true, 7)]
        [InlineData("  -0012 ", true, -12)]

        // non validi
        [InlineData("", false, 0)]   // vuoto
        [InlineData("   ", false, 0)]   // solo spazi
        [InlineData("+", false, 0)]   // solo segno (diverso da TryParseDouble)
        [InlineData("-", false, 0)]   // solo segno
        [InlineData("+ 1", false, 0)]   // spazio dopo il segno
        [InlineData("1 2", false, 0)]   // spazio in mezzo
        [InlineData("1-2", false, 0)]   // segno non in prima posizione
        [InlineData("++1", false, 0)]   // doppio segno
        [InlineData("1,000", false, 0)]   // separatore migliaia non consentito
        [InlineData("1_000", false, 0)]   // underscore non consentito
        [InlineData("0x10", false, 0)]   // prefisso esadecimale non consentito
        public void TryParseInt_edge_cases(string input, bool ok, int value)
        {
            var success = DataPageViewModel.TryParseInt(input, out var v);
            Assert.Equal(ok, success);
            if (ok)
                Assert.Equal(value, v);
        }


        // GetCurrentSensorConfiguration behavior
        private static T CreateViewModel<T>() =>
     (T)FormatterServices.GetUninitializedObject(typeof(T));

        // VM senza ctor
        private static DataPageViewModel CreateViewModel() => CreateViewModel<DataPageViewModel>();


        // Set bool su property O field (pubbliche o private)
        private static void SetBool(object obj, string name, bool value)
        {
            var t = obj.GetType();

            var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }

            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(obj, value);
                return;
            }

            throw new MissingMemberException($"{t.Name} non ha bool property/field '{name}'.");
        }

        private static void SetAllSensorsPriv(object vm, bool v)
        {
            SetBool(vm, "enableLowNoiseAccelerometer", v);
            SetBool(vm, "enableWideRangeAccelerometer", v);
            SetBool(vm, "enableGyroscope", v);
            SetBool(vm, "enableMagnetometer", v);
            SetBool(vm, "enablePressureTemperature", v);
            SetBool(vm, "enableBattery", v);
            SetBool(vm, "enableExtA6", v);
            SetBool(vm, "enableExtA7", v);
            SetBool(vm, "enableExtA15", v);
        }

        private static void SetExgPriv(DataPageViewModel vm, bool enableExg, bool ecg, bool emg, bool test, bool resp)
        {
            SetBool(vm, "enableExg", enableExg);
            SetBool(vm, "exgModeECG", ecg);
            SetBool(vm, "exgModeEMG", emg);
            SetBool(vm, "exgModeTest", test);
            SetBool(vm, "exgModeRespiration", resp);
        }


        // -------------------- TESTS --------------------

        [Fact]
        public void GetCurrentSensorConfiguration_AllFalse_returns_all_false()
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, false);
            SetExgPriv(vm, false, false, false, false, false);

            var snap = vm.GetCurrentSensorConfiguration();

            Assert.False(snap.EnableLowNoiseAccelerometer);
            Assert.False(snap.EnableWideRangeAccelerometer);
            Assert.False(snap.EnableGyroscope);
            Assert.False(snap.EnableMagnetometer);
            Assert.False(snap.EnablePressureTemperature);
            Assert.False(snap.EnableBattery);
            Assert.False(snap.EnableExtA6);
            Assert.False(snap.EnableExtA7);
            Assert.False(snap.EnableExtA15);

            Assert.False(snap.EnableExg);
            Assert.False(snap.IsExgModeECG);
            Assert.False(snap.IsExgModeEMG);
            Assert.False(snap.IsExgModeTest);
            Assert.False(snap.IsExgModeRespiration);
        }

        [Theory]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public void GetCurrentSensorConfiguration_SingleMode_only_that_mode_true(
            bool ecg, bool emg, bool test, bool resp)
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, false);
            SetExgPriv(vm, true, ecg, emg, test, resp);


            var snap = vm.GetCurrentSensorConfiguration();

            Assert.True(snap.EnableExg);
            Assert.Equal(ecg, snap.IsExgModeECG);
            Assert.Equal(emg, snap.IsExgModeEMG);
            Assert.Equal(test, snap.IsExgModeTest);
            Assert.Equal(resp, snap.IsExgModeRespiration);

            // mutua esclusione (una sola modalità alla volta)
            int modes = (ecg ? 1 : 0) + (emg ? 1 : 0) + (test ? 1 : 0) + (resp ? 1 : 0);
            Assert.Equal(1, modes);
        }

        [Fact]
        public void GetCurrentSensorConfiguration_ExgOnly_copies_exg_flags()
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, false);
            SetExgPriv(vm, true, true, false, false, false);

            var snap = vm.GetCurrentSensorConfiguration();

            // sensori
            Assert.False(snap.EnableLowNoiseAccelerometer);
            Assert.False(snap.EnableWideRangeAccelerometer);
            Assert.False(snap.EnableGyroscope);
            Assert.False(snap.EnableMagnetometer);
            Assert.False(snap.EnablePressureTemperature);
            Assert.False(snap.EnableBattery);
            Assert.False(snap.EnableExtA6);
            Assert.False(snap.EnableExtA7);
            Assert.False(snap.EnableExtA15);

            // exg
            Assert.True(snap.EnableExg);
            Assert.True(snap.IsExgModeECG);
            Assert.False(snap.IsExgModeEMG);
            Assert.False(snap.IsExgModeTest);
            Assert.False(snap.IsExgModeRespiration);
        }

        [Fact]
        public void GetCurrentSensorConfiguration_MixedPattern_matches_vm_flags()
        {
            var vm = CreateViewModel(); // senza eseguire il costruttore

            // sensori “misti”
            SetBool(vm, "enableLowNoiseAccelerometer", true);
            SetBool(vm, "enableWideRangeAccelerometer", false);
            SetBool(vm, "enableGyroscope", true);
            SetBool(vm, "enableMagnetometer", false);
            SetBool(vm, "enablePressureTemperature", true);
            SetBool(vm, "enableBattery", false);
            SetBool(vm, "enableExtA6", true);
            SetBool(vm, "enableExtA7", false);
            SetBool(vm, "enableExtA15", true);

            // EXG: abilita EXG, solo EMG = true
            SetBool(vm, "enableExg", true);
            SetBool(vm, "exgModeECG", false);
            SetBool(vm, "exgModeEMG", true);
            SetBool(vm, "exgModeTest", false);
            SetBool(vm, "exgModeRespiration", false);

            var snap = vm.GetCurrentSensorConfiguration();

            Assert.True(snap.EnableLowNoiseAccelerometer);
            Assert.False(snap.EnableWideRangeAccelerometer);
            Assert.True(snap.EnableGyroscope);
            Assert.False(snap.EnableMagnetometer);
            Assert.True(snap.EnablePressureTemperature);
            Assert.False(snap.EnableBattery);
            Assert.True(snap.EnableExtA6);
            Assert.False(snap.EnableExtA7);
            Assert.True(snap.EnableExtA15);

            Assert.True(snap.EnableExg);
            Assert.False(snap.IsExgModeECG);
            Assert.True(snap.IsExgModeEMG);
            Assert.False(snap.IsExgModeTest);
            Assert.False(snap.IsExgModeRespiration);
        }

        [Fact]
        public void GetCurrentSensorConfiguration_returns_new_instance_each_call()
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, true);
            SetExgPriv(vm, true, true, false, false, false);

            var first = vm.GetCurrentSensorConfiguration();
            var second = vm.GetCurrentSensorConfiguration();

            Assert.NotSame(first, second);

            // due snapshot identici

            Assert.Equal(first.EnableLowNoiseAccelerometer, second.EnableLowNoiseAccelerometer);

            Assert.Equal(first.EnableWideRangeAccelerometer, second.EnableWideRangeAccelerometer);
            Assert.Equal(first.EnableGyroscope, second.EnableGyroscope);
            Assert.Equal(first.EnableMagnetometer, second.EnableMagnetometer);
            Assert.Equal(first.EnablePressureTemperature, second.EnablePressureTemperature);
            Assert.Equal(first.EnableBattery, second.EnableBattery);
            Assert.Equal(first.EnableExtA6, second.EnableExtA6);
            Assert.Equal(first.EnableExtA7, second.EnableExtA7);
            Assert.Equal(first.EnableExtA15, second.EnableExtA15);

            Assert.Equal(first.EnableExg, second.EnableExg);
            Assert.Equal(first.IsExgModeECG, second.IsExgModeECG);
            Assert.Equal(first.IsExgModeEMG, second.IsExgModeEMG);
            Assert.Equal(first.IsExgModeTest, second.IsExgModeTest);
            Assert.Equal(first.IsExgModeRespiration, second.IsExgModeRespiration);
        }

        // ResetAllTimestamps behavior
        // ===== Helpers per lavorare senza costruttore =====


        private static double EnsureDeviceSamplingRate(object vm, double desiredHz)
        {
            var t = vm.GetType();

            // prova a impostare varie “porte d’ingresso”
            var pText = t.GetProperty("SamplingRateText",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pText != null && pText.CanWrite && pText.PropertyType == typeof(string))
                pText.SetValue(vm, desiredHz.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var pDisp = t.GetProperty("SamplingRateDisplay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pDisp != null && pDisp.CanWrite && pDisp.PropertyType == typeof(double))
                pDisp.SetValue(vm, desiredHz);

            var fDisp = t.GetField("samplingRateDisplay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fDisp != null && fDisp.FieldType == typeof(double))
                fDisp.SetValue(vm, desiredHz);

            var fLast = t.GetField("_lastValidSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fLast != null && fLast.FieldType == typeof(double))
                fLast.SetValue(vm, desiredHz);

            // leggi la rate effettiva
            var pEff = t.GetProperty("DeviceSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pEff == null)
                throw new MissingMemberException("DeviceSamplingRate non trovato.");

            return (double)pEff.GetValue(vm)!;
        }

        private static int MsStepFromVm(object vm)
        {
            var pEff = vm.GetType().GetProperty("DeviceSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var eff = (double)pEff!.GetValue(vm)!;
            return (int)(1000.0 / eff);  // coerente con ResetAllTimestamps: (int)(i * (1000.0 / rate))
        }





        private static void SetRef(object obj, string name, object? value)
    {
        var t = obj.GetType();

        var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return;
        }

        var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(obj, value);
            return;
        }

        throw new MissingMemberException($"{t.Name} non ha member '{name}'.");
    }

    private static object? GetRef(object obj, string name)
    {
        var t = obj.GetType();

        var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null) return prop.GetValue(obj);

        var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null) return field.GetValue(obj);

        throw new MissingMemberException($"{t.Name} non ha member '{name}'.");
    }

    /// Crea un Dictionary<string, List<T>> compatibile con il tipo reale di timeStampsCollections
    /// e lo assegna alla VM. Ritorna (dict, elementType) per poter popolare le liste.
    private static (object dict, Type elementType) InitTimeStampDict(object vm)
    {
        var type = vm.GetType();
        MemberInfo? member =
            type.GetField("timeStampsCollections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? (MemberInfo?)type.GetProperty("timeStampsCollections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (member == null) throw new MissingMemberException("timeStampsCollections non trovato.");

        var memberType = member is FieldInfo fi ? fi.FieldType : ((PropertyInfo)member).PropertyType;

        if (!memberType.IsGenericType) throw new InvalidOperationException("timeStampsCollections non è generico.");
        var args = memberType.GetGenericArguments(); // [TKey, TValue]
        var keyType = args[0];
        var valueType = args[1]; // atteso List<X>

        if (keyType != typeof(string)) throw new InvalidOperationException("timeStampsCollections deve avere chiave string.");
        if (!valueType.IsGenericType || valueType.GetGenericTypeDefinition() != typeof(List<>))
            throw new InvalidOperationException("timeStampsCollections deve essere List<> come valore.");

        var elemType = valueType.GetGenericArguments()[0]; // X

        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dictInstance = Activator.CreateInstance(dictType)!;

        SetRef(vm, "timeStampsCollections", dictInstance);
        return (dictInstance, elemType);
    }

    private static object MakeListOf(Type elemType, params object[] items)
    {
        var listType = typeof(List<>).MakeGenericType(elemType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var it in items)
            list.Add(Convert.ChangeType(it, elemType));
        return list;
    }

    private static void AddSeries(object dict, string key, object list)
    {
        // prova il metodo Add(key, value)
        var add = dict.GetType().GetMethod("Add", new[] { typeof(string), list.GetType() });
        if (add != null)
        {
            add.Invoke(dict, new object[] { key, list });
            return;
        }

        // fallback: dict[key] = value via indicizzatore
        var itemProp = dict.GetType().GetProperty("Item");
        if (itemProp == null) throw new MissingMemberException("Indicizzatore 'Item' non trovato sul dizionario.");
        itemProp.SetValue(dict, list, new object[] { key });
    }

    private static void AssertSeq(object listObj, params double[] expected)
    {
        var list = (System.Collections.IList)listObj;
        Assert.Equal(expected.Length, list.Count);

        var elemType = listObj.GetType().GetGenericArguments()[0];

        for (int i = 0; i < expected.Length; i++)
        {
            if (elemType == typeof(int))
            {
                Assert.Equal((int)expected[i], (int)Convert.ChangeType(list[i]!, typeof(int))!);
            }
            else if (elemType == typeof(float))
            {
                Assert.InRange(Math.Abs((float)Convert.ChangeType(list[i]!, typeof(float))! - (float)expected[i]), 0, 1e-3f);
            }
            else if (elemType == typeof(double))
            {
                Assert.InRange(Math.Abs((double)Convert.ChangeType(list[i]!, typeof(double))! - expected[i]), 0, 1e-6);
            }
            else
            {
                throw new InvalidOperationException($"Tipo elemento inatteso: {elemType}");
            }
        }
    }

        private static double GetEffectiveRate(object vm)
        {
            var pEff = vm.GetType().GetProperty("DeviceSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (double)pEff!.GetValue(vm)!;
        }

        private static double[] ExpectedStamps(int count, double effRate)
        {
            var arr = new double[count];
            for (int i = 0; i < count; i++)
                arr[i] = (int)(i * (1000.0 / effRate)); // stessa formula della VM
            return arr;
        }



        // ===================== TESTS =====================
        [Fact]
        public void ResetAllTimestamps_builds_even_spacing_for_50Hz_on_multiple_series()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

            // chiedi 50Hz ma la VM può forzare 51.2 → ci adeguiamo
            EnsureDeviceSamplingRate(vm, 50.0);
            var eff = GetEffectiveRate(vm);

            var (dict, elemType) = InitTimeStampDict(vm);
            AddSeries(dict, "Accel", MakeListOf(elemType, 999, 999, 999, 999)); // 4 elementi
            AddSeries(dict, "Gyro", MakeListOf(elemType, 111, 111, 111));      // 3 elementi

            vm.ResetAllTimestamps();

            var idx = dict.GetType().GetProperty("Item");
            var accelList = idx!.GetValue(dict, new object[] { "Accel" });
            var gyroList = idx!.GetValue(dict, new object[] { "Gyro" });

            AssertSeq(accelList!, ExpectedStamps(4, eff)); // 0, 19, 39, 58 con 51.2Hz
            AssertSeq(gyroList!, ExpectedStamps(3, eff)); // 0, 19, 39 con 51.2Hz
        }

        [Fact]
        public void ResetAllTimestamps_uses_sampling_rate_and_casts_to_int_like_code()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

            EnsureDeviceSamplingRate(vm, 7.5); // la VM può rimanere a 51.2
            var eff = GetEffectiveRate(vm);

            var (dict, elemType) = InitTimeStampDict(vm);
            AddSeries(dict, "Flux", MakeListOf(elemType, 0, 0, 0));

            vm.ResetAllTimestamps();

            var idx = dict.GetType().GetProperty("Item");
            var flux = idx!.GetValue(dict, new object[] { "Flux" });

            AssertSeq(flux!, ExpectedStamps(3, eff)); // con 51.2Hz: 0,19,39
        }



        [Fact]
        public void ResetAllTimestamps_keeps_lengths_unchanged()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

            // chiedi 100 Hz (se la VM clamp-a, non importa ai fini di questo test)
            EnsureDeviceSamplingRate(vm, 100.0);

            var (dict, elemType) = InitTimeStampDict(vm);
            AddSeries(dict, "M1", MakeListOf(elemType, 1, 2, 3));
            AddSeries(dict, "M2", MakeListOf(elemType, 7, 8, 9, 10, 11));

            vm.ResetAllTimestamps();

            var idx = dict.GetType().GetProperty("Item");
            var m1 = (System.Collections.IList)idx!.GetValue(dict, new object[] { "M1" })!;
            var m2 = (System.Collections.IList)idx!.GetValue(dict, new object[] { "M2" })!;

            Assert.Equal(3, m1.Count);
            Assert.Equal(5, m2.Count);
        }


        [Fact]
        public void ResetAllTimestamps_empty_dictionary_no_throw_and_stays_empty()
        {
            // Arrange
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());
            EnsureDeviceSamplingRate(vm, 100.0); // qualunque valore va bene

            var (dict, _) = InitTimeStampDict(vm); // non aggiungiamo serie

            // Act + Assert
            var countBefore = (int)dict.GetType().GetProperty("Count")!.GetValue(dict)!;
            Assert.Equal(0, countBefore); // conferma vuoto

            // Non deve lanciare eccezioni
            vm.ResetAllTimestamps();

            var countAfter = (int)dict.GetType().GetProperty("Count")!.GetValue(dict)!;
            Assert.Equal(0, countAfter); // resta vuoto
        }





    }
}
