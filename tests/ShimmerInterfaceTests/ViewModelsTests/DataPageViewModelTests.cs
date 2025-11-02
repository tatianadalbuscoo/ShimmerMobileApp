/*
 * DataPageViewModelTests.cs
 * Purpose: Unit tests for DataPageViewModel file.
 */


using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using ShimmerSDK.IMU;
using System.Globalization;
using System.Reflection;
using ShimmerSDK.EXG;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;



namespace ShimmerInterfaceTests.ViewModelsTests
{
    public class DataPageViewModelTests
    {

        /// <summary>
        /// Helper: builds a <see cref="ShimmerDevice"/> with all IMU-related flags enabled; EXG is left off.
        /// </summary>
        /// <returns>A <see cref="ShimmerDevice"/> configured with IMU flags on and EXG off.</returns>
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
                EnableExg = false
            };
        }


        /// <summary>
        /// Helper: returns a fake EXG device + config with all flags enabled and ECG mode.
        /// </summary>
        /// <returns>A tuple of (<see cref="ShimmerSDK_EXG"/>, <see cref="ShimmerDevice"/>).</returns>
        private static (ShimmerSDK.EXG.ShimmerSDK_EXG exg, ShimmerInterface.Models.ShimmerDevice cfg) MakeExgDeviceWithAllFlags()
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


        /// <summary>
        /// Helper: builds a fake IMU + config (all flags on) with a default sampling rate of 51.2 Hz.
        /// Expected: returned tuple is non-null.
        /// </summary>
        /// <returns>A tuple of (<see cref="ShimmerSDK_IMU"/>, <see cref="ShimmerDevice"/>).</returns>
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


        /// <summary>
        /// Helper: reads a private field via reflection (generic).
        /// Expected: returns field value cast to T.
        /// </summary>
        /// <typeparam name="T">Field value type.</typeparam>
        /// <param name="target">Instance to read from.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <returns>The field value cast to <typeparamref name="T"/>.</returns>
        static T GetPrivate<T>(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            return (T)f!.GetValue(target)!;
        }


        /// <summary>
        /// Helper: sets a private instance field via reflection (generic).
        /// </summary>
        /// <typeparam name="T">Field value type.</typeparam>
        /// <param name="target">The instance whose field will be set.</param>
        /// <param name="field">The private field name.</param>
        /// <param name="value">The value to assign to the field.</param>
        static void SetPrivate(object target, string field, object? value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }


        /// <summary>
        /// Helper: invokes a private instance method by name with no parameters.
        /// </summary>
        /// <param name="target">The instance that owns the private method.</param>
        /// <param name="method">The private method name to invoke.</param>
        static void InvokePrivate(object target, string method)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m);
            m!.Invoke(target, null);
        }


        /// <summary>
        /// Helper: builds a <see cref="ShimmerDevice"/> with all IMU flags enabled and EXG selectable.
        /// </summary>
        /// <param name="exg">If true, EXG is enabled; otherwise disabled.</param>
        /// <returns>A configured <see cref="ShimmerDevice"/> instance.</returns>
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


        // ----- ChartDisplayMode behavior -----


        /// <summary>
        /// ChartDisplayMode & labels — verifies transitions between Multi and Split for IMU/EXG,
        /// and that derived labels reflect the selected mode.
        /// Expected: selecting split variants switches to Split; regular groups keep Multi; EXG split toggles IsExgSplit; labels match the mode.
        /// </summary>
        [Fact(DisplayName = "ChartDisplayMode: default is Multi")]
        public void ChartDisplayMode_default_is_Multi()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
        }


        /// <summary>
        /// Selecting the IMU split variant switches to Split and updates the mode label.
        /// Expected: mode == Split; <see cref="DataPageViewModel.ChartModeLabel"/> == "Split (three separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartDisplayMode: selecting IMU split variant switches to Split")]
        public void ChartDisplayMode_switches_to_Split_when_selecting_IMU_split_label()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";

            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel);
        }


        /// <summary>
        /// Selecting a regular IMU group keeps Multi mode and the corresponding label.
        /// Expected: mode == Multi; label == "Multi Parameter (X, Y, Z)".
        /// </summary>
        [Fact(DisplayName = "ChartDisplayMode: selecting a regular IMU group keeps Multi")]
        public void ChartDisplayMode_stays_Multi_for_regular_IMU_group()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);
        }


        /// <summary>
        /// Multi/Split transitions for EXG reflect in label and IsExgSplit.
        /// Expected: "ECG" in Multi → IsExgSplit=false; split variant → IsExgSplit=true and label="Split (two separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartDisplayMode + EXG: Multi/Split and ChartModeLabel are consistent")]
        public void ChartDisplayMode_EXG_multi_vs_split_label()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";
            Assert.Equal(ChartDisplayMode.Multi, vm.ChartDisplayMode);
            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);
            Assert.False(vm.IsExgSplit);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
            Assert.True(vm.IsExgSplit);
        }


        /// <summary>
        /// IsExgSplit is true only when mode is Split and the selected family is EXG.
        /// Expected: Split+EXG → true; Split+IMU → false; Multi+EXG → false.
        /// </summary>
        [Fact(DisplayName = "IsExgSplit: true ONLY when Split and EXG-like parameter")]
        public void IsExgSplit_true_only_when_Split_and_EXG_family()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";
            Assert.False(vm.IsExgSplit);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            Assert.True(vm.IsExgSplit);

            var (imu, cfgImu) = MakeImuDeviceWithAllFlags();
            vm = new DataPageViewModel(imu, cfgImu);
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";
            Assert.Equal(ChartDisplayMode.Split, vm.ChartDisplayMode);
            Assert.False(vm.IsExgSplit);
        }


        /// <summary>
        /// IMU labels are consistent for both modes.
        /// Expected: Gyroscope → "Multi Parameter (X, Y, Z)"; split variant → "Split (three separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: IMU — Multi and Split return correct descriptions")]
        public void ChartModeLabel_IMU_texts_are_correct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Multi
            vm.SelectedParameter = "Gyroscope";
            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);

            // Split
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";
            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel);
        }


        /// <summary>
        /// EXG labels are consistent for both modes.
        /// Expected: ECG → "Multi Parameter (EXG1, EXG2)"; split variant → "Split (two separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: EXG — Multi and Split return correct descriptions")]
        public void ChartModeLabel_EXG_texts_are_correct()
        {
            var (exg, cfg) = MakeExgDeviceWithAllFlags();
            var vm = new DataPageViewModel(exg, cfg);

            // Multi
            vm.SelectedParameter = "ECG";
            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);

            // Split
            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
        }


        // ----- Text-entry bindable properties -----


        /// <summary>
        /// Helper: sets a private instance field via reflection (generic).
        /// </summary>
        /// <typeparam name="T">Field value type.</typeparam>
        /// <param name="target">Instance to mutate.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <param name="value">Value to assign.</param>
        static void SetPrivate<T>(object target, string fieldName, T value)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }


        /// <summary>
        /// Helper: sets a public or non-public property via reflection (generic).
        /// Expected: property set succeeds.
        /// </summary>
        /// <typeparam name="T">Property value type.</typeparam>
        /// <param name="target">Instance to mutate.</param>
        /// <param name="propName">Property name.</param>
        /// <param name="value">Value to assign.</param>
        static void SetProp<T>(object target, string propName, T value)
        {
            var p = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(p);
            p!.SetValue(target, value);
        }


        /// <summary>
        /// SamplingRateText mirrors raw input without validation at set-time.
        /// Expected: text changes; numeric display and validation message unchanged.
        /// </summary>
        [Fact(DisplayName = "SamplingRateText: invalid set does nothing (text mirror only)")]
        public void SamplingRateText_sets_text_only_without_validation_or_numeric_change()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            var beforeRate = vm.SamplingRateDisplay;
            var beforeMsg = vm.ValidationMessage;

            vm.SamplingRateText = "abc";

            Assert.Equal("abc", vm.SamplingRateText);
            Assert.Equal(beforeRate, vm.SamplingRateDisplay); 
            Assert.Equal(beforeMsg, vm.ValidationMessage);    
        }


        /// <summary>
        /// YAxisMin/Max text setters only affect the text mirror.
        /// Expected: text updated; numeric min/max unchanged; no validation message.
        /// </summary>
        [Fact(DisplayName = "YAxisMinText/YAxisMaxText: setters update text only, not numeric values")]
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
            Assert.Equal(yMinBefore, vm.YAxisMin); 
            Assert.Equal(yMaxBefore, vm.YAxisMax);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }


        /// <summary>
        /// Valid TimeWindowSecondsText updates numeric value and clears buffers.
        /// Expected: numeric becomes 30, message empty.
        /// </summary>
        [Fact(DisplayName = "TimeWindowSecondsText: valid → updates property and resets timeline")]
        public void TimeWindowSecondsText_valid_updates_value_and_clears_buffers()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            Assert.Equal(20, vm.TimeWindowSeconds);
            Assert.Equal("20", vm.TimeWindowSecondsText);

            vm.TimeWindowSecondsText = "30";

            Assert.Equal(30, vm.TimeWindowSeconds);
            Assert.Equal(30, vm.TimeWindowSeconds);

            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }


        /// <summary>
        /// Invalid TimeWindowSecondsText resets to last valid and shows an error.
        /// Expected: text reset to "20"; numeric remains 20; message contains "Time Window".
        /// </summary>
        /// <param name="input">Invalid input.</param>
        [Theory(DisplayName = "TimeWindowSecondsText: invalid input → error message + text reset")]
        [InlineData("abc")]
        [InlineData("+")]
        public void TimeWindowSecondsText_invalid_shows_error_and_resets(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.TimeWindowSecondsText = input;

            Assert.Contains("Time Window", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("20", vm.TimeWindowSecondsText);
            Assert.Equal(20, vm.TimeWindowSeconds);
        }


        /// <summary>
        /// Whitespace-only input is ignored.
        /// Expected: text remains " "; numeric unchanged; no validation message.
        /// </summary>
        [Fact(DisplayName = "TimeWindowSecondsText: whitespace only → no error, no reset, numeric unchanged")]
        public void TimeWindowSecondsText_whitespace_is_noop()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeNumeric = vm.TimeWindowSeconds;

            vm.TimeWindowSecondsText = " ";

            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.Equal(" ", vm.TimeWindowSecondsText);
            Assert.Equal(beforeNumeric, vm.TimeWindowSeconds);
        }


        /// <summary>
        /// Out-of-range TimeWindowSecondsText resets to last valid.
        /// Expected: message contains "too small"/"too large"; text reset to "20"; numeric is 20.
        /// </summary>
        /// <param name="input">Provided input.</param>
        /// <param name="snippet">Expected snippet in error.</param>
        [Theory(DisplayName = "TimeWindowSecondsText: min/max out of range → error + reset")]
        [InlineData("0", "too small")]
        [InlineData("10000", "too large")]
        public void TimeWindowSecondsText_out_of_range_triggers_error_and_reset(string input, string snippet)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.TimeWindowSecondsText = input;

            Assert.Contains(snippet, vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("20", vm.TimeWindowSecondsText);
            Assert.Equal(20, vm.TimeWindowSeconds);
        }


        /// <summary>
        /// Valid XAxisLabelIntervalText updates numeric property.
        /// Expected: interval becomes 7; message empty.
        /// </summary>
        [Fact(DisplayName = "XAxisLabelIntervalText: valid → updates property")]
        public void XAxisLabelIntervalText_valid_updates_property()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.Equal("5", vm.XAxisLabelIntervalText);

            vm.XAxisLabelIntervalText = "7";

            Assert.Equal(7, vm.XAxisLabelInterval);
            Assert.Equal("7", vm.XAxisLabelIntervalText);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }


        /// <summary>
        /// Invalid/out-of-range XAxisLabelIntervalText resets to last valid.
        /// Expected: message contains snippet; text reset to "5"; numeric is 5.
        /// </summary>
        /// <param name="input">User input.</param>
        /// <param name="snippet">Expected snippet in error.</param>
        [Theory(DisplayName = "XAxisLabelIntervalText: out of range/invalid → error + reset")]
        [InlineData("0", "too low")]          
        [InlineData("20000", "too high")]
        [InlineData("abc", "must be a valid positive number")]
        public void XAxisLabelIntervalText_out_of_range_or_invalid_resets_and_errors(string input, string snippet)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.XAxisLabelIntervalText = input;

            Assert.Contains(snippet, vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("5", vm.XAxisLabelIntervalText);
            Assert.Equal(5, vm.XAxisLabelInterval);
        }


        // ----- IsExgSplit behavior -----


        /// <summary>
        /// Split + ECG should mark IsExgSplit true.
        /// Expected: <see cref="DataPageViewModel.IsExgSplit"/> == true.
        /// </summary>
        [Fact(DisplayName = "IsExgSplit: Split + ECG → true")]
        public void IsExgSplit_split_with_ecg_true()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "ECG";                 
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.True(vm.IsExgSplit);
        }


        /// <summary>
        /// Split + EXG split-variant labels should mark IsExgSplit true.
        /// Expected: IsExgSplit == true for "→ EMG — separate charts (EXG1·EXG2)".
        /// </summary>
        [Fact(DisplayName = "IsExgSplit: Split + EMG (split variant label) → true")]
        public void IsExgSplit_split_with_emg_split_variant_true()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "    → EMG — separate charts (EXG1·EXG2)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.True(vm.IsExgSplit);
        }


        /// <summary>
        /// Multi + ECG is not considered EXG split.
        /// Expected: IsExgSplit == false.
        /// </summary>
        [Fact(DisplayName = "IsExgSplit: Multi + ECG → false")]
        public void IsExgSplit_multi_with_ecg_false()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "ECG";
            vm.ChartDisplayMode = ChartDisplayMode.Multi;

            Assert.False(vm.IsExgSplit);
        }


        /// <summary>
        /// Split + IMU groups do not count as EXG split.
        /// Expected: IsExgSplit == false for IMU groups.
        /// </summary>
        [Fact(DisplayName = "IsExgSplit: Split + Gyroscope (IMU) → false")]
        public void IsExgSplit_split_with_imu_group_false()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.False(vm.IsExgSplit);
        }


        // ----- CurrentTimeInSeconds behavior -----


        /// <summary>
        /// Computes current time from sampleCounter / samplingRate with no baseline.
        /// Expected: 100/50.0 == 2.0 seconds.
        /// </summary>
        [Fact(DisplayName = "CurrentTimeInSeconds: baseline calculation (sampleCounter/rate, no baseline)")]
        public void CurrentTimeInSeconds_basic()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate<int>(vm, "sampleCounter", 100);
            SetPrivate<double>(vm, "timeBaselineSeconds", 0.0);

            Assert.Equal(2.0, vm.CurrentTimeInSeconds, precision: 5);
        }


        /// <summary>
        /// Subtracts a non-zero baseline from the computed time.
        /// Expected: 100/50.0 - 1.5 == 0.5 seconds.
        /// </summary>
        [Fact(DisplayName = "CurrentTimeInSeconds: with baseline (subtraction)")]
        public void CurrentTimeInSeconds_with_baseline()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate<int>(vm, "sampleCounter", 100);
            SetPrivate<double>(vm, "timeBaselineSeconds", 1.5);

            Assert.Equal(0.5, vm.CurrentTimeInSeconds, precision: 5);
        }


        /// <summary>
        /// Negative results are clamped to zero.
        /// Expected: baseline > current -> CurrentTimeInSeconds == 0.
        /// </summary>
        [Fact(DisplayName = "CurrentTimeInSeconds: clamped to zero if baseline > current")]
        public void CurrentTimeInSeconds_clamped_at_zero()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, cfg);

            // current=40/50=0.8, baseline=1.2 -> negative -> clamp at 0
            SetPrivate<int>(vm, "sampleCounter", 40);
            SetPrivate<double>(vm, "timeBaselineSeconds", 1.2);

            Assert.Equal(0.0, vm.CurrentTimeInSeconds, precision: 5);
        }


        // ----- ApplyYMinCommand / ApplyYMaxCommand behavior -----


        /// <summary>
        /// ApplyYMinCommand with valid input updates YAxisMin.
        /// Expected: YAxisMin == -100; ValidationMessage empty.
        /// </summary>
        [Fact(DisplayName = "ApplyYMinCommand: valid value → updates YAxisMin and clears errors")]
        public void ApplyYMinCommand_applies_valid_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMinText = "-100";
            vm.YAxisMaxText = "200";

            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(-100, vm.YAxisMin, 5);
            Assert.Equal("", vm.ValidationMessage);
        }


        /// <summary>
        /// ApplyYMinCommand rejects values >= YAxisMax.
        /// Expected: ValidationMessage contains "cannot be greater"; YAxisMinText rolled back to numeric current.
        /// </summary>
        [Fact(DisplayName = "ApplyYMinCommand: >= YMax -> error and text rollback")]
        public void ApplyYMinCommand_blocks_when_ge_than_max()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg)
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMinText = "300";
            vm.YAxisMaxText = "250";

            vm.ApplyYMinCommand.Execute(null);

            Assert.Contains("cannot be greater", vm.ValidationMessage, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(vm.YAxisMin.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMinText);
        }


        /// <summary>
        /// ApplyYMaxCommand with valid input updates YAxisMax.
        /// Expected: YAxisMax == 10; ValidationMessage empty.
        /// </summary>
        [Fact(DisplayName = "ApplyYMaxCommand: valid value -> updates YAxisMax")]
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


        /// <summary>
        /// ApplyYMaxCommand rejects values &lt;= YAxisMin.
        /// Expected: ValidationMessage contains "less than or equal"; YAxisMax remains previous valid.
        /// </summary>
        [Fact(DisplayName = "ApplyYMaxCommand: <= YMin -> error")]
        public void ApplyYMaxCommand_blocks_when_le_than_min()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.AutoYAxis = false;               
            vm.IsYAxisManualEnabled = true;

            vm.YAxisMin = 5;       
            vm.YAxisMaxText = "4";       

            (vm.ApplyYMaxCommand as RelayCommand)!.Execute(null);

            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.YAxisMin);       

            Assert.True(vm.YAxisMax > vm.YAxisMin);
        }


        // ----- ApplySamplingRateCommand behavior -----


        /// <summary>
        /// Invalid format shows error and resets text to last valid.
        /// Expected: ValidationMessage contains "valid number"; SamplingRateText == "51.2".
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact(DisplayName = "ApplySamplingRateCommand: invalid format -> error message + reset")]
        public async Task ApplySamplingRateCommand_invalid_format_sets_error()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SamplingRateText = "abc";

            await vm.ApplySamplingRateCommand.ExecuteAsync(null);

            Assert.Contains("valid number", vm.ValidationMessage, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal("51.2", vm.SamplingRateText);
        }


        /// <summary>
        /// Out-of-range values surface appropriate messages.
        /// Expected: message contains "too low" or "too high" accordingly.
        /// </summary>
        /// <param name="input">Input rate as text.</param>
        /// <param name="expectedPart">Expected message snippet.</param>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Theory(DisplayName = "ApplySamplingRateCommand: out of range -> message")]
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


        /// <summary>
        /// With no devices attached, the requested value is used.
        /// Expected: SamplingRateDisplay == requested; ValidationMessage empty; busy events emitted.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact(DisplayName = "ApplySamplingRateCommand: no devices (forced null) -> uses requested value")]
        public async Task ApplySamplingRateCommand_no_devices_uses_requested_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate<object?>(vm, "shimmerImu", null);
            SetPrivate<object?>(vm, "shimmerExg", null);

            int showBusy = 0, hideBusy = 0;
            vm.ShowBusyRequested += (_, __) => showBusy++;
            vm.HideBusyRequested += (_, __) => hideBusy++;

            vm.SamplingRateText = "25";

            await vm.ApplySamplingRateCommand.ExecuteAsync(null);

            Assert.Equal(25, vm.SamplingRateDisplay, 5);
            Assert.Equal("", vm.ValidationMessage);
            Assert.True(showBusy >= 1 && hideBusy >= 1);
        }


        // ----- DeviceSamplingRate behavior -----


        /// <summary>
        /// IMU sampling rate is preferred when available.
        /// Expected: samplingRateDisplay == IMU.SamplingRate (42.0).
        /// </summary>
        [Fact(DisplayName = "DeviceSamplingRate: IMU present -> samplingRateDisplay = IMU")]
        public void DeviceSamplingRate_prefers_imu_when_present()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            SetProp<double>(imu, "SamplingRate", 42.0);
            var vm = new DataPageViewModel(imu, cfg);

            var display = GetPrivate<double>(vm, "samplingRateDisplay");
            Assert.Equal(42.0, display, 5);
        }


        /// <summary>
        /// EXG sampling rate is used when IMU is absent.
        /// Expected: samplingRateDisplay == EXG.SamplingRate (77.0).
        /// </summary>
        [Fact(DisplayName = "DeviceSamplingRate: EXG only -> samplingRateDisplay = EXG")]
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


        // ----- DeviceStartStreaming behavior -----


        /// <summary>
        /// Verifies private DeviceStartStreaming is null-safe when device refs are missing.
        /// Expected: invoking DeviceStartStreaming with shimmerImu/shimmerExg = null does not throw.
        /// </summary>
        [Fact(DisplayName = "DeviceStartStreaming: devices = null -> does not throw")]
        public void StartStreaming_null_devices_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => InvokePrivate(vm, "DeviceStartStreaming"));
            Assert.Null(ex);
        }


        // ----- DeviceStopStreaming behavior -----


        /// <summary>
        /// Verifies private DeviceStopStreaming is null-safe when device refs are missing.
        /// Expected: invoking DeviceStopStreaming with shimmerImu/shimmerExg = null does not throw.
        /// </summary>
        [Fact(DisplayName = "DeviceStopStreaming: devices = null -> does not throw")]
        public void StopStreaming_null_devices_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            // Force device absence
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => InvokePrivate(vm, "DeviceStopStreaming"));
            Assert.Null(ex);
        }


        // ----- SetFirmwareSamplingRateNearestUnified behavior -----


        /// <summary>
        /// Helper: invokes the private SetFirmwareSamplingRateNearestUnified and returns the result.
        /// </summary>
        /// <param name="vm">Target <see cref="DataPageViewModel"/> instance.</param>
        /// <param name="newRate">Requested sampling rate.</param>
        /// <returns>The nearest sampling rate accepted by the device logic.</returns>
        static double InvokeSetNearest(DataPageViewModel vm, double newRate)
        {
            var m = typeof(DataPageViewModel).GetMethod(
                "SetFirmwareSamplingRateNearestUnified",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.NotNull(m);
            return (double)m!.Invoke(vm, new object[] { newRate })!;
        }


        /// <summary>
        /// When no devices are attached, the "nearest" sampler should return the input value.
        /// Expected: actual == requested (within double precision).
        /// </summary>
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: no devices -> returns input")]
        public void SetNearest_no_devices_returns_input()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            // Force device absence
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var requested = 37.5;
            var actual = InvokeSetNearest(vm, requested);

            Assert.Equal(requested, actual, 6);
        }


        /// <summary>
        /// IMU-present path must execute without exceptions.
        /// Expected: invoking SetFirmwareSamplingRateNearestUnified does not throw.
        /// </summary>
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: IMU present -> no throw")]
        public void SetNearest_with_imu_present_does_not_throw()
        {
            var imu = new ShimmerSDK_IMU();
            var vm = new DataPageViewModel(imu, Cfg(exg: false));

            var ex = Record.Exception(() => InvokeSetNearest(vm, 25.0));
            Assert.Null(ex);
        }


        /// <summary>
        /// EXG-present path must execute without exceptions.
        /// Expected: invoking SetFirmwareSamplingRateNearestUnified does not throw.
        /// </summary>
        [Fact(DisplayName = "SetFirmwareSamplingRateNearestUnified: EXG present → no throw")]
        public void SetNearest_with_exg_present_does_not_throw()
        {
            var exg = new ShimmerSDK_EXG();
            var vm = new DataPageViewModel(exg, Cfg(exg: true));

            var ex = Record.Exception(() => InvokeSetNearest(vm, 25.0));
            Assert.Null(ex);
        }


        // ----- SubscribeSamples / UnsubscribeSamples behavior -----


        /// <summary>
        /// Ensures private SubscribeSamples/UnsubscribeSamples are idempotent and null-safe.
        /// Expected: repeated calls do not throw; calling with null devices does not throw.
        /// </summary>
        [Fact(DisplayName = "Subscribe/Unsubscribe: idempotent and safe with null")]
        public void Subscribe_Unsubscribe_idempotent_and_safe()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            var ex1 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));
            var ex2 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));    // repeat
            var ex3 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples"));
            var ex4 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples"));  // repeat

            Assert.Null(ex1); Assert.Null(ex2); Assert.Null(ex3); Assert.Null(ex4);

            // With devices = null
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex5 = Record.Exception(() => InvokePrivate(vm, "SubscribeSamples"));
            var ex6 = Record.Exception(() => InvokePrivate(vm, "UnsubscribeSamples"));
            Assert.Null(ex5); Assert.Null(ex6);
        }


        // ----- Constructor behavior -----

        /// <summary>
        /// Helper: builds a <see cref="ShimmerDevice"/> with all IMU flags enabled and optional EXG.
        /// </summary>
        /// <param name="exgOn">Whether EXG should be enabled (also selects ECG mode).</param>
        /// <returns>A configured <see cref="ShimmerDevice"/>.</returns>
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
            IsExgModeECG = exgOn,
            IsExgModeEMG = false,
            IsExgModeTest = false,
            IsExgModeRespiration = false
        };


        /// <summary>
        /// IMU constructor initializes sampling rate from device, builds parameter list, syncs selection/Y-axis, mirrors text, and snapshots "last valid".
        /// Expected: SamplingRateDisplay == device SR; IMU params present; selecting "Gyroscope" yields "deg/s" and ±250; text mirrors numeric; last-valids match current.
        /// </summary>
        [Fact(DisplayName = "Ctor(IMU): SamplingRateDisplay from IMU, consistent UI params, mirrored texts")]
        public void Constructor_IMU_initializes_state_and_parameters()
        {
            var imu = new ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 99.0);
            var cfg = ConfigAllOn(exgOn: false);

            // Act
            var vm = new DataPageViewModel(imu, cfg);

            Assert.Equal(99.0, vm.SamplingRateDisplay, 5);

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

            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedParameter));
            Assert.Contains(vm.SelectedParameter, vm.AvailableParameters);


            vm.SelectedParameter = "Gyroscope";
            Assert.Equal("Gyroscope", vm.YAxisLabel);
            Assert.Equal("deg/s", vm.YAxisUnit);
            Assert.Equal(-250, vm.YAxisMin);
            Assert.Equal(250, vm.YAxisMax);

            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);
            Assert.Equal(vm.SamplingRateDisplay.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

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


        /// <summary>
        /// Verifies that the EXG constructor initializes sampling rate from the device,
        /// populates EXG (and IMU) parameters, sets a valid SelectedParameter, updates
        /// Y-axis metadata for EXG, mirrors text fields, and snapshots “last valid” values.
        /// Expected: SamplingRateDisplay equals EXG device SR; "ECG" entries appear; selecting "ECG"
        /// yields label "ECG", unit "mV", and range [-15, 15]; mirrored texts match numerics; last-valids equal currents.
        /// </summary>
        [Fact(DisplayName = "Ctor(EXG): SamplingRateDisplay from EXG, consistent UI params, mirrored texts")]
        public void Constructor_EXG_initializes_state_and_parameters()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 77.0);
            var cfg = ConfigAllOn(exgOn: true);

            var vm = new DataPageViewModel(exg, cfg);

            Assert.Equal(77.0, vm.SamplingRateDisplay, 5);

            Assert.Contains("ECG", vm.AvailableParameters);
            Assert.Contains("    → ECG — separate charts (EXG1·EXG2)", vm.AvailableParameters);
            Assert.Contains("Low-Noise Accelerometer", vm.AvailableParameters);

            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedParameter));
            Assert.Contains(vm.SelectedParameter, vm.AvailableParameters);

            vm.SelectedParameter = "ECG";
            Assert.Equal("ECG", vm.YAxisLabel);
            Assert.Equal("mV", vm.YAxisUnit);
            Assert.Equal(-15, vm.YAxisMin);
            Assert.Equal(15, vm.YAxisMax);

            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);
            Assert.Equal(vm.SamplingRateDisplay.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

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


        // ----- Methods under test: -----
        // - public void Dispose() 
        // - protected virtual void Dispose(bool disposing) 


        /// <summary>
        /// Helper: attempts to read the event's backing delegate field by event name.
        /// For auto-implemented events, the backing field typically matches the event name.
        /// </summary>
        /// <param name="target">The instance that declares the event.</param>
        /// <param name="eventName">The event name to look up.</param>
        /// <returns>The underlying <see cref="Delegate"/> if found; otherwise <c>null</c>.</returns>
        static Delegate? GetEventDelegate(object target, string eventName)
        {
            var f = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(target) as Delegate;
        }


        /// <summary>
        /// Ensures Dispose() clears internal buffers and detaches ChartUpdateRequested.
        /// Expected: data/time collections for seeded keys become empty; ChartUpdateRequested backing delegate is null.
        /// </summary>
        [Fact(DisplayName = "Dispose(): clears buffers and nulls ChartUpdateRequested")]
        public void Dispose_clears_collections_and_nulls_event()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            // Attach a handler to ChartUpdateRequested
            EventHandler handler = (_, __) => { };
            vm.ChartUpdateRequested += handler;

            // Seed internal buffers
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            dataDict["GyroscopeX"] = new List<float> { 1f, 2f, 3f };
            timeDict["GyroscopeX"] = new List<int> { 10, 20, 30 };

            // Pre-conditions
            Assert.True(dataDict["GyroscopeX"].Count > 0);
            Assert.True(timeDict["GyroscopeX"].Count > 0);
            Assert.NotNull(GetEventDelegate(vm, "ChartUpdateRequested"));

            // Act
            vm.Dispose();

            // Buffers cleared
            Assert.Empty(dataDict["GyroscopeX"]);
            Assert.Empty(timeDict["GyroscopeX"]);

            // Event detached
            Assert.Null(GetEventDelegate(vm, "ChartUpdateRequested"));
        }


        /// <summary>
        /// Verifies double-dispose is safe and does not throw.
        /// Expected: second invocation of Dispose() does not throw.
        /// </summary>
        [Fact(DisplayName = "Dispose(): idempotent (second call does not throw)")]
        public void Dispose_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            vm.Dispose(); // first
            var ex = Record.Exception(() => vm.Dispose()); // second
            Assert.Null(ex);
        }


        /// <summary>
        /// Ensures Dispose() does not throw with IMU device present.
        /// Expected: calling Dispose() completes without exception.
        /// </summary>
        [Fact(DisplayName = "Dispose(): does not throw with IMU")]
        public void Dispose_with_imu_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));
            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }


        /// <summary>
        /// Ensures Dispose() does not throw with EXG device present.
        /// Expected: calling Dispose() completes without exception.
        /// </summary>
        [Fact(DisplayName = "Dispose(): does not throw with EXG")]
        public void Dispose_with_exg_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_EXG(), Cfg(exg: true));
            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }


        /// <summary>
        /// Invokes the protected Dispose(bool) via reflection with disposing=false to cover that branch.
        /// Expected: calling the non-public overload with false does not throw.
        /// </summary>
        [Fact(DisplayName = "Dispose(bool): disposing=false does not crash")]
        public void Dispose_bool_false_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            var m = typeof(DataPageViewModel).GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic, new Type[] { typeof(bool) });
            Assert.NotNull(m);

            var ex = Record.Exception(() => m!.Invoke(vm, new object[] { false }));
            Assert.Null(ex);
        }


        /// <summary>
        /// Ensures Dispose() remains safe when shimmerImu/shimmerExg are null,
        /// covering the branch that attempts to unsubscribe with null devices.
        /// Expected: no exception thrown.
        /// </summary>
        [Fact(DisplayName = "Dispose(): safe with shimmerImu/shimmerExg = null")]
        public void Dispose_safe_with_null_devices()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: true));

            // Force devices to null to cover unsubscribe path with null
            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var ex = Record.Exception(() => vm.Dispose());
            Assert.Null(ex);
        }


        // ----- OnIsYAxisManualEnabledChanged behavior -----


        /// <summary>
        /// Helper: creates a ShimmerDevice with all IMU-related flags enabled and EXG disabled.
        /// </summary>
        /// <returns>A configured <see cref="ShimmerDevice"/> instance.</returns>
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


        /// <summary>
        /// Toggling IsYAxisManualEnabled should raise CanExecuteChanged on both ApplyYMin and ApplyYMax commands.
        /// Expected: each command's CanExecuteChanged fires at least once per toggle.
        /// </summary>
        [Fact(DisplayName = "OnIsYAxisManualEnabledChanged: toggle raises CanExecuteChanged on both commands")]
        public void IsYAxisManualEnabled_toggle_raises_CanExecuteChanged_for_both_commands()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg());

            int yMinChanged = 0, yMaxChanged = 0;
            vm.ApplyYMinCommand.CanExecuteChanged += (_, __) => yMinChanged++;
            vm.ApplyYMaxCommand.CanExecuteChanged += (_, __) => yMaxChanged++;

            // Default: IsYAxisManualEnabled = true; toggle to false should notify
            vm.IsYAxisManualEnabled = false;

            Assert.True(yMinChanged >= 1, "ApplyYMinCommand dovrebbe aver notificato CanExecuteChanged");
            Assert.True(yMaxChanged >= 1, "ApplyYMaxCommand dovrebbe aver notificato CanExecuteChanged");

            // Toggle back (false -> true) to confirm repeated notifications
            vm.IsYAxisManualEnabled = true;
            Assert.True(yMinChanged >= 2);
            Assert.True(yMaxChanged >= 2);
        }


        // ----- ApplyYMin behavior -----


        /// <summary>
        /// With a valid numeric input and AutoYAxis disabled, ApplyYMinCommand updates YAxisMin and clears errors.
        /// Expected: YAxisMin equals the parsed value, ValidationMessage is empty, and the value changes from its previous state.
        /// </summary>
        [Fact(DisplayName = "ApplyYMin: valid input updates YAxisMin and clears errors")]
        public void ApplyYMin_with_valid_input_updates_value_and_clears_error()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            var previousMin = vm.YAxisMin;

            vm.YAxisMinText = "-100";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(-100, vm.YAxisMin, 5);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.NotEqual(previousMin, vm.YAxisMin);
        }


        /// <summary>
        /// When the input is non-numeric and AutoYAxis is disabled, ApplyYMinCommand should set a validation
        /// error and roll the text back to the last valid numeric value while keeping the numeric property unchanged.
        /// Expected: ValidationMessage contains "valid number"; YAxisMinText reverts to the previous numeric; YAxisMin unchanged.
        /// </summary>
        [Fact(DisplayName = "ApplyYMin: non-numeric input -> error message and text rollback")]
        public void ApplyYMin_with_invalid_input_sets_error_and_rolls_back_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            var lastValid = vm.YAxisMin; // current numeric (last-valid) snapshot

            vm.YAxisMinText = "abc";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Contains("valid number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);

            // text rolled back to last valid numeric
            Assert.Equal(lastValid.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);

            // numeric value unchanged
            Assert.Equal(lastValid, vm.YAxisMin, 5);
        }


        /// <summary>
        /// With AutoYAxis enabled, ApplyYMinCommand should be effectively a no-op.
        /// Expected: YAxisMin remains unchanged.
        /// </summary>
        [Fact(DisplayName = "ApplyYMin: AutoYAxis enabled -> command does not change value")]
        public void ApplyYMin_does_nothing_when_AutoYAxis_is_true()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = true 
            };

            vm.SelectedParameter = "Gyroscope";
            var before = vm.YAxisMin;

            vm.YAxisMinText = "-123";
            vm.ApplyYMinCommand.Execute(null);

            Assert.Equal(before, vm.YAxisMin, 5);
        }


        // ----- ApplyYMax behavior -----


        /// <summary>
        /// With a valid numeric input and AutoYAxis disabled, ApplyYMaxCommand updates YAxisMax and clears errors.
        /// Expected: YAxisMax equals the parsed value; ValidationMessage is empty.
        /// </summary>
        [Fact(DisplayName = "ApplyYMax: valid input updates YAxisMax and clears errors")]
        public void ApplyYMax_with_valid_input_updates_value_and_clears_error()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Low-Noise Accelerometer";
            vm.YAxisMaxText = "10";

            vm.ApplyYMaxCommand.Execute(null);

            Assert.Equal(10, vm.YAxisMax, 5);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
        }


        /// <summary>
        /// If the proposed max is less than or equal to the current min, validation should fail and text should roll back.
        /// Expected: ValidationMessage mentions "less than or equal"; YAxisMaxText reverts to last valid; YAxisMax numeric unchanged.
        /// </summary>
        [Fact(DisplayName = "ApplyYMax: <= YMin -> error and text rollback")]
        public void ApplyYMax_blocks_when_less_or_equal_than_min()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), AllOnCfg())
            {
                AutoYAxis = false
            };

            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMin = 5;               // current min
            var prevMax = vm.YAxisMax;     // snapshot last valid max

            vm.YAxisMaxText = "4";
            vm.ApplyYMaxCommand.Execute(null);

            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);

            // text rolled back to last valid
            Assert.Equal(prevMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);

            // numeric unchanged
            Assert.Equal(prevMax, vm.YAxisMax, 5);
        }


        /// <summary>
        /// With AutoYAxis enabled, ApplyYMaxCommand should not change the numeric property.
        /// Expected: YAxisMax remains unchanged.
        /// </summary>
        [Fact(DisplayName = "ApplyYMax: AutoYAxis enabled -> command does not change value")]
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


        // ----- SyncImuFlagsFromExgDeviceIfChanged behavior -----


        /// <summary>
        /// Helper: invokes a private instance method by name, optionally with arguments.
        /// </summary>
        /// <param name="target">Target instance that declares the method.</param>
        /// <param name="methodName">Non-public method name.</param>
        /// <param name="args">Optional arguments to pass to the method.</param>
        /// <returns>The return value of the invoked method, if any.</returns>
        static object? InvokePrivate(object target, string methodName, params object[]? args)
        {
            var m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m);
            return m!.Invoke(target, args?.Length > 0 ? args : null);
        }


        /// <summary>
        /// Helper: builds a ShimmerDevice with all IMU flags initially false, EXG on (ECG mode),
        /// to create a delta for flag synchronization from EXG.
        /// </summary>
        /// <param name="exgOn">Whether EXG should be enabled.</param>
        /// <returns>A configured <see cref="ShimmerDevice"/>.</returns>
        static ShimmerDevice CfgAllFalseImuFlags(bool exgOn = true) => new ShimmerDevice
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

            EnableExg = exgOn,
            IsExgModeECG = true
        };


        /// <summary>
        /// When shimmerExg is null, the internal IMU flags should remain unchanged after attempting sync.
        /// Expected: all internal IMU enable flags remain identical before vs after.
        /// </summary>
        [Fact(DisplayName = "SyncImuFlagsFromExg: shimmerExg = null -> no changes to IMU flags")]
        public void SyncImuFlags_exg_null_keeps_flags_unchanged()
        {
            var exg = new ShimmerSDK_EXG();
            var cfg = new ShimmerDevice
            {

                // keep all IMU flags false for simplicity
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

            // pre-snapshot
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

            // force shimmerExg = null
            SetPrivate(vm, "shimmerExg", null);

            // invoke private method (void helper in this test)
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // post-snapshot: must match
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


        /// <summary>
        /// When EXG reports differences for some IMU-related enable flags, the VM should update its internal flags accordingly.
        /// Expected: the corresponding internal flags become true after sync.
        /// </summary>
        [Fact(DisplayName = "SyncImuFlagsFromExg: differences on some flags -> VM updated")]
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

            // confirm starting values are false
            Assert.False(GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"));
            Assert.False(GetPrivate<bool>(vm, "enableGyroscope"));
            Assert.False(GetPrivate<bool>(vm, "enableBattery"));
            Assert.False(GetPrivate<bool>(vm, "enableExtA15"));

            // simulate EXG reporting some flags as enabled
            SetProp(exg, "EnableLowNoiseAccelerometer", true);
            SetProp(exg, "EnableGyroscope", true);
            SetProp(exg, "EnableBatteryVoltage", true);
            SetProp(exg, "EnableExtA15", true);

            // invoke sync
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // verify VM fields updated to true
            Assert.True(GetPrivate<bool>(vm, "enableLowNoiseAccelerometer"));
            Assert.True(GetPrivate<bool>(vm, "enableGyroscope"));
            Assert.True(GetPrivate<bool>(vm, "enableBattery"));
            Assert.True(GetPrivate<bool>(vm, "enableExtA15"));
        }


        /// <summary>
        /// If EXG flags already match the VM's internal flags, no changes should occur.
        /// Expected: before/after snapshots are identical.
        /// </summary>
        [Fact(DisplayName = "SyncImuFlagsFromExg: already aligned → no modifications")]
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

            // Align EXG flags to false as well (no differences)
            SetProp(exg, "EnableLowNoiseAccelerometer", false);
            SetProp(exg, "EnableWideRangeAccelerometer", false);
            SetProp(exg, "EnableGyroscope", false);
            SetProp(exg, "EnableMagnetometer", false);
            SetProp(exg, "EnablePressureTemperature", false);
            SetProp(exg, "EnableBatteryVoltage", false);
            SetProp(exg, "EnableExtA6", false);
            SetProp(exg, "EnableExtA7", false);
            SetProp(exg, "EnableExtA15", false);

            // pre snapshot
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

            // invoke
            InvokePrivate(vm, "SyncImuFlagsFromExgDeviceIfChanged");

            // post snapshot: identical
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


        // ----- OnSampleReceived behavior -----


        // pseudo-fields with real properties (reflection-friendly)
        class SampleField { public float Data { get; set; } public SampleField(float v) { Data = v; } }

        // IMU sample with public properties
        class ImuSample
        {
            public SampleField LowNoiseAccelerometerX { get; set; } = new SampleField(0);
            public SampleField LowNoiseAccelerometerY { get; set; } = new SampleField(0);
            public SampleField LowNoiseAccelerometerZ { get; set; } = new SampleField(0);
        }

        // EXG sample with public properties
        class ExgSample
        {
            public SampleField Exg1 { get; set; } = new SampleField(0);
            public SampleField Exg2 { get; set; } = new SampleField(0);
        }


        /// <summary>
        /// Helper: IMU-only configuration with Low-Noise Accelerometer enabled and all others off.
        /// </summary>
        /// <returns>A configured <see cref="ShimmerDevice"/>.</returns>
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


        /// <summary>
        /// Helper: EXG enabled (ECG mode), IMU flags off.
        /// </summary>
        /// <returns>A configured <see cref="ShimmerDevice"/>.</returns>
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


        /// <summary>
        /// IMU path: receiving a sample should increment the internal counter and request a chart redraw.
        /// Expected: sampleCounter increases by one; ChartUpdateRequested is raised at least once.
        /// </summary>
        [Fact(DisplayName = "OnSampleReceived (IMU): increments sampleCounter and requests redraw")]
        public void OnSampleReceived_IMU_increments_counter_and_requests_redraw()
        {
            var imu = new ShimmerSDK_IMU();

            // Avoid divide-by-zero and keep labels consistent
            SetProp(imu, "SamplingRate", 50.0);
            var vm = new DataPageViewModel(imu, CfgIMU_AllOn());

            // If there is at least one available parameter, select the first
            if (vm.AvailableParameters.Count > 0)
                vm.SelectedParameter = vm.AvailableParameters[0];

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            var sample = new ImuSample
            {
                LowNoiseAccelerometerX = new SampleField(1.23f),
                LowNoiseAccelerometerY = new SampleField(-0.5f),
                LowNoiseAccelerometerZ = new SampleField(9.81f)
            };

            InvokePrivate(vm, "OnSampleReceived", new object(), sample);

            var newCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(prevCount + 1, newCount);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// EXG path: when device flags change between samples, the VM should increment the counter and request redraw.
        /// Expected: sampleCounter increases; ChartUpdateRequested raised at least once. No assumptions on SelectedParameter.
        /// </summary>
        [Fact(DisplayName = "OnSampleReceived (EXG): flag changes -> increments counter and requests redraw")]
        public void OnSampleReceived_EXG_flag_change_increments_and_redraws()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp(exg, "SamplingRate", 51.2);
            var vm = new DataPageViewModel(exg, CfgEXG_On_IMU_AllOff());

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // First sample
            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(2f), Exg2 = new(-1f) });

            // Desynchronize EXG-reported flags relative to VM internal state
            SetProp(exg, "EnableLowNoiseAccelerometer", true);
            SetProp(exg, "EnableGyroscope", true);
            SetProp(exg, "EnableBatteryVoltage", true);

            // Set an invalid selection (don't assert fix-up: platform-specific)
            vm.SelectedParameter = "__invalid__";

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            // Second sample: should trigger sync path if supported by the build
            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(3f), Exg2 = new(0f) });

            var curCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.True(curCount > prevCount);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// EXG path: when flags do not change, only the counter should increment, without requiring redraw assertions on series.
        /// Expected: sampleCounter increases by one; no assumptions on series are made.
        /// </summary>
        [Fact(DisplayName = "OnSampleReceived (EXG): no flag differences -> increments counter only")]
        public void OnSampleReceived_EXG_no_flag_change_increments_only()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp(exg, "SamplingRate", 51.2);
            var vm = new DataPageViewModel(exg, CfgEXG_On_IMU_AllOff());

            var prevCount = GetPrivate<int>(vm, "sampleCounter");

            InvokePrivate(vm, "OnSampleReceived", new object(), new ExgSample { Exg1 = new(0.1f), Exg2 = new(0.2f) });

            var curCount = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(prevCount + 1, curCount);
        }


        // ----- AttachToDevice behavior -----


        /// <summary>
        /// Helper: invokes a private method with an exact parameter count, useful for overloads.
        /// </summary>
        /// <param name="target">Instance that declares the non-public method.</param>
        /// <param name="method">Method name.</param>
        /// <param name="args">Arguments to match the desired overload.</param>
        /// <returns>Nothing.</returns>
        static void InvokePrivateParams(object target, string method, params object?[] args)
        {
            var type = target.GetType();
            var m = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                        .FirstOrDefault(mi => mi.Name == method &&
                                              mi.GetParameters().Length == (args?.Length ?? 0));
            Assert.NotNull(m);
            m!.Invoke(target, args);
        }


        /// <summary>
        /// AttachToDevice should be null-safe when shimmerImu/exg are null and must not throw.
        /// Expected: calling AttachToDevice completes without exception.
        /// </summary>
        [Fact(DisplayName = "AttachToDevice: with shimmerImu/exg = null does not throw")]
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


        /// <summary>
        /// AttachToDevice should be idempotent; multiple invocations must not throw.
        /// Expected: both calls complete without exceptions.
        /// </summary>
        [Fact(DisplayName = "AttachToDevice: repeated invocations do not throw")]
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


        // ----- DetachFromDevice behavior -----


        /// <summary>
        /// DetachFromDevice should be null-safe when shimmerImu/exg are null.
        /// Expected: calling DetachFromDevice completes without exception.
        /// </summary>
        [Fact(DisplayName = "DetachFromDevice: with shimmerImu/exg = null does not throw")]
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


        /// <summary>
        /// DetachFromDevice should be idempotent; repeated calls must not throw.
        /// Expected: both invocations complete without exceptions.
        /// </summary>
        [Fact(DisplayName = "DetachFromDevice: repeated call does not throw")]
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


        // ----- OnIsApplyingSamplingRateChanged behavior -----


        /// <summary>
        /// When IsApplyingSamplingRate is set to true, ApplySamplingRateCommand should be disabled.
        /// Expected: ApplySamplingRateCommand.CanExecute returns false.
        /// </summary>
        [Fact(DisplayName = "OnIsApplyingSamplingRateChanged: IsApplying=true -> ApplySamplingRateCommand disabled")]
        public void OnIsApplyingSamplingRateChanged_disables_when_true()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            // set backing field to true and invoke the partial
            SetPrivate(vm, "isApplyingSamplingRate", true);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", true);

            Assert.False(vm.ApplySamplingRateCommand.CanExecute(null));
        }


        /// <summary>
        /// When IsApplyingSamplingRate changes back to false, ApplySamplingRateCommand should be enabled again.
        /// Expected: ApplySamplingRateCommand.CanExecute returns true after transition to false.
        /// </summary>
        [Fact(DisplayName = "OnIsApplyingSamplingRateChanged: IsApplying=false -> ApplySamplingRateCommand enabled")]
        public void OnIsApplyingSamplingRateChanged_enables_when_false()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "isApplyingSamplingRate", true);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", true);
            Assert.False(vm.ApplySamplingRateCommand.CanExecute(null));

            SetPrivate(vm, "isApplyingSamplingRate", false);
            InvokePrivateParams(vm, "OnIsApplyingSamplingRateChanged", false);
            Assert.True(vm.ApplySamplingRateCommand.CanExecute(null));
        }


        // ----- ConnectAndStartAsync behavior -----


        /// <summary>
        /// With device references null, ConnectAndStartAsync should still raise busy events and complete cleanly.
        /// Expected: ShowBusy then HideBusy are raised; no exception is thrown.
        /// </summary>
        [Fact(DisplayName = "ConnectAndStartAsync: emits ShowBusy/HideBusy and completes (devices null)")]
        public async Task ConnectAndStartAsync_emits_busy_and_finishes_with_null_devices()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            SetPrivate(vm, "shimmerImu", null);
            SetPrivate(vm, "shimmerExg", null);

            var events = new List<string>();
            vm.ShowBusyRequested += (_, __) => events.Add("show");
            vm.HideBusyRequested += (_, __) => events.Add("hide");

            var ex = await Record.ExceptionAsync(() => vm.ConnectAndStartAsync());
            Assert.Null(ex);

            Assert.Contains("show", events);
            Assert.Contains("hide", events);
            Assert.True(events.IndexOf("show") < events.IndexOf("hide"));
        }


        /// <summary>
        /// With device references null, ConnectAndStartAsync should not show user alerts (no bubbled errors).
        /// Expected: ShowAlertRequested is not raised.
        /// </summary>
        [Fact(DisplayName = "ConnectAndStartAsync: no alert when devices are null")]
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


        /// <summary>
        /// Verifies that <c>ConnectAndStartAsync</c> emits busy notifications and completes successfully.
        /// Expected: no exception is thrown; both <c>ShowBusyRequested</c> and <c>HideBusyRequested</c>
        /// are raised at least once.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact(DisplayName = "ConnectAndStartAsync: emits busy events and completes")]
        public async Task ConnectAndStartAsync_emits_busy_and_finishes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));
            int show = 0, hide = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;

            var ex = await Record.ExceptionAsync(() => vm.ConnectAndStartAsync());
            Assert.Null(ex);
            Assert.True(show >= 1);
            Assert.True(hide >= 1);
        }


        // ----- StopAsync behavior -----


        /// <summary>
        /// StopAsync with disconnect=false must complete without exceptions when devices are null.
        /// Expected: no exception thrown.
        /// </summary>
        [Fact(DisplayName = "StopAsync(false): completes without exceptions (devices null)")]
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


        /// <summary>
        /// StopAsync with disconnect=true must complete without exceptions when devices are null.
        /// Expected: no exception thrown.
        /// </summary>
        [Fact(DisplayName = "StopAsync(true): completes without exceptions (devices null)")]
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


        /// <summary>
        /// StopAsync should be idempotent; repeated calls must not throw.
        /// Expected: both calls (disconnect=false, then true) complete without exceptions.
        /// </summary>
        [Fact(DisplayName = "StopAsync: repeated calls do not throw")]
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


        /// <summary>
        /// Verifies that <c>StopAsync</c> completes without throwing when called with <c>disconnect = false</c>.
        /// Expected: no exception is thrown.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact(DisplayName = "StopAsync: completes without exceptions")]
        public async Task StopAsync_no_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK_IMU(), Cfg(exg: false));

            var ex = await Record.ExceptionAsync(() => vm.StopAsync(disconnect: false));
            Assert.Null(ex);
        }


        /// <summary>
        /// Device helpers should not throw in normal flow.
        /// Expected: Attach, Detach, and StopAsync complete without exceptions.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact(DisplayName = "Device helpers: Attach/Detach/StopAsync do not throw")]
        public async Task Device_helpers_do_not_throw()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.AttachToDevice();
            vm.DetachFromDevice();
            await vm.StopAsync(disconnect: false);

            Assert.True(true);
        }


        // ----- ApplySamplingRateAsync behavior -----


        /// <summary>
        /// Helper: invoke a private async method by name and await its completion.
        /// </summary>
        /// <param name="target">The instance that declares the method.</param>
        /// <param name="method">The non-public async method name.</param>
        /// <param name="args">Optional arguments to pass to the method.</param>
        /// <returns>A task representing the invocation.</returns>
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


        /// <summary>
        /// Helper: retrieves MIN/MAX sampling limits from private static fields.
        /// </summary>
        /// <param name="vmType">The view-model type that contains the private fields.</param>
        /// <returns>A tuple with (min, max) sampling rate limits.</returns>
        static (double min, double max) GetSamplingLimits(Type vmType)
        {
            var fMin = vmType.GetField("MIN_SAMPLING_RATE", BindingFlags.NonPublic | BindingFlags.Static);
            var fMax = vmType.GetField("MAX_SAMPLING_RATE", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(fMin); Assert.NotNull(fMax);
            return ((double)fMin!.GetValue(null)!, (double)fMax!.GetValue(null)!);
        }


        /// <summary>
        /// Non-numeric SamplingRateText should trigger validation and not raise busy/alert events.
        /// Expected: ValidationMessage is set; no ShowBusy/HideBusy/ShowAlert events; isApplyingSamplingRate=false.
        /// </summary>
        [Fact(DisplayName = "ApplySamplingRateAsync: non-numeric input -> validation + no busy/alert")]
        public async Task ApplySamplingRateAsync_invalid_text_shows_validation_and_no_busy()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerDevice();
            var vm = new DataPageViewModel(imu, cfg);

            int show = 0, hide = 0, alerts = 0;
            vm.ShowBusyRequested += (_, __) => show++;
            vm.HideBusyRequested += (_, __) => hide++;
            vm.ShowAlertRequested += (_, __) => alerts++;

            vm.SamplingRateText = "abc";

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.NotEmpty(vm.ValidationMessage);
            Assert.Equal(0, show);
            Assert.Equal(0, hide);
            Assert.Equal(0, alerts);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }


        /// <summary>
        /// SamplingRateText above MAX should trigger validation and not raise busy/alert events.
        /// Expected: ValidationMessage mentions "too high"; no ShowBusy/HideBusy/ShowAlert; isApplyingSamplingRate=false.
        /// </summary>
        [Fact(DisplayName = "ApplySamplingRateAsync: input > MAX -> validation + no busy/alert")]
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


        /// <summary>
        /// SamplingRateText below MIN should trigger validation and not raise busy/alert events.
        /// Expected: ValidationMessage mentions "too low"; no ShowBusy/HideBusy/ShowAlert; isApplyingSamplingRate=false.
        /// </summary>
        [Fact(DisplayName = "ApplySamplingRateAsync: input < MIN -> validation + no busy/alert")]
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


        /// <summary>
        /// Valid SamplingRateText should raise busy, apply the rate, show success alert, clear validation, and reset flag.
        /// Expected: one ShowBusy and one HideBusy; one success alert containing "Sampling rate set to";
        /// ValidationMessage empty; isApplyingSamplingRate=false.
        /// </summary>
        [Fact(DisplayName = "ApplySamplingRateAsync: valid input -> busy/success alert and IsApplying=false")]
        public async Task ApplySamplingRateAsync_valid_flow_shows_busy_success_alert_and_resets_flag()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();

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

                Assert.Contains("Sampling rate set to", msg, StringComparison.OrdinalIgnoreCase);
            };

            await InvokePrivateAsync(vm, "ApplySamplingRateAsync");

            Assert.Equal(1, show);
            Assert.Equal(1, hide);
            Assert.Equal(1, alerts);
            Assert.True(show <= hide);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.False(GetPrivate<bool>(vm, "isApplyingSamplingRate"));
        }


        // ----- UpdateSamplingRateAndRestart behavior -----


        /// <summary>
        /// Helper: set a private instance field by name.
        /// </summary>
        /// <param name="target">The instance to modify.</param>
        /// <param name="field">Private field name.</param>
        /// <param name="value">Value to set.</param>
        static void SetPrivateField(object target, string field, object? value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(target, value);
        }


        /// <summary>
        /// Helper: IMU config with only Low-Noise Accelerometer enabled (enough to create series/parameters).
        /// </summary>
        /// <returns>A configured <see cref="ShimmerDevice"/>.</returns>
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


        /// <summary>
        /// Without attached devices, UpdateSamplingRateAndRestart should apply the requested rate,
        /// update UI mirrors, clear all series, reset counters, and request a chart update.
        /// Expected: SamplingRateDisplay/Text reflect requested; series emptied; sampleCounter=0; ValidationMessage empty; redraw requested.
        /// </summary>
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: no devices -> applies rate, clears series, resets counters")]
        public void UpdateSamplingRateAndRestart_no_devices_updates_ui_clears_and_resets()
        {

            // IMU VM to populate a series prior to restart
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
          
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());

            Assert.Contains("Low-Noise Accelerometer", vm.AvailableParameters);
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Push one sample so we can assert clearing later
            var sampleType = new
            {
                LowNoiseAccelerometerX = new { Data = 1.0f },
                LowNoiseAccelerometerY = new { Data = 2.0f },
                LowNoiseAccelerometerZ = new { Data = 3.0f }
            };
            InvokePrivate(vm, "OnSampleReceived", new object(), sampleType);

            // Before restart there is 1 point
            var before = vm.GetSeriesSnapshot("Low-Noise AccelerometerX");
            Assert.Single(before.data);
            Assert.Single(before.time);

            // Force "no device" branch
            SetPrivateField(vm, "shimmerImu", null);
            SetPrivateField(vm, "shimmerExg", null);


            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Invoke with a new rate
            double requested = 100;
            InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested);

            Assert.Equal(requested, vm.SamplingRateDisplay, 5);
            Assert.Equal(requested.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);

            // Counters reset
            var sampleCounter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(0, sampleCounter);

            // Series cleared
            var after = vm.GetSeriesSnapshot("Low-Noise AccelerometerX");
            Assert.Empty(after.data);
            Assert.Empty(after.time);

            // Clean validation, redraw requested at least once
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// With an IMU attached, UpdateSamplingRateAndRestart should update UI coherently (even if SDK snaps),
        /// reset counters, and request a chart update without throwing.
        /// Expected: SamplingRateDisplay/Text updated and non-empty; sampleCounter reset; redraw requested; no validation errors.
        /// </summary>
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: with IMU -> updates UI and resets counter")]
        public void UpdateSamplingRateAndRestart_with_imu_updates_ui_and_resets_counter()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Bring counter > 0 to verify reset
            InvokePrivate(vm, "OnSampleReceived", new object(), new
            {
                LowNoiseAccelerometerX = new { Data = 1.0f },
                LowNoiseAccelerometerY = new { Data = 2.0f },
                LowNoiseAccelerometerZ = new { Data = 3.0f }
            });
            Assert.True(GetPrivate<int>(vm, "sampleCounter") > 0);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            double requested = 40.0;
            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested));
            Assert.Null(ex);

            Assert.True(vm.SamplingRateDisplay > 0);
            Assert.False(string.IsNullOrEmpty(vm.SamplingRateText));

            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
            Assert.True(redraws >= 1);

            Assert.Equal(string.Empty, vm.ValidationMessage);
        }


        /// <summary>
        /// UpdateSamplingRateAndRestart should swallow internal exceptions from nested calls and keep the VM consistent,
        /// especially on null-device paths.
        /// Expected: no exception; SamplingRate mirrors reflect requested; sampleCounter reset; ValidationMessage empty.
        /// </summary>
        [Fact(DisplayName = "UpdateSamplingRateAndRestart: swallows internal errors and keeps VM consistent")]
        public void UpdateSamplingRateAndRestart_swallow_internal_errors_keeps_vm_consistent()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 25.0);
            var vm = new DataPageViewModel(imu, CfgIMU_LnaOnly());
            vm.SelectedParameter = "Low-Noise Accelerometer";

            // Ensure safest path by nulling devices
            SetPrivateField(vm, "shimmerImu", null);
            SetPrivateField(vm, "shimmerExg", null);

            double requested = 12.345;
            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateSamplingRateAndRestart", requested));
            Assert.Null(ex);

            Assert.Equal(requested, vm.SamplingRateDisplay, 5);
            Assert.Equal(requested.ToString(CultureInfo.InvariantCulture), vm.SamplingRateText);
            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
            Assert.Equal(string.Empty, vm.ValidationMessage);
        }


        // ----- InitializeDataCollections behavior -----


        /// <summary>
        /// InitializeDataCollections should create empty series/time keys for LNA (X,Y,Z) and enabled ExtADC channels.
        /// Expected: keys exist in both dictionaries and are empty.
        /// </summary>
        [Fact(DisplayName = "InitializeDataCollections: LNA + ExtADC -> creates expected empty series")]
        public void InitializeDataCollections_creates_LNA_and_ExtADC_keys()
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
                EnableExtA7 = true,
                EnableExtA15 = true,
                EnableExg = false
            };

            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var vm = new DataPageViewModel(imu, cfg);

            // clear maps to call InitializeDataCollections explicitly
            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            InvokePrivate(vm, "InitializeDataCollections");

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");


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


        /// <summary>
        /// When EXG is enabled (ECG mode), InitializeDataCollections should add Exg1/Exg2 keys.
        /// Expected: both Exg1 and Exg2 exist and are empty in data/time dictionaries.
        /// </summary>
        [Fact(DisplayName = "InitializeDataCollections: EXG enabled -> creates Exg1/Exg2")]
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

            // Clear and invoke explicitly
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


        /// <summary>
        /// With all flags disabled, InitializeDataCollections should not create any keys.
        /// Expected: both collections remain empty.
        /// </summary>
        [Fact(DisplayName = "InitializeDataCollections: no sensors enabled -> no series created")]
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


        /// <summary>
        /// InitializeDataCollections should be idempotent; calling it twice must not duplicate keys.
        /// Expected: key sets are identical across invocations.
        /// </summary>
        [Fact(DisplayName = "InitializeDataCollections: called twice -> no duplicates")]
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

            var exg = new ShimmerSDK.EXG.ShimmerSDK_EXG();
            var vm = new DataPageViewModel(exg, cfg);

            SetPrivate(vm, "dataPointsCollections", new Dictionary<string, List<float>>());
            SetPrivate(vm, "timeStampsCollections", new Dictionary<string, List<int>>());

            InvokePrivate(vm, "InitializeDataCollections");
            var data1 = new HashSet<string>(GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections").Keys);

            // Second call
            InvokePrivate(vm, "InitializeDataCollections");
            var data2 = new HashSet<string>(GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections").Keys);

            // No duplicated or changed keys
            Assert.Equal(data1, data2);
        }


        // ----- MarkFirstOpenBaseline behavior -----


        /// <summary>
        /// MarkFirstOpenBaseline(true) should reset the baseline, clear all buffers, reset counters,
        /// and request a chart update to reflect a fresh timeline.
        /// Expected: internal data/time collections cleared; sampleCounter reset to 0; redraw requested at least once.
        /// </summary>
        [Fact(DisplayName = "MarkFirstOpenBaseline(true): clears buffers and resets counters")]
        public void MarkFirstOpenBaseline_clearBuffers_clears_and_resets()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };

            var vm = new DataPageViewModel(imu, cfg);

            // Seed some internal state (data and counters) to be cleared
            SetPrivate<int>(vm, "sampleCounter", 42);
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Ensure required keys exist
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

            var baseline = GetPrivate<double>(vm, "timeBaselineSeconds");
            Assert.Equal(0.0, baseline, 6);

            Assert.Empty(data["Low-Noise AccelerometerX"]);
            Assert.Empty(time["Low-Noise AccelerometerX"]);

            var counter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(0, counter);

            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// When clearBuffers is false, MarkFirstOpenBaseline should compute the baseline as sampleCounter / DeviceSamplingRate
        /// and keep existing data intact.
        /// Expected: timeBaselineSeconds == 2.0 for sampleCounter=100 and SR=50 Hz; data and counter unchanged; a redraw is requested.
        /// </summary>
        [Fact(DisplayName = "MarkFirstOpenBaseline(false): baseline = sampleCounter/DeviceSamplingRate, keeps data intact")]
        public void MarkFirstOpenBaseline_keepBuffers_sets_baseline_and_keeps_data()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            SetProp<double>(imu, "SamplingRate", 50.0);

            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };

            var vm = new DataPageViewModel(imu, cfg);

            // sampleCounter > 0 to get a meaningful baseline
            SetPrivate<int>(vm, "sampleCounter", 100);  // 100/50 = 2.0 s expected

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

            var baseline = GetPrivate<double>(vm, "timeBaselineSeconds");
            Assert.Equal(2.0, baseline, 6); // 100 / 50

            // Data intact
            Assert.Equal(dataCountBefore, data["Low-Noise AccelerometerX"].Count);
            Assert.Equal(timeCountBefore, time["Low-Noise AccelerometerX"].Count);

            // Counter intact
            var counter = GetPrivate<int>(vm, "sampleCounter");
            Assert.Equal(100, counter);

            Assert.True(redraws >= 1);
        }


        // ----- ClearAllDataCollections behavior -----


        /// <summary>
        /// ClearAllDataCollections should empty all data/time lists while preserving existing keys.
        /// Expected: same key sets after the call; each list is empty.
        /// </summary>
        [Fact(DisplayName = "ClearAllDataCollections: empties data/timestamps but keeps keys")]
        public void ClearAllDataCollections_clears_lists_keeps_keys()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true,
                EnableExtA6 = true
            };
            var vm = new DataPageViewModel(imu, cfg);

            // Access private dictionaries
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Ensure at least one key with values
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

            // Assert: keys remain
            Assert.Equal(keysBeforeData, data.Keys.ToList());
            Assert.Equal(keysBeforeTime, time.Keys.ToList());

            // All lists are empty
            foreach (var k in keysBeforeData)
                Assert.Empty(data[k]);
            foreach (var k in keysBeforeTime)
                Assert.Empty(time[k]);
        }


        /// <summary>
        /// ClearAllDataCollections should be idempotent; invoking it again keeps keys and lists empty with no exceptions.
        /// Expected: no exception; identical key sets; lists stay empty.
        /// </summary>
        [Fact(DisplayName = "ClearAllDataCollections: idempotent (second call changes nothing)")]
        public void ClearAllDataCollections_is_idempotent()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice
            {
                EnableLowNoiseAccelerometer = true
            };
            var vm = new DataPageViewModel(imu, cfg);

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // First clear
            InvokePrivate(vm, "ClearAllDataCollections");

            var keysAfterFirstData = data.Keys.ToList();
            var keysAfterFirstTime = time.Keys.ToList();

            // Verify already empty
            foreach (var k in keysAfterFirstData) Assert.Empty(data[k]);
            foreach (var k in keysAfterFirstTime) Assert.Empty(time[k]);

            // Second clear
            var ex = Record.Exception(() => InvokePrivate(vm, "ClearAllDataCollections"));

            Assert.Null(ex);
            Assert.Equal(keysAfterFirstData, data.Keys.ToList());
            Assert.Equal(keysAfterFirstTime, time.Keys.ToList());
            foreach (var k in keysAfterFirstData) Assert.Empty(data[k]);
            foreach (var k in keysAfterFirstTime) Assert.Empty(time[k]);
        }


        // ----- TrimCollection behavior -----


        /// <summary>
        /// TrimCollection should retain only the last <paramref name="maxPoints"/> elements for the given key
        /// and keep data/time arrays aligned.
        /// Expected: exactly maxPoints items remain and match the last original elements (indices 5..9 in this setup).
        /// </summary>
        /// <param name="key">Series key to trim.</param>
        /// <param name="maxPoints">Maximum number of points to keep.</param>
        [Fact(DisplayName = "TrimCollection: trims to maxPoints and preserves data↔time alignment")]
        public void TrimCollection_trims_to_max_and_keeps_alignment()
        {
            var imu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var cfg = new ShimmerInterface.Models.ShimmerDevice { EnableLowNoiseAccelerometer = true };
            var vm = new DataPageViewModel(imu, cfg);

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "Low-Noise AccelerometerX";

            // Ensure the key exists
            if (!data.ContainsKey(key)) data[key] = new List<float>();
            if (!time.ContainsKey(key)) time[key] = new List<int>();

            // 10 points, we want to trim to 5
            data[key].Clear(); time[key].Clear();
            for (int i = 0; i < 10; i++) { data[key].Add(i + 0.5f); time[key].Add(i * 10); }

            // Act
            InvokePrivate(vm, "TrimCollection", key, 5);

            // 5 points left (original indices 5..9)
            Assert.Equal(5, data[key].Count);
            Assert.Equal(5, time[key].Count);
            Assert.Equal(5 + 0.5f, data[key][0], 3);
            Assert.Equal(5 * 10, time[key][0]);
            Assert.Equal(9 + 0.5f, data[key][4], 3);
            Assert.Equal(9 * 10, time[key][4]);
        }


        /// <summary>
        /// When the series length is already ≤ maxPoints, TrimCollection should do nothing.
        /// Expected: series remain unchanged.
        /// </summary>
        [Fact(DisplayName = "TrimCollection: no-op when length <= maxPoints")]
        public void TrimCollection_noop_when_length_le_max()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "GyroscopeX";
            data[key] = new List<float> { 1f, 2f, 3f };
            time[key] = new List<int> { 10, 20, 30 };

            // Copies for comparison
            var beforeData = data[key].ToList();
            var beforeTime = time[key].ToList();

            // Act
            InvokePrivate(vm, "TrimCollection", key, 5);

            // Unchanged
            Assert.Equal(beforeData, data[key]);
            Assert.Equal(beforeTime, time[key]);
        }


        /// <summary>
        /// Missing key should be a safe no-op.
        /// Expected: no exception; global key sets unchanged.
        /// </summary>
        [Fact(DisplayName = "TrimCollection: missing parameter -> safe no-op")]
        public void TrimCollection_missing_key_is_safe_noop()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            var dataKeysBefore = data.Keys.ToList();
            var timeKeysBefore = time.Keys.ToList();

            var ex = Record.Exception(() => InvokePrivate(vm, "TrimCollection", "__missing__", 3));

            Assert.Null(ex);
            Assert.Equal(dataKeysBefore, data.Keys.ToList());
            Assert.Equal(timeKeysBefore, time.Keys.ToList());
        }


        /// <summary>
        /// If timestamps are shorter than data, TrimCollection should stop when timestamps are exhausted
        /// and only remove as many data points as timestamps removed, preserving consistency.
        /// Expected: time becomes empty; data keeps the last (dataCount - removed) elements.
        /// </summary>
        [Fact(DisplayName = "TrimCollection: shorter time than data -> stops when timestamps end")]
        public void TrimCollection_stops_when_time_exhausted_if_mismatched_lengths()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var time = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "MagnetometerX";
            data[key] = new List<float> { 0f, 1f, 2f, 3f, 4f, 5f }; // 6 elements
            time[key] = new List<int> { 10, 20, 30 };               // 3 elements

            InvokePrivate(vm, "TrimCollection", key, 2);

            Assert.Empty(time[key]);
            Assert.Equal(3, data[key].Count); // Remaining: 3,4,5
            Assert.Equal(new List<float> { 3f, 4f, 5f }, data[key]);
        }


        // ----- GetSeriesSnapshot behavior -----


        /// <summary>
        /// Requesting a non-existing series should return empty lists instead of nulls.
        /// Expected: non-null empty data and time lists.
        /// </summary>
        [Fact(DisplayName = "GetSeriesSnapshot: missing parameter -> returns empty lists")]
        public void GetSeriesSnapshot_missing_key_returns_empty_lists()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            var (data, time) = vm.GetSeriesSnapshot("__missing__");

            Assert.NotNull(data);
            Assert.NotNull(time);
            Assert.Empty(data);
            Assert.Empty(time);
        }


        /// <summary>
        /// GetSeriesSnapshot must return deep copies; modifying the snapshot should not affect originals.
        /// Expected: originals unchanged after editing snapshot.
        /// </summary>
        [Fact(DisplayName = "GetSeriesSnapshot: deep copy -> snapshot changes do not affect original")]
        public void GetSeriesSnapshot_returns_deep_copy_snapshot_is_independent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "GyroscopeX";
            dataDict[key] = new List<float> { 1f, 2f, 3f };
            timeDict[key] = new List<int> { 10, 20, 30 };

            var (snapData, snapTime) = vm.GetSeriesSnapshot(key);

            // Modify snapshot
            snapData.Add(99f);
            snapTime.Add(999);

            // Originals untouched
            Assert.Equal(new List<float> { 1f, 2f, 3f }, dataDict[key]);
            Assert.Equal(new List<int> { 10, 20, 30 }, timeDict[key]);
        }


        /// <summary>
        /// Snapshot should remain stable even if originals change after it was taken.
        /// Expected: snapshot content does not change after original lists are appended.
        /// </summary>
        [Fact(DisplayName = "GetSeriesSnapshot: deep copy -> original changes do not affect snapshot")]
        public void GetSeriesSnapshot_snapshot_not_affected_by_later_original_changes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());
            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            const string key = "Low-Noise AccelerometerX";
            dataDict[key] = new List<float> { 0f, 1f };
            timeDict[key] = new List<int> { 5, 15 };

            var (snapData, snapTime) = vm.GetSeriesSnapshot(key);

            // Change originals after snapshot
            dataDict[key].Add(2f);
            timeDict[key].Add(25);

            // Snapshot remains the original content
            Assert.Equal(new List<float> { 0f, 1f }, snapData);
            Assert.Equal(new List<int> { 5, 15 }, snapTime);
        }


        /// <summary>
        /// GetSeriesSnapshot should map external EXG labels (e.g., "EXG1") to internal keys (e.g., "Exg1").
        /// Expected: correct mapping and data returned.
        /// </summary>
        [Fact(DisplayName = "GetSeriesSnapshot: uses MapToInternalKey (EXG1 -> Exg1)")]
        public void GetSeriesSnapshot_maps_exg_channel_names()
        {
            var cfg = new ShimmerInterface.Models.ShimmerDevice { EnableExg = true, IsExgModeECG = true };
            var vm = new DataPageViewModel(new ShimmerSDK.EXG.ShimmerSDK_EXG(), cfg);

            var dataDict = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            var timeDict = GetPrivate<Dictionary<string, List<int>>>(vm, "timeStampsCollections");

            // Populate expected internal key: "Exg1"
            dataDict["Exg1"] = new List<float> { 0.1f, 0.2f, 0.3f };
            timeDict["Exg1"] = new List<int> { 100, 200, 300 };

            // Pass "EXG1" (uppercase) to verify mapping
            var (data, time) = vm.GetSeriesSnapshot("EXG1");

            Assert.Equal(new List<float> { 0.1f, 0.2f, 0.3f }, data);
            Assert.Equal(new List<int> { 100, 200, 300 }, time);
        }


        /// <summary>
        /// Simple IMU case: standard key should return a deep-copied snapshot.
        /// Expected: snapshot equals current data but is independent from originals.
        /// </summary>
        [Fact(DisplayName = "GetSeriesSnapshot: simple IMU case (GyroscopeX)")]
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

            // Verify deep copy
            data.Add(6f);
            time.Add(60);
            Assert.Equal(new List<float> { 4f, 5f }, dataDict[key]);
            Assert.Equal(new List<int> { 40, 50 }, timeDict[key]);
        }


        // ----- UpdateChart behavior -----


        /// <summary>
        /// UpdateChart should raise ChartUpdateRequested exactly once with the VM as sender and EventArgs.Empty as args.
        /// Expected: one invocation received; sender==vm; args==EventArgs.Empty.
        /// </summary>
        [Fact(DisplayName = "UpdateChart: raises ChartUpdateRequested once with correct sender and args")]
        public void UpdateChart_raises_event_once_with_correct_sender_and_args()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            int calls = 0;
            object? lastSender = null;
            EventArgs? lastArgs = null;

            EventHandler handler = (s, e) => { calls++; lastSender = s; lastArgs = e; };
            vm.ChartUpdateRequested += handler;

            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateChart"));
            Assert.Null(ex);

            Assert.Equal(1, calls);
            Assert.Same(vm, lastSender);
            Assert.Same(EventArgs.Empty, lastArgs);
        }


        /// <summary>
        /// UpdateChart with no subscribers must not throw.
        /// Expected: no exception.
        /// </summary>
        [Fact(DisplayName = "UpdateChart: no subscribers -> does not throw")]
        public void UpdateChart_with_no_subscribers_does_not_throw()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), new ShimmerInterface.Models.ShimmerDevice());

            var ex = Record.Exception(() => InvokePrivate(vm, "UpdateChart"));
            Assert.Null(ex);
        }


        /// <summary>
        /// UpdateChart should notify all registered subscribers exactly once.
        /// Expected: every handler is called once.
        /// </summary>
        [Fact(DisplayName = "UpdateChart: multiple subscribers -> all are notified")]
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


        /// <summary>
        /// Unsubscribing a listener must prevent it from receiving UpdateChart notifications.
        /// Expected: handler is not called after being removed.
        /// </summary>
        [Fact(DisplayName = "UpdateChart: unsubscribe removes the listener")]
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


        // ----- ChartModeLabel behavior -----


        /// <summary>
        /// Helper: that creates an IMU device with a valid SamplingRate (> 0)
        /// and a configuration with all IMU-related flags enabled (EXG disabled).
        /// Intended to provide a rich feature set for tests targeting IMU charts.
        /// </summary>
        /// <returns>
        /// A tuple (imu, cfg) where:
        /// - imu: ShimmerSDK_IMU with SamplingRate=51.2
        /// - cfg: ShimmerDevice with IMU flags enabled; EXG disabled
        /// </returns>
        static (ShimmerSDK_IMU imu, ShimmerDevice cfg) ImuAllOn()
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


        /// <summary>
        /// Helper that creates an EXG device configured for ECG mode,
        /// while also enabling the IMU-related flags (harmless but convenient for test setup).
        /// </summary>
        /// <returns>
        /// A tuple (exg, cfg) where:
        /// - exg: ShimmerSDK_EXG with SamplingRate=51.2
        /// - cfg: ShimmerDevice with EXG enabled in ECG mode; other flags also enabled
        /// </returns>
        static (ShimmerSDK_EXG exg, ShimmerDevice cfg) ExgAllOnECG()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 51.2);

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
            return (exg, cfg);
        }


        /// <summary>
        /// IMU + Multi display mode should label as "Multi Parameter (X, Y, Z)".
        /// Expected: ChartModeLabel == "Multi Parameter (X, Y, Z)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: IMU + Multi -> 'Multi Parameter (X, Y, Z)'")]
        public void ChartModeLabel_IMU_Multi()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";
            vm.ChartDisplayMode = ChartDisplayMode.Multi;

            Assert.Equal("Multi Parameter (X, Y, Z)", vm.ChartModeLabel);
        }


        /// <summary>
        /// IMU split variant + Split mode should label as "Split (three separate charts)".
        /// Expected: ChartModeLabel == "Split (three separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: IMU + Split -> 'Split (three separate charts)'")]
        public void ChartModeLabel_IMU_Split()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            // IMU split “variant”
            vm.SelectedParameter = "    → Gyroscope — separate charts (X·Y·Z)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.Equal("Split (three separate charts)", vm.ChartModeLabel);
        }


        /// <summary>
        /// EXG family + Multi mode should label as "Multi Parameter (EXG1, EXG2)".
        /// Expected: ChartModeLabel == "Multi Parameter (EXG1, EXG2)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: EXG + Multi -> 'Multi Parameter (EXG1, EXG2)'")]
        public void ChartModeLabel_EXG_Multi()
        {
            var (exg, cfg) = ExgAllOnECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";
            vm.ChartDisplayMode = ChartDisplayMode.Multi;

            Assert.Equal("Multi Parameter (EXG1, EXG2)", vm.ChartModeLabel);
        }


        /// <summary>
        /// EXG split variant + Split mode should label as "Split (two separate charts)".
        /// Expected: ChartModeLabel == "Split (two separate charts)".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: EXG + Split -> 'Split (two separate charts)'")]
        public void ChartModeLabel_EXG_Split()
        {
            var (exg, cfg) = ExgAllOnECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";
            vm.ChartDisplayMode = ChartDisplayMode.Split;

            Assert.Equal("Split (two separate charts)", vm.ChartModeLabel);
        }


        /// <summary>
        /// Unknown display mode should fall back to "Unified".
        /// Expected: ChartModeLabel == "Unified".
        /// </summary>
        [Fact(DisplayName = "ChartModeLabel: unknown mode fallback → 'Unified'")]
        public void ChartModeLabel_Fallback_Unified()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            // Force an unexpected enum value to test the default branch
            vm.ChartDisplayMode = (ChartDisplayMode)999;

            Assert.Equal("Unified", vm.ChartModeLabel);
        }


        // ----- Legend (labels and colors) -----


        /// <summary>
        /// Helper: that creates an EXG device configured for ECG mode,
        /// enabling EXG (ECG) plus other flags (harmless).
        /// </summary>
        /// <returns>
        /// A tuple (exg, cfg) where:
        /// - exg: ShimmerSDK_EXG with SamplingRate=51.2
        /// - cfg: ShimmerDevice with EXG enabled in ECG mode; other flags also enabled
        /// </returns>
        static (ShimmerSDK_EXG exg, ShimmerDevice cfg) ExgECG()
        {
            var exg = new ShimmerSDK_EXG();
            SetProp<double>(exg, "SamplingRate", 51.2);
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
            return (exg, cfg);
        }


        /// <summary>
        /// IMU/Gyroscope legends: expect X,Y,Z with colors Red, Green, Blue.
        /// Expected: LegendLabels == ["X","Y","Z"]; Legend1/2/3Text as "X","Y","Z"; colors R-G-B.
        /// </summary>
        [Fact(DisplayName = "LegendLabels (IMU/Gyroscope): X,Y,Z")]
        public void LegendLabels_IMU_Gyroscope_XYZ()
        {
            var (imu, cfg) = ImuAllOn();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            var labels = vm.LegendLabels;
            Assert.Equal(new[] { "X", "Y", "Z" }, labels.ToArray());


            Assert.Equal("X", vm.Legend1Text);
            Assert.Equal("Y", vm.Legend2Text);
            Assert.Equal("Z", vm.Legend3Text);

            // Colors: Red, Green (because 3 series), Blue
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Green, vm.Legend2Color);
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }


        /// <summary>
        /// IMU split variant should still expose X,Y,Z with the same color mapping.
        /// Expected: LegendLabels == ["X","Y","Z"]; colors R-G-B.
        /// </summary>
        [Fact(DisplayName = "LegendLabels (IMU Split variant): X,Y,Z unchanged")]
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


        /// <summary>
        /// EXG/ECG legends: expect EXG1, EXG2; third legend text is empty; colors Red, Blue, Blue.
        /// Expected: LegendLabels == ["EXG1","EXG2"]; Legend3Text == ""; colors R-B-B.
        /// </summary>
        [Fact(DisplayName = "LegendLabels (EXG/ECG): EXG1,EXG2; Legend3Text = \"\"")]
        public void LegendLabels_EXG_ECG_EXG1_EXG2()
        {
            var (exg, cfg) = ExgECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "ECG";

            var labels = vm.LegendLabels;
            Assert.Equal(new[] { "EXG1", "EXG2" }, labels.ToArray());

            // Third entry must be empty
            Assert.Equal("EXG1", vm.Legend1Text);
            Assert.Equal("EXG2", vm.Legend2Text);
            Assert.Equal(string.Empty, vm.Legend3Text);

            // Colors: Red, Blue (Count==2), Blue (constant for Legend3Color)
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Blue, vm.Legend2Color);
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }


        /// <summary>
        /// EXG ECG split variant: expect EXG1,EXG2 with two-channel colors.
        /// Expected: LegendLabels == ["EXG1","EXG2"]; colors R-B-B; Legend3Text empty.
        /// </summary>
        [Fact(DisplayName = "LegendLabels (EXG Split variant): EXG1,EXG2 + 2-channel colors")]
        public void LegendLabels_EXG_SplitVariant_EXG1_EXG2()
        {
            var (exg, cfg) = ExgECG();
            var vm = new DataPageViewModel(exg, cfg);

            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";

            Assert.Equal(new[] { "EXG1", "EXG2" }, vm.LegendLabels.ToArray());
            Assert.Equal(Colors.Red, vm.Legend1Color);
            Assert.Equal(Colors.Blue, vm.Legend2Color);  // 2 channels -> Blue
            Assert.Equal(Colors.Blue, vm.Legend3Color);  // still Blue
            Assert.Equal(string.Empty, vm.Legend3Text);  // no third label
        }


        /// <summary>
        /// Unknown parameter should produce a single raw legend and coherent default texts/colors.
        /// Expected: single label "__unknown__"; Legend1Text="__unknown__", Legend2/3Text empty; colors R-G-B.
        /// </summary>
        [Fact(DisplayName = "LegendLabels: unknown parameter -> single raw label + coherent texts/colors")]
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
            Assert.Equal(Colors.Green, vm.Legend2Color);  // Count != 2 -> Green
            Assert.Equal(Colors.Blue, vm.Legend3Color);
        }


        // ----- OnAutoYAxisChanged behavior -----


        /// <summary>
        /// Enabling AutoYAxis should compute/apply auto limits, disable manual editing, sync text mirrors,
        /// and request a chart update.
        /// Expected: IsYAxisManualEnabled=false; Y limits == auto; texts synced; message empty; redraw requested.
        /// </summary>
        [Fact(DisplayName = "AutoYAxis=true: applies auto limits, disables manual, syncs texts, updates chart")]
        public void AutoYAxis_true_applies_calculated_auto_limits_and_updates_ui()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Select a group and set custom manual limits (to be backed up)
            vm.SelectedParameter = "Gyroscope";
            vm.YAxisMin = -123.4;
            vm.YAxisMax = 234.5;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Enable auto
            vm.AutoYAxis = true;

            // Read auto limits from private fields
            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // Consistent auto state
            Assert.False(vm.IsYAxisManualEnabled);
            Assert.Equal(autoMin, vm.YAxisMin, 6);
            Assert.Equal(autoMax, vm.YAxisMax, 6);
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Disabling AutoYAxis should restore last valid manual limits and re-enable input,
        /// sync texts and request a chart update.
        /// Expected: IsYAxisManualEnabled=true; limits restored; texts synced; message empty; redraw requested.
        /// </summary>
        [Fact(DisplayName = "AutoYAxis=false: restores backed-up manual limits and re-enables input")]
        public void AutoYAxis_false_restores_backed_up_manual_limits()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.SelectedParameter = "Gyroscope";

            // Set manual limits and let them be backed up when toggling to auto
            vm.YAxisMin = -50;
            vm.YAxisMax = 150;

            // Go auto to store last-valid manual values
            vm.AutoYAxis = true;

            // Retrieve last-valid manual values from private fields
            var lastMin = GetPrivate<double>(vm, "_lastValidYAxisMin");
            var lastMax = GetPrivate<double>(vm, "_lastValidYAxisMax");
            Assert.Equal(-50, lastMin, 6);
            Assert.Equal(150, lastMax, 6);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            // Back to manual
            vm.AutoYAxis = false;

            // Limits restored to last-valid
            Assert.True(vm.IsYAxisManualEnabled);
            Assert.Equal(lastMin, vm.YAxisMin, 6);
            Assert.Equal(lastMax, vm.YAxisMax, 6);
            Assert.Equal(vm.YAxisMin.ToString(CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        // ----- CalculateAutoYAxisRange behavior -----


        /// <summary>
        /// Grouped parameters with data: auto range based on global min/max with ±10% margin, rounded to 3 decimals.
        /// Expected: min=-5, max=12 → [-6.7, 13.7].
        /// </summary>
        [Fact(DisplayName = "AutoRange (group): with data uses min/max ±10%")]
        public void AutoRange_Group_WithData_UsesMinMaxPlus10pct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "Gyroscope";

            // Populate data for the three sub-series
            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["GyroscopeX"] = new List<float> { -5f, -2f, 1f };
            data["GyroscopeY"] = new List<float> { 0f, 10f, 12f };
            data["GyroscopeZ"] = new List<float> { 3f, 4f, 6f };

            // Act
            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // global min = -5, global max = 12, range=17 -> margin=1.7 -> expected: [-6.7, 13.7] rounded to 3 dec.
            Assert.Equal(-6.7, autoMin, 3);
            Assert.Equal(13.7, autoMax, 3);
        }


        /// <summary>
        /// Grouped parameters without data: auto range falls back to the group's default limits.
        /// Expected: _autoYAxisMin/Max equal current group's defaults.
        /// </summary>
        [Fact(DisplayName = "AutoRange (group): without data -> falls back to group defaults")]
        public void AutoRange_Group_NoData_FallsBackToDefaults()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Select group; VM sets default limits
            vm.SelectedParameter = "Gyroscope";
            var expectedMin = vm.YAxisMin;
            var expectedMax = vm.YAxisMax;

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            Assert.Equal(expectedMin, autoMin, 6);
            Assert.Equal(expectedMax, autoMax, 6);
        }


        /// <summary>
        /// Single-series with data: auto range uses min/max ±10% margin.
        /// Expected: [3600,3800,3900] -> [3570, 3930].
        /// </summary>
        [Fact(DisplayName = "AutoRange (single): BatteryVoltage with data -> min/max ±10%")]
        public void AutoRange_Single_WithData_UsesMinMaxPlus10pct()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "BatteryVoltage"; // single series

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["BatteryVoltage"] = new List<float> { 3600f, 3800f, 3900f }; // mV

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            // min=3600, max=3900, range=300 -> margin=30 -> [3570, 3930]
            Assert.Equal(3570, autoMin, 3);
            Assert.Equal(3930, autoMax, 3);
        }


        /// <summary>
        /// Single-series without data: auto range falls back to the parameter defaults.
        /// Expected: _autoYAxisMin/Max equal current parameter defaults.
        /// </summary>
        [Fact(DisplayName = "AutoRange (single): without data -> falls back to parameter defaults")]
        public void AutoRange_Single_NoData_FallsBackToDefaults()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Select single parameter; VM sets defaults
            vm.SelectedParameter = "BatteryVoltage";
            var expectedMin = vm.YAxisMin;
            var expectedMax = vm.YAxisMax;

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            Assert.Equal(expectedMin, autoMin, 6);
            Assert.Equal(expectedMax, autoMax, 6);
        }


        /// <summary>
        /// Constant data should produce a small symmetric margin around center.
        /// Expected: center=5; margin=|center|*0.1 + 0.1 = 0.6 -> [4.4, 5.6].
        /// </summary>
        [Fact(DisplayName = "AutoRange: constant data -> small margin around center")]
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

            // center=5; margin = |center|*0.1 + 0.1 = 0.5 + 0.1 = 0.6 → [4.4, 5.6] rounded
            Assert.Equal(4.4, autoMin, 3);
            Assert.Equal(5.6, autoMax, 3);
        }


        /// <summary>
        /// Auto range min/max are rounded to 3 decimals.
        /// Expected: values equal Math.Round(value, 3).
        /// </summary>
        [Fact(DisplayName = "AutoRange: rounds limits to 3 decimals")]
        public void AutoRange_Rounds_To_Three_Decimals()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);
            vm.SelectedParameter = "BatteryVoltage";

            var data = GetPrivate<Dictionary<string, List<float>>>(vm, "dataPointsCollections");
            data["BatteryVoltage"] = new List<float> { 1.23449f, 1.23451f }; // Crafted for rounding test

            InvokePrivate(vm, "CalculateAutoYAxisRange");

            var autoMin = GetPrivate<double>(vm, "_autoYAxisMin");
            var autoMax = GetPrivate<double>(vm, "_autoYAxisMax");

            double Round3(double v) => Math.Round(v, 3);
            Assert.Equal(Round3(autoMin), autoMin);
            Assert.Equal(Round3(autoMax), autoMax);
        }


        // ----- UpdateTextProperties behavior -----


        /// <summary>
        /// UpdateTextProperties should sync text mirrors from numeric values and raise PropertyChanged,
        /// without altering numeric fields.
        /// Expected: text mirrors updated (InvariantCulture); PropertyChanged raised; numerics unchanged.
        /// </summary>
        [Fact(DisplayName = "UpdateTextProperties: syncs text mirrors and raises PropertyChanged")]
        public void UpdateTextProperties_syncs_texts_and_raises_PropertyChanged()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Non-default numerics
            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;
            vm.TimeWindowSeconds = 42;
            vm.XAxisLabelInterval = 7;

            // Dirty texts to verify overwrite
            vm.YAxisMinText = "xxx";
            vm.YAxisMaxText = "yyy";
            vm.TimeWindowSecondsText = "zzz";
            vm.XAxisLabelIntervalText = "qqq";

            // Track PropertyChanged
            var changed = new HashSet<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                    changed.Add(e.PropertyName);
            };

            // Act
            InvokePrivate(vm, "UpdateTextProperties");

            Assert.Equal(vm.YAxisMin.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            Assert.Contains(nameof(vm.YAxisMinText), changed);
            Assert.Contains(nameof(vm.YAxisMaxText), changed);
            Assert.Contains(nameof(vm.TimeWindowSecondsText), changed);
            Assert.Contains(nameof(vm.XAxisLabelIntervalText), changed);

            Assert.Equal(-12.34, vm.YAxisMin, 3);
            Assert.Equal(56.78, vm.YAxisMax, 3);
            Assert.Equal(42, vm.TimeWindowSeconds);
            Assert.Equal(7, vm.XAxisLabelInterval);
        }


        /// <summary>
        /// UpdateTextProperties should be idempotent; re-sync after numeric changes.
        /// Expected: first call aligns texts; second call without changes leaves identical texts; after changing numerics, texts update again.
        /// </summary>
        [Fact(DisplayName = "UpdateTextProperties: idempotent and re-syncs texts after numeric changes")]
        public void UpdateTextProperties_idempotent_and_updates_after_numeric_changes()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -1.23;
            vm.YAxisMax = 4.56;
            vm.TimeWindowSeconds = 12;
            vm.XAxisLabelInterval = 3;

            // Dirty texts to ensure overwrite
            vm.YAxisMinText = "nope";
            vm.YAxisMaxText = "nope";
            vm.TimeWindowSecondsText = "nope";
            vm.XAxisLabelIntervalText = "nope";

            // Track notifications
            var changed1 = new HashSet<string>();
            ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName)) changed1.Add(e.PropertyName!);
            };

            // Act 1
            InvokePrivate(vm, "UpdateTextProperties");

            // Assert 1
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            // Idempotence
            var beforeSecond_YMin = vm.YAxisMinText;
            var beforeSecond_YMax = vm.YAxisMaxText;
            var beforeSecond_TW = vm.TimeWindowSecondsText;
            var beforeSecond_XI = vm.XAxisLabelIntervalText;

            InvokePrivate(vm, "UpdateTextProperties");

            Assert.Equal(beforeSecond_YMin, vm.YAxisMinText);
            Assert.Equal(beforeSecond_YMax, vm.YAxisMaxText);
            Assert.Equal(beforeSecond_TW, vm.TimeWindowSecondsText);
            Assert.Equal(beforeSecond_XI, vm.XAxisLabelIntervalText);

            // Act 2: change numerics then re-sync
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

            // Assert 2
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
            Assert.Equal(vm.TimeWindowSeconds.ToString(), vm.TimeWindowSecondsText);
            Assert.Equal(vm.XAxisLabelInterval.ToString(), vm.XAxisLabelIntervalText);

            Assert.Contains(nameof(vm.YAxisMinText), changed2);
            Assert.Contains(nameof(vm.YAxisMaxText), changed2);
            Assert.Contains(nameof(vm.TimeWindowSecondsText), changed2);
            Assert.Contains(nameof(vm.XAxisLabelIntervalText), changed2);
        }


        // ----- UpdateYAxisTextPropertiesOnly behavior -----


        /// <summary>
        /// Sync only YAxisMinText/YAxisMaxText from numeric values using InvariantCulture.
        /// Expected: Y texts updated; other text properties untouched.
        /// </summary>
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: syncs YAxisMin/MaxText from numeric values")]
        public void UpdateYAxisTextPropertiesOnly_syncs_y_texts_from_numeric_values()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;

            // Dirty texts to verify overwrite
            vm.YAxisMinText = "dirty-min";
            vm.YAxisMaxText = "dirty-max";

            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal(vm.YAxisMin.ToString(inv), vm.YAxisMinText);
            Assert.Equal(vm.YAxisMax.ToString(inv), vm.YAxisMaxText);
        }


        /// <summary>
        /// Ensure UpdateYAxisTextPropertiesOnly does not touch TimeWindowSecondsText and XAxisLabelIntervalText.
        /// Expected: those two remain unchanged.
        /// </summary>
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: does not change TimeWindow/XAxisLabelInterval")]
        public void UpdateYAxisTextPropertiesOnly_does_not_change_other_texts()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // Capture current values (likely "20" and "5" by default)
            var beforeTW = vm.TimeWindowSecondsText;
            var beforeXI = vm.XAxisLabelIntervalText;

            // Change Y numerics to trigger Y text update
            vm.YAxisMin = -12.34;
            vm.YAxisMax = 56.78;

            // Call method under test
            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

            // Verify: TW/XI stay unchanged
            Assert.Equal(beforeTW, vm.TimeWindowSecondsText);
            Assert.Equal(beforeXI, vm.XAxisLabelIntervalText);
        }


        /// <summary>
        /// PropertyChanged should be raised only for YAxisMinText and YAxisMaxText.
        /// Expected: exactly two property names raised: YAxisMinText and YAxisMaxText.
        /// </summary>
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: raises PropertyChanged only for YAxisMinText/YAxisMaxText")]
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
            Assert.Equal(2, changes.Count); // Only two notifications
        }


        /// <summary>
        /// UpdateYAxisTextPropertiesOnly should be idempotent.
        /// Expected: second call leaves texts identical to the first call.
        /// </summary>
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: idempotent (multiple calls keep same result)")]
        public void UpdateYAxisTextPropertiesOnly_is_idempotent()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            vm.YAxisMin = -7.5;
            vm.YAxisMax = 123.4;

            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");
            var firstMin = vm.YAxisMinText;
            var firstMax = vm.YAxisMaxText;

            // Second call
            InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");
            Assert.Equal(firstMin, vm.YAxisMinText);
            Assert.Equal(firstMax, vm.YAxisMaxText);
        }


        /// <summary>
        /// Formatting must use InvariantCulture (dot decimal separator).
        /// Expected: YAxisMinText/YAxisMaxText contain '.' regardless of current culture.
        /// </summary>
        [Fact(DisplayName = "UpdateYAxisTextPropertiesOnly: uses InvariantCulture for formatting")]
        public void UpdateYAxisTextPropertiesOnly_uses_invariant_culture()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var prev = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("it-IT"); // comma decimal
                vm.YAxisMin = 1.5;
                vm.YAxisMax = 2.5;

                InvokePrivate(vm, "UpdateYAxisTextPropertiesOnly");

                // Must always use dot
                Assert.Equal("1.5", vm.YAxisMinText);
                Assert.Equal("2.5", vm.YAxisMaxText);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = prev;
            }
        }


        // ----- ValidateAndUpdateYAxisMin behavior -----


        /// <summary>
        /// With AutoYAxis enabled, ValidateAndUpdateYAxisMin should ignore input, not request a chart update,
        /// and keep ValidationMessage unchanged.
        /// Expected: YAxisMin and ValidationMessage unchanged; no redraw.
        /// </summary>
        [Fact(DisplayName = "YMin: AutoYAxis=true -> input ignored, no chart, no message")]
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


        /// <summary>
        /// Empty/whitespace input should reset YAxisMin to the parameter defaults,
        /// clear the validation message, and request a chart refresh.
        /// Expected: YAxisMin == -250 for "Gyroscope"; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "YMin: empty string -> reset to parameter defaults + chart + clear message")]
        public void ValidateAndUpdateYAxisMin_empty_resets_to_default_and_updates_chart()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // Set a non-default value to verify reset
            vm.YAxisMin = -100;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "   ");

            // Gyroscope default = -250
            Assert.Equal(-250, vm.YAxisMin, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// YMin should ignore partial numeric input like "+" or "-" (still being typed),
        /// produce no validation errors, not trigger a chart refresh, and keep the value unchanged.
        /// Expected: YAxisMin unchanged; ValidationMessage == ""; redraw == 0.
        /// </summary>
        /// <param name="input">A partial numeric token, e.g. "+" or "-".</param>
        [Theory(DisplayName = "YMin: partial input (+/-) -> no error, no chart, value unchanged")]
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


        /// <summary>
        /// A valid numeric YMin within allowed range and strictly less than YMax
        /// must be applied, clear the validation message, and request a chart refresh.
        /// Expected: YAxisMin == -123.45; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "YMin: valid input within range and < YMax -> apply, clear message, chart")]
        public void ValidateAndUpdateYAxisMin_valid_applies_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // Make sure YMax is high enough
            vm.YAxisMax = 250;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "-123.45");

            Assert.Equal(-123.45, vm.YAxisMin, 2);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Out-of-range YMin must not be applied, must show an error, and must roll the text back
        /// to the last valid value. No chart refresh should occur.
        /// Expected: YAxisMin unchanged; YAxisMinText rolled back; ValidationMessage contains "out of range"; redraw == 0.
        /// </summary>
        /// <param name="input">An out-of-range numeric string for YMin.</param>
        [Theory(DisplayName = "YMin: out-of-range -> error message and rollback text")]
        [InlineData("-999999")]
        [InlineData("999999")]
        public void ValidateAndUpdateYAxisMin_out_of_range_shows_error_and_rolls_back(string input)
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            var lastValid = vm.YAxisMin;
            var lastValidText = vm.YAxisMinText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", input);

            Assert.Contains("out of range", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.YAxisMin, 5);
            Assert.Equal(lastValidText, vm.YAxisMinText);
            Assert.Equal(0, redraws);
        }


        /// <summary>
        /// YMin greater than or equal to YMax must be rejected with a validation message,
        /// the text rolled back, and no chart refresh.
        /// Expected: ValidationMessage contains "greater than or equal"; YAxisMinText rolled back; redraw == 0.
        /// </summary>
        [Fact(DisplayName = "YMin: ≥ YMax -> error message and rollback text")]
        public void ValidateAndUpdateYAxisMin_blocks_when_ge_than_ymax()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            vm.YAxisMin = -10;
            vm.YAxisMax = 5;
            var lastValidText = vm.YAxisMinText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMin", "5");

            Assert.Contains("greater than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValidText, vm.YAxisMinText);
            Assert.Equal(0, redraws);
        }


        /// <summary>
        /// Non-numeric YMin must be rejected with a validation message and text rollback,
        /// leaving the numeric value unchanged and not triggering a chart refresh.
        /// Expected: YAxisMin unchanged; YAxisMinText rolled back; ValidationMessage contains "valid number"; redraw == 0.
        /// </summary>
        /// <param name="input">A non-numeric string (e.g., "abc").</param>
        [Theory(DisplayName = "YMin: non-numeric -> error message and rollback text")]
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


        // ----- ValidateAndUpdateYAxisMax behavior -----


        /// <summary>
        /// When AutoYAxis is true, YMax input must be ignored (no changes, no chart, no message).
        /// Expected: YAxisMax unchanged; ValidationMessage unchanged; redraw == 0.
        /// </summary>
        [Fact(DisplayName = "YMax: AutoYAxis=true -> input ignored, no chart, no message")]
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


        /// <summary>
        /// Empty/whitespace input should reset YAxisMax to the parameter defaults,
        /// clear the validation message, and request a chart refresh.
        /// Expected: YAxisMax == +250 for "Gyroscope"; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "YMax: empty string -> reset to parameter defaults + chart + clear message")]
        public void ValidateAndUpdateYAxisMax_empty_resets_to_default_and_updates_chart()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            // Set a non-default to verify reset
            vm.YAxisMax = 10;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "   ");

            Assert.Equal(250, vm.YAxisMax, 5);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Partial numeric input for YMax like "+" or "-" must be a no-op:
        /// no errors, no chart refresh, and no value change.
        /// Expected: YAxisMax unchanged; ValidationMessage == ""; redraw == 0.
        /// </summary>
        /// <param name="input">A partial numeric token, e.g. "+" or "-".</param>
        [Theory(DisplayName = "YMax: partial input (+/-) -> no error, no chart, value unchanged")]
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


        /// <summary>
        /// A valid numeric YMax within allowed range and strictly greater than YMin
        /// must be applied, clear the validation message, and request a chart refresh.
        /// Expected: YAxisMax == 123.45; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "YMax: valid input within range and > YMin -> apply, clear message, chart")]
        public void ValidateAndUpdateYAxisMax_valid_applies_value()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            vm.YAxisMin = -200;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "123.45");

            Assert.Equal(123.45, vm.YAxisMax, 2);
            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Out-of-range YMax must not be applied, must show an error, and must roll the text back
        /// to the last valid value. No chart refresh should occur.
        /// Expected: YAxisMax unchanged; YAxisMaxText rolled back; ValidationMessage contains "out of range"; redraw == 0.
        /// </summary>
        /// <param name="input">An out-of-range numeric string for YMax.</param>
        [Theory(DisplayName = "YMax: out-of-range -> error message and rollback text")]
        [InlineData("-999999")] 
        [InlineData("999999")]
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
            Assert.Equal(lastValid, vm.YAxisMax, 5);      
            Assert.Equal(lastValidText, vm.YAxisMaxText); 
            Assert.Equal(0, redraws);                     
        }


        /// <summary>
        /// YMax less than or equal to YMin must be rejected with a validation message,
        /// the text rolled back, and no chart refresh.
        /// Expected: ValidationMessage contains "less than or equal"; YAxisMaxText rolled back; YAxisMax unchanged; redraw == 0.
        /// </summary>
        [Fact(DisplayName = "YMax: ≤ YMin -> error message and rollback text")]
        public void ValidateAndUpdateYAxisMax_blocks_when_le_than_ymin()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Gyroscope" };

            vm.YAxisMin = 5;                 
            vm.YAxisMax = 10;               
            var lastValidText = vm.YAxisMaxText;

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateYAxisMax", "4");

            Assert.Contains("less than or equal", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValidText, vm.YAxisMaxText);
            Assert.Equal(10, vm.YAxisMax, 5);
            Assert.Equal(0, redraws);
        }


        /// <summary>
        /// Non-numeric YMax must be rejected with a validation message and text rollback,
        /// leaving the numeric value unchanged and not triggering a chart refresh.
        /// Expected: YAxisMax unchanged; YAxisMaxText rolled back; ValidationMessage contains "valid number"; redraw == 0.
        /// </summary>
        /// <param name="input">A non-numeric string (e.g., "abc").</param>
        [Theory(DisplayName = "YMax: non-numeric -> error message and rollback text")]
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


        // ----- ValidateAndUpdateTimeWindow behavior -----


        /// <summary>
        /// Empty/whitespace TimeWindow input should be a no-op, but it should clear any
        /// existing validation message and not trigger a chart refresh.
        /// Expected: TimeWindowSeconds and TimeWindowSecondsText unchanged; ValidationMessage == ""; redraw == 0.
        /// </summary>
        [Fact(DisplayName = "TimeWindow: empty/whitespace -> no change, clear message, no chart")]
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


        /// <summary>
        /// A valid TimeWindow must update the value, clear all data series and counters,
        /// clear the validation message, and request a chart refresh.
        /// Expected: TimeWindowSeconds == 30; data/time collections empty; sampleCounter == 0; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "TimeWindow: valid -> update value, clear data/counter, refresh chart")]
        public void TimeWindow_valid_updates_value_clears_data_and_refreshes()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg) { SelectedParameter = "Low-Noise Accelerometer" };

            // Pre-populate internal series to verify clearing
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

            Assert.Empty(dataDict["Low-Noise AccelerometerX"]);
            Assert.Empty(timeDict["Low-Noise AccelerometerX"]);
            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));

            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Non-numeric TimeWindow input must be rejected with a validation message and text rollback,
        /// without requesting a chart refresh.
        /// Expected: TimeWindowSeconds unchanged; TimeWindowSecondsText rolled back; ValidationMessage contains "valid positive number"; redraw == 0.
        /// </summary>
        [Fact(DisplayName = "TimeWindow: non-numeric -> validation message and rollback text")]
        public void TimeWindow_invalid_text_shows_error_and_rolls_back()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var lastValid = vm.TimeWindowSeconds;      // 20
            var lastText = vm.TimeWindowSecondsText;   // "20"

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "abc");

            Assert.Contains("valid positive number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(lastValid, vm.TimeWindowSeconds);
            Assert.Equal(lastText, vm.TimeWindowSecondsText);
            Assert.Equal(0, redraws);
        }


        /// <summary>
        /// Too-large TimeWindow must be rejected with a validation message and a text reset to the last valid value.
        /// Expected: ValidationMessage contains "too large"; TimeWindowSecondsText reset to previous; TimeWindowSeconds unchanged (default 20).
        /// </summary>
        [Fact(DisplayName = "TimeWindow: too large -> validation + reset text")]
        public void TimeWindow_above_max_shows_error_and_resets_text()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            var beforeText = vm.TimeWindowSecondsText;

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", (100000).ToString());

            Assert.Contains("too large", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeText, vm.TimeWindowSecondsText);  
            Assert.Equal(20, vm.TimeWindowSeconds);               
        }


        /// <summary>
        /// Too-small TimeWindow must be rejected with a validation message and a text reset to the last valid value.
        /// Expected: ValidationMessage contains "too small"; TimeWindowSecondsText reset to previous; TimeWindowSeconds unchanged (default 20).
        /// </summary>
        [Fact(DisplayName = "TimeWindow: too small -> validation + reset text")]
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


        /// <summary>
        /// On valid TimeWindow update, the backing field _lastValidTimeWindowSeconds must be updated.
        /// Expected: _lastValidTimeWindowSeconds == 45.
        /// </summary>
        [Fact(DisplayName = "TimeWindow: valid -> updates _lastValidTimeWindowSeconds")]
        public void TimeWindow_valid_updates_last_valid_backing_field()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "45");

            var last = GetPrivate<int>(vm, "_lastValidTimeWindowSeconds");
            Assert.Equal(45, last);
        }


        /// <summary>
        /// TimeWindow changes should not affect YAxisMin/Max text or XAxisLabelInterval text.
        /// Expected: YAxisMinText, YAxisMaxText, and XAxisLabelIntervalText remain unchanged.
        /// </summary>
        [Fact(DisplayName = "TimeWindow: no side-effect on YAxis and XAxis text properties")]
        public void TimeWindow_changes_do_not_touch_yaxis_and_xaxis_texts()
        {
            var (imu, cfg) = MakeImuDeviceWithAllFlags();
            var vm = new DataPageViewModel(imu, cfg);

            // “Dirty” the texts to verify they are not touched
            SetPrivate(vm, "_yAxisMinText", "YMIN-TXT");
            SetPrivate(vm, "_yAxisMaxText", "YMAX-TXT");
            SetPrivate(vm, "_xAxisLabelIntervalText", "XINT-TXT");

            InvokePrivate(vm, "ValidateAndUpdateTimeWindow", "25");

            Assert.Equal("YMIN-TXT", vm.YAxisMinText);
            Assert.Equal("YMAX-TXT", vm.YAxisMaxText);
            Assert.Equal("XINT-TXT", vm.XAxisLabelIntervalText);
        }


        // ----- ValidateAndUpdateXAxisInterval behavior -----


        /// <summary>
        /// Empty X-axis label interval input resets to default (5) and triggers a chart refresh
        /// without producing a validation error.
        /// Expected: XAxisLabelInterval == 5; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "XLabel: empty -> reset to default (5), no errors, chart refresh")]
        public void XAxisInterval_empty_resets_to_default_and_refreshes()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());
            Assert.Equal(5, vm.XAxisLabelInterval);

            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "");

            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// A valid X-axis label interval updates the numeric value and requests a chart refresh,
        /// but does not change the text field (the public text is updated elsewhere).
        /// Expected: XAxisLabelInterval updated to 7; XAxisLabelIntervalText unchanged; ValidationMessage == ""; redraw >= 1.
        /// </summary>
        [Fact(DisplayName = "XLabel: valid -> update value, keep text, refresh chart")]
        public void XAxisInterval_valid_updates_value_and_refreshes_chart()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            Assert.Equal(5, vm.XAxisLabelInterval);
            var prevText = vm.XAxisLabelIntervalText;
            int redraws = 0;
            vm.ChartUpdateRequested += (_, __) => redraws++;

            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "7");

            Assert.Equal(7, vm.XAxisLabelInterval);
            Assert.Equal(prevText, vm.XAxisLabelIntervalText);
            Assert.True(string.IsNullOrEmpty(vm.ValidationMessage));
            Assert.True(redraws >= 1);
        }


        /// <summary>
        /// Too-low X-axis label interval must be rejected with a validation message and text reset
        /// to the last valid value. The numeric value stays unchanged.
        /// Expected: ValidationMessage contains "too low"; XAxisLabelInterval == 5; XAxisLabelIntervalText reset to previous.
        /// </summary>
        [Fact(DisplayName = "XLabel: too low (<MIN) -> message and reset text to last valid")]
        public void XAxisInterval_too_low_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            Assert.Equal(5, vm.XAxisLabelInterval);
            var lastValidText = vm.XAxisLabelIntervalText; 

            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "0");

            Assert.Contains("too low", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval); 
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText);
        }


        /// <summary>
        /// Too-high X-axis label interval must be rejected with a validation message and text reset
        /// to the last valid value. The numeric value stays unchanged.
        /// Expected: ValidationMessage contains "too high"; XAxisLabelInterval == 5; XAxisLabelIntervalText reset to previous.
        /// </summary>
        [Fact(DisplayName = "XLabel: too high (>MAX) -> message and reset text to last valid")]
        public void XAxisInterval_too_high_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var lastValidText = vm.XAxisLabelIntervalText;

            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "20000");

            Assert.Contains("too high", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText);
        }


        /// <summary>
        /// Non-numeric X-axis label interval must be rejected with a validation message and text reset
        /// to the last valid value. The numeric value stays unchanged.
        /// Expected: ValidationMessage contains "valid positive number"; XAxisLabelInterval == 5; XAxisLabelIntervalText reset to previous.
        /// </summary>
        [Fact(DisplayName = "XLabel: non-numeric -> message and reset text to last valid")]
        public void XAxisInterval_invalid_text_shows_error_and_resets_text()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var lastValidText = vm.XAxisLabelIntervalText;

            InvokePrivate(vm, "ValidateAndUpdateXAxisInterval", "abc");

            Assert.Contains("valid positive number", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(5, vm.XAxisLabelInterval);
            Assert.Equal(lastValidText, vm.XAxisLabelIntervalText);
        }


        // ----- ResetAllCounters behavior -----


        /// <summary>
        /// ResetAllCounters must set the internal sampleCounter to zero.
        /// Expected: sampleCounter == 0.
        /// </summary>
        [Fact(DisplayName = "ResetAllCounters: zeroes sampleCounter")]
        public void ResetAllCounters_zeros_sample_counter()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "sampleCounter", 42);

            InvokePrivate(vm, "ResetAllCounters");

            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
        }


        /// <summary>
        /// ResetAllCounters is idempotent: multiple calls keep sampleCounter at zero.
        /// Expected: sampleCounter == 0 after repeated calls.
        /// </summary>
        [Fact(DisplayName = "ResetAllCounters: idempotent (second call stays 0)")]
        public void ResetAllCounters_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "sampleCounter", 5);
            InvokePrivate(vm, "ResetAllCounters");
            InvokePrivate(vm, "ResetAllCounters");

            Assert.Equal(0, GetPrivate<int>(vm, "sampleCounter"));
        }


        // ----- ResetSamplingRateText behavior -----


        /// <summary>
        /// Restores the sampling-rate text to the last valid value using InvariantCulture formatting.
        /// Expected: SamplingRateText == "123.45".
        /// </summary>
        [Fact(DisplayName = "ResetSamplingRateText: restores text to last valid (invariant)")]
        public void ResetSamplingRateText_restores_last_valid_invariant()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            // Set last-valid and dirty the current text
            SetPrivate<double>(vm, "_lastValidSamplingRate", 123.45);
            SetPrivate<string>(vm, "_samplingRateText", "WRONG");

            InvokePrivate(vm, "ResetSamplingRateText");

            Assert.Equal("123.45", vm.SamplingRateText);
        }


        /// <summary>
        /// Always uses InvariantCulture to format the sampling-rate text even if the current culture uses commas.
        /// Expected: SamplingRateText == "12.5" when last valid is 12.5 and culture is it-IT.
        /// </summary>
        [Fact(DisplayName = "ResetSamplingRateText: always uses InvariantCulture (even with it-IT)")]
        public void ResetSamplingRateText_uses_invariant_culture()
        {
            var prev = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            try
            {
                var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

                // 12.5 must become "12.5" (not "12,5")
                SetPrivate<double>(vm, "_lastValidSamplingRate", 12.5);
                SetPrivate<string>(vm, "_samplingRateText", "??");

                InvokePrivate(vm, "ResetSamplingRateText");

                Assert.Equal("12.5", vm.SamplingRateText);
            }
            finally { CultureInfo.CurrentCulture = prev; }
        }


        // ----- ResetYAxisMinText behavior -----


        /// <summary>
        /// Restores YAxisMinText to the last valid value without changing the numeric YAxisMin.
        /// Expected: YAxisMinText == "-3.2"; YAxisMin (numeric) unchanged.
        /// </summary>
        [Fact(DisplayName = "ResetYAxisMinText: restores YAxisMinText to last valid")]
        public void ResetYAxisMinText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.YAxisMin; // Must not change
            SetPrivate<double>(vm, "_lastValidYAxisMin", -3.2);
            SetPrivate<string>(vm, "_yAxisMinText", "sporcato");

            InvokePrivate(vm, "ResetYAxisMinText");

            Assert.Equal("-3.2", vm.YAxisMinText);
            Assert.Equal(beforeNumeric, vm.YAxisMin, 5); // Text only, not numeric
        }


        /// <summary>
        /// ResetYAxisMinText is idempotent: repeated calls keep the same restored text.
        /// Expected: YAxisMinText == "-1" after repeated calls when last valid is -1.0.
        /// </summary>
        [Fact(DisplayName = "ResetYAxisMinText: idempotent")]
        public void ResetYAxisMinText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<double>(vm, "_lastValidYAxisMin", -1.0);
            SetPrivate<string>(vm, "_yAxisMinText", "X");
            InvokePrivate(vm, "ResetYAxisMinText");
            InvokePrivate(vm, "ResetYAxisMinText");

            Assert.Equal("-1", vm.YAxisMinText);
        }


        // ----- ResetYAxisMaxText behavior -----


        /// <summary>
        /// Restores YAxisMaxText to the last valid value without changing the numeric YAxisMax.
        /// Expected: YAxisMaxText == "7.75"; YAxisMax (numeric) unchanged.
        /// </summary>
        [Fact(DisplayName = "ResetYAxisMaxText: restores YAxisMaxText to last valid")]
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


        /// <summary>
        /// ResetYAxisMaxText is idempotent: repeated calls keep the same restored text.
        /// Expected: YAxisMaxText == "10" after repeated calls when last valid is 10.0.
        /// </summary>
        [Fact(DisplayName = "ResetYAxisMaxText: idempotent")]
        public void ResetYAxisMaxText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<double>(vm, "_lastValidYAxisMax", 10.0);
            SetPrivate<string>(vm, "_yAxisMaxText", "wrong");
            InvokePrivate(vm, "ResetYAxisMaxText");
            InvokePrivate(vm, "ResetYAxisMaxText");

            Assert.Equal("10", vm.YAxisMaxText);
        }


        // ----- ResetTimeWindowText behavior -----


        /// <summary>
        /// Restores TimeWindowSecondsText to the last valid value without changing TimeWindowSeconds.
        /// Expected: TimeWindowSecondsText == "30"; TimeWindowSeconds unchanged.
        /// </summary>
        [Fact(DisplayName = "ResetTimeWindowText: restores TimeWindowSecondsText to last valid")]
        public void ResetTimeWindowText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.TimeWindowSeconds;
            SetPrivate<int>(vm, "_lastValidTimeWindowSeconds", 30);
            SetPrivate<string>(vm, "_timeWindowSecondsText", "dirty");

            InvokePrivate(vm, "ResetTimeWindowText");

            Assert.Equal("30", vm.TimeWindowSecondsText);
            Assert.Equal(beforeNumeric, vm.TimeWindowSeconds); // Text only
        }


        /// <summary>
        /// ResetTimeWindowText is idempotent: repeated calls keep the same restored text.
        /// Expected: TimeWindowSecondsText == "45" after repeated calls when last valid is 45.
        /// </summary>
        [Fact(DisplayName = "ResetTimeWindowText: idempotent")]
        public void ResetTimeWindowText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "_lastValidTimeWindowSeconds", 45);
            SetPrivate<string>(vm, "_timeWindowSecondsText", "foo");

            InvokePrivate(vm, "ResetTimeWindowText");
            InvokePrivate(vm, "ResetTimeWindowText");

            Assert.Equal("45", vm.TimeWindowSecondsText);
        }


        // ----- ResetXAxisIntervalText behavior -----


        /// <summary>
        /// Restores XAxisLabelIntervalText to the last valid value without changing XAxisLabelInterval.
        /// Expected: XAxisLabelIntervalText == "7"; XAxisLabelInterval unchanged.
        /// </summary>
        [Fact(DisplayName = "ResetXAxisIntervalText: restores XAxisLabelIntervalText to last valid")]
        public void ResetXAxisIntervalText_restores_text_to_last_valid()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            var beforeNumeric = vm.XAxisLabelInterval;
            SetPrivate<int>(vm, "_lastValidXAxisLabelInterval", 7);
            SetPrivate<string>(vm, "_xAxisLabelIntervalText", "bad");

            InvokePrivate(vm, "ResetXAxisIntervalText");

            Assert.Equal("7", vm.XAxisLabelIntervalText);
            Assert.Equal(beforeNumeric, vm.XAxisLabelInterval); // Text only
        }


        /// <summary>
        /// ResetXAxisIntervalText is idempotent: repeated calls keep the same restored text.
        /// Expected: XAxisLabelIntervalText == "1" after repeated calls when last valid is 1.
        /// </summary>
        [Fact(DisplayName = "ResetXAxisIntervalText: idempotent")]
        public void ResetXAxisIntervalText_is_idempotent()
        {
            var vm = new DataPageViewModel(new ShimmerSDK.IMU.ShimmerSDK_IMU(), AllOnCfg());

            SetPrivate<int>(vm, "_lastValidXAxisLabelInterval", 1);
            SetPrivate<string>(vm, "_xAxisLabelIntervalText", "nope");
            InvokePrivate(vm, "ResetXAxisIntervalText");
            InvokePrivate(vm, "ResetXAxisIntervalText");

            Assert.Equal("1", vm.XAxisLabelIntervalText);
        }


        // ----- GetDefaultYAxisMin behavior -----


        /// <summary>
        /// Helper: that invokes a private static double-returning method on <see cref="DataPageViewModel"/> with a single string argument.
        /// </summary>
        /// <param name="methodName">The private static method name to invoke (e.g., "GetDefaultYAxisMin").</param>
        /// <param name="arg">The string parameter to pass to the method.</param>
        /// <returns>The method's double return value.</returns>
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


        // TEST MIN
        public class GetDefaultYAxisMin_Tests
        {

            /// <summary>
            /// IMU group names must return expected default Y-axis minima.
            /// Expected: (-20, -20, -250, -5) respectively.
            /// </summary>
            /// <param name="group">Group name.</param>
            /// <param name="expected">Expected min value.</param>
            [Theory(DisplayName = "GetDefaultYAxisMin: IMU groups -> expected values")]
            [InlineData("Low-Noise Accelerometer", -20)]
            [InlineData("Wide-Range Accelerometer", -20)]
            [InlineData("Gyroscope", -250)]
            [InlineData("Magnetometer", -5)]
            public void Groups_Return_Expected_Min(string group, double expected)
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", group);
                Assert.Equal(expected, min, 5);
            }


            /// <summary>
            /// IMU single-parameter names must return expected default Y-axis minima.
            /// Expected: values as per InlineData.
            /// </summary>
            /// <param name="param">Parameter name.</param>
            /// <param name="expected">Expected min value.</param>
            [Theory(DisplayName = "GetDefaultYAxisMin: IMU singles -> expected values")]
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


            /// <summary>
            /// EXG family names must return -15 for the default Y-axis minimum.
            /// Expected: -15.0 for the provided EXG labels.
            /// </summary>
            /// <param name="exg">EXG mode label.</param>
            [Theory(DisplayName = "GetDefaultYAxisMin: EXG -> -15")]
            [InlineData("ECG")]
            [InlineData("EMG")]
            [InlineData("EXG Test")]
            [InlineData("Respiration")]
            public void EXG_Returns_Minus15_Min(string exg)
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", exg);
                Assert.Equal(-15.0, min, 5);
            }


            /// <summary>
            /// Unknown parameter names must fall back to 0 for the default Y-axis minimum.
            /// Expected: 0.0.
            /// </summary>
            [Fact(DisplayName = "GetDefaultYAxisMin: unknown -> 0")]
            public void Unknown_Returns_0_Min()
            {
                double min = InvokePrivateStaticDouble("GetDefaultYAxisMin", "__unknown__");
                Assert.Equal(0.0, min, 5);
            }
        }


        // TEST MAX
        public class GetDefaultYAxisMax_Tests
        {

            /// <summary>
            /// IMU group names must return expected default Y-axis maxima.
            /// Expected: (20, 20, 250, 5) respectively.
            /// </summary>
            /// <param name="group">Group name.</param>
            /// <param name="expected">Expected max value.</param>
            [Theory(DisplayName = "GetDefaultYAxisMax: IMU groups -> expected values")]
            [InlineData("Low-Noise Accelerometer", 20)]
            [InlineData("Wide-Range Accelerometer", 20)]
            [InlineData("Gyroscope", 250)]
            [InlineData("Magnetometer", 5)]
            public void Groups_Return_Expected_Max(string group, double expected)
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", group);
                Assert.Equal(expected, max, 5);
            }


            /// <summary>
            /// IMU single-parameter names must return expected default Y-axis maxima.
            /// Expected: values as per InlineData.
            /// </summary>
            /// <param name="param">Parameter name.</param>
            /// <param name="expected">Expected max value.</param>
            [Theory(DisplayName = "GetDefaultYAxisMax: IMU singles -> expected values")]
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


            /// <summary>
            /// EXG family names must return +15 for the default Y-axis maximum.
            /// Expected: 15.0 for the provided EXG labels.
            /// </summary>
            /// <param name="exg">EXG mode label.</param>
            [Theory(DisplayName = "GetDefaultYAxisMax: EXG -> +15")]
            [InlineData("ECG")]
            [InlineData("EMG")]
            [InlineData("EXG Test")]
            [InlineData("Respiration")]
            public void EXG_Returns_Plus15_Max(string exg)
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", exg);
                Assert.Equal(15.0, max, 5);
            }


            /// <summary>
            /// Unknown parameter names must fall back to 1 for the default Y-axis maximum.
            /// Expected: 1.0.
            /// </summary>
            [Fact(DisplayName = "GetDefaultYAxisMax: unknown -> 1")]
            public void Unknown_Returns_1_Max()
            {
                double max = InvokePrivateStaticDouble("GetDefaultYAxisMax", "__unknown__");
                Assert.Equal(1.0, max, 5);
            }
        }


        // ----- InitializeAvailableParameters behavior -----


        /// <summary>
        /// Helper: invoke the private instance method InitializeAvailableParameters on the given VM.
        /// </summary>
        /// <param name="vm">A <see cref="DataPageViewModel"/> instance.</param>
        private static void InvokePrivateInitializeAvailableParameters(object vm)
        {
            var mi = vm.GetType().GetMethod("InitializeAvailableParameters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            mi!.Invoke(vm, null);
        }


        /// <summary>
        /// Helper: set a private boolean field on the view model.
        /// </summary>
        /// <param name="vm">Target instance.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <param name="value">Boolean value to set.</param>
        private static void SetPrivateBool(object vm, string fieldName, bool value)
        {
            var f = vm.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(vm, value);
        }


        /// <summary>
        /// Helper: Sets multiple boolean flags on the VM by private field name.
        /// </summary>
        /// <param name="vm">Target instance.</param>
        /// <param name="flags">Dictionary of fieldName → value.</param>
        private static void SetFlags(object vm, IDictionary<string, bool> flags)
        {
            foreach (var kv in flags) SetPrivateBool(vm, kv.Key, kv.Value);
        }


        /// <summary>
        /// Helper: Sets a list-typed property (or backing field) by hint name; falls back to the first compatible field.
        /// </summary>
        /// <typeparam name="TList">List type to set.</typeparam>
        /// <param name="target">Target object.</param>
        /// <param name="propNameHint">Property/field name hint.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetListPropertyOrField<TList>(object target, string propNameHint, TList value)
            where TList : class
        {
            var t = target.GetType();

            // Property with setter
            var p = t.GetProperty(propNameHint,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value);
                return;
            }

            // Backing field containing hint
            var f = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     .FirstOrDefault(fi =>
                         fi.FieldType == typeof(TList) &&
                         fi.Name.Contains(propNameHint, StringComparison.OrdinalIgnoreCase));

            // First compatible field as fallback
            f ??= t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                   .FirstOrDefault(fi => fi.FieldType == typeof(TList));

            Assert.NotNull(f);
            f!.SetValue(target, value);
        }


        /// <summary>
        /// Helper: Sets a string property (or backing field) by hint name; falls back to the first compatible field.
        /// </summary>
        /// <param name="target">Target object.</param>
        /// <param name="propNameHint">Property/field name hint.</param>
        /// <param name="value">String value to assign.</param>
        private static void SetStringPropertyOrField(object target, string propNameHint, string value)
        {
            var t = target.GetType();

            // Property with setter
            var p = t.GetProperty(propNameHint,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                p.SetValue(target, value);
                return;
            }

            // Backing field containing hint
            var f = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     .FirstOrDefault(fi =>
                         fi.FieldType == typeof(string) &&
                         fi.Name.Contains(propNameHint, StringComparison.OrdinalIgnoreCase));

            // First compatible field as fallback
            f ??= t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                   .FirstOrDefault(fi => fi.FieldType == typeof(string));

            Assert.NotNull(f);
            f!.SetValue(target, value);
        }


        /// <summary>
        /// Helper: Creates a "raw" <see cref="DataPageViewModel"/> without invoking its constructor,
        /// wiring minimal state so that InitializeAvailableParameters can run.
        /// </summary>
        /// <returns>An uninitialized <see cref="DataPageViewModel"/> with minimal fields set.</returns>
        private static DataPageViewModel NewVmForAvailableParameters()
        {
            var t = typeof(DataPageViewModel);

            // Instantiate without running any constructor
            var vm = (DataPageViewModel)RuntimeHelpers.GetUninitializedObject(t);

            // Minimal state
            SetListPropertyOrField(vm, "AvailableParameters", new ObservableCollection<string>());
            SetStringPropertyOrField(vm, "SelectedParameter", "");

            return vm;
        }


        /// <summary>
        /// Helper: Alias for compatibility: returns a new VM prepared for InitializeAvailableParameters tests.
        /// </summary>
        /// <returns>A prepared <see cref="DataPageViewModel"/>.</returns>
        private static DataPageViewModel NewVm() => NewVmForAvailableParameters();


        /// <summary>
        /// With all feature flags disabled, AvailableParameters must be empty and SelectedParameter must be empty.
        /// Expected: AvailableParameters.Count == 0; SelectedParameter == "".
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: all disabled -> empty list and empty selection")]
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

            // Initial selection set to an invalid value to verify reset
            SetStringPropertyOrField(vm, "SelectedParameter", "Non-existing");

            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Empty(((IEnumerable<string>)vm.AvailableParameters).ToArray());
            Assert.Equal("", vm.SelectedParameter);
        }


        /// <summary>
        /// With only Low-Noise Accelerometer enabled, the list must contain two entries (main + split variant),
        /// and SelectedParameter must be the first entry.
        /// Expected: ["Low-Noise Accelerometer", "    → Low-Noise Accelerometer — separate charts (X·Y·Z)"]; SelectedParameter == first.
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: only LNA enabled -> 2 entries in order; selection is first")]
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


        /// <summary>
        /// With Gyroscope, Battery, and ExtA7 enabled, verifies the expected entries and the correct order,
        /// and that the selection points to the first entry ("Gyroscope").
        /// Expected: ["Gyroscope", "    → Gyroscope — separate charts (X·Y·Z)", "BatteryVoltage", "BatteryPercent", "ExtADC_A7"]; SelectedParameter == "Gyroscope".
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: multiple sensors (Gyro + Battery + ExtA7) -> expected entries and order")]
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


        /// <summary>
        /// With EXG enabled in a specific mode, InitializeAvailableParameters must emit the proper main label
        /// and split variant for that mode, with selection on the main label.
        /// Expected: [expectedMain, expectedSplit]; SelectedParameter == expectedMain.
        /// </summary>
        /// <param name="modeFlag">The EXG mode boolean field name to set true.</param>
        /// <param name="expectedMain">Expected main label for the EXG mode.</param>
        /// <param name="expectedSplit">Expected split label for the EXG mode.</param>
        [Theory(DisplayName = "InitializeAvailableParameters: EXG active in specific mode -> correct label and split variant")]
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


        /// <summary>
        /// With EXG enabled but no specific mode set, InitializeAvailableParameters must
        /// produce a generic "EXG" entry plus its split variant, and select "EXG".
        /// Expected: ["EXG", "    → EXG — separate charts (EXG1·EXG2)"]; SelectedParameter == "EXG".
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: EXG active without a mode -> generic EXG label")]
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


        /// <summary>
        /// If the previously selected parameter remains available after re-initialization,
        /// it must remain selected.
        /// Expected: SelectedParameter stays on "BatteryPercent".
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: valid SelectedParameter remains unchanged")]
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

            // First init
            InvokePrivateInitializeAvailableParameters(vm);

            // Force selection to an entry that is valid
            SetStringPropertyOrField(vm, "SelectedParameter", "BatteryPercent");

            // Re-initialize with the same flags: selection must not change
            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Contains("BatteryPercent", vm.AvailableParameters);
            Assert.Equal("BatteryPercent", vm.SelectedParameter);
        }


        /// <summary>
        /// If the previously selected parameter becomes invalid after re-initialization,
        /// the selection must move to the first entry or to empty if the list becomes empty.
        /// Expected: when everything is disabled, AvailableParameters is empty and SelectedParameter is "".
        /// </summary>
        [Fact(DisplayName = "InitializeAvailableParameters: invalid SelectedParameter -> selects first or empty")]
        public void Init_Selection_Replaced_When_NotAvailable()
        {
            var vm = NewVm();

            // Enable LNA to have a predictable first entry
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

            // First init
            InvokePrivateInitializeAvailableParameters(vm);

            Assert.Equal("Low-Noise Accelerometer", vm.SelectedParameter);

            // Now disable everything: previous selection becomes invalid
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


        // ----- CleanParameterName behavior -----


        /// <summary>
        /// Verifies that CleanParameterName removes UI adornments like the leading arrow and split hints,
        /// while leaving plain names untouched.
        /// Expected: mapped names equal the expected plain labels.
        /// </summary>
        /// <param name="raw">The raw label as shown in the UI list.</param>
        /// <param name="expected">The expected plain parameter name.</param>
        [Theory(DisplayName = "CleanParameterName: strips UI adornments (arrow and split hints)")]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)", "Gyroscope")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)", "ECG")]
        [InlineData("Magnetometer (separate charts)", "Magnetometer")]
        [InlineData("Low-Noise Accelerometer", "Low-Noise Accelerometer")]
        public void CleanParameterName_strips_UI_adornments(string raw, string expected)
        {
            var clean = DataPageViewModel.CleanParameterName(raw);
            Assert.Equal(expected, clean);
        }


        /// <summary>
        /// Ensures that <c>CleanParameterName</c> handles <c>null</c>/empty strings,
        /// extra spaces, and hyphenated split hints, returning a clean base name.
        /// Expected:
        /// - null / "" / "   " → ""
        /// - "    →   Magnetometer  - separate charts (X·Y·Z)   " → "Magnetometer"
        /// - "    →  EXG Test - separate charts (EXG1·EXG2) " → "EXG Test"
        /// </summary>
        /// <param name="raw">Raw label from the UI list (may be null/empty).</param>
        /// <param name="expected">Expected cleaned base name.</param>
        [Theory(DisplayName = "CleanParameterName: null/empty and hyphen/extra-spaces variants")]
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


        // ----- MapToInternalKey behavior -----


        /// <summary>
        /// Verifies that <c>MapToInternalKey</c> maps EXG channel tokens to the internal
        /// "Exg1"/"Exg2" keys (case-insensitive) and otherwise returns the cleaned label.
        /// Expected:
        /// - "EXG1" → "Exg1"
        /// - "exg2" → "Exg2"
        /// - "GyroscopeX" → "GyroscopeX"
        /// - "    → ECG — separate charts (EXG1·EXG2)" → "ECG"
        /// </summary>
        /// <param name="input">Input label to normalize.</param>
        /// <param name="expected">Expected normalized key.</param>
        [Theory(DisplayName = "MapToInternalKey: maps EXG channels and cleans UI adornments")]
        [InlineData("EXG1", "Exg1")]
        [InlineData("exg2", "Exg2")]
        [InlineData("GyroscopeX", "GyroscopeX")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)", "ECG")]
        public void MapToInternalKey_maps_exg_channels_and_cleans(string input, string expected)
        {
            var key = DataPageViewModel.MapToInternalKey(input);
            Assert.Equal(expected, key);
        }


        /// <summary>
        /// Handles edge cases for <c>MapToInternalKey</c>: trims spaces, is case-insensitive
        /// for EXG1/EXG2 channels, cleans adornments for generic EXG, and returns empty for empty input.
        /// Expected:
        /// - "  eXg1  " → "Exg1"
        /// - "   EXG2   " → "Exg2"
        /// - "    → EXG — separate charts (EXG1·EXG2)" → "EXG"
        /// - "" → "".
        /// </summary>
        /// <param name="input">Input label.</param>
        /// <param name="expected">Expected normalized key.</param>
        [Theory(DisplayName = "MapToInternalKey: spaces/Case/generic EXG/empty")]
        [InlineData("  eXg1  ", "Exg1")]
        [InlineData("   EXG2   ", "Exg2")]
        [InlineData("    → EXG — separate charts (EXG1·EXG2)", "EXG")]
        [InlineData("", "")]
        public void MapToInternalKey_handles_edge_cases(string input, string expected)
        {
            var key = DataPageViewModel.MapToInternalKey(input);
            Assert.Equal(expected, key);
        }


        // ----- IsSplitVariantLabel behavior -----


        /// <summary>
        /// Helper: to invoke the private static <c>IsSplitVariantLabel(string?)</c>.
        /// Expected: returns the boolean result of the private method.
        /// </summary>
        /// <param name="label">Label to inspect.</param>
        /// <returns><c>true</c> if the label represents a split-variant; otherwise <c>false</c>.</returns>
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


        /// <summary>
        /// Recognizes labels that denote "split/separate charts" variants (case-insensitive).
        /// Expected: returns true for arrow/split forms, plain "Split", etc.
        /// </summary>
        /// <param name="label">Candidate label.</param>
        [Theory(DisplayName = "IsSplitVariantLabel: recognizes split/separate charts labels (case-insensitive)")]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)")]
        [InlineData("Magnetometer - separate charts (X·Y·Z)")]      // Normal hyphen
        [InlineData("Split (two separate charts)")]                 // Contains "Split"
        [InlineData("split (tre grafici)")]                         // Word "split", case-insensitive
        public void IsSplitVariantLabel_true_cases(string label)
        {
            Assert.True(Invoke_IsSplitVariantLabel(label));
        }


        /// <summary>
        /// Must be false when there are no UI adornments/split hints,
        /// for clean labels or empty/whitespace.
        /// Expected: false for "Gyroscope", "Low-Noise Accelerometer", "EXG", "ECG", "", "   ".
        /// </summary>
        /// <param name="label">Candidate label.</param>
        [Theory(DisplayName = "IsSplitVariantLabel: not split when there are no adornments")]
        [InlineData("Gyroscope")]
        [InlineData("Low-Noise Accelerometer")]
        [InlineData("EXG")]
        [InlineData("ECG")]                 
        [InlineData("")]                    
        [InlineData("   ")]                 
        public void IsSplitVariantLabel_false_cases(string label)
        {
            Assert.False(Invoke_IsSplitVariantLabel(label));
        }


        /// <summary>
        /// Null labels are not split variants.
        /// Expected: returns false.
        /// </summary>
        [Fact(DisplayName = "IsSplitVariantLabel: null -> false")]
        public void IsSplitVariantLabel_null_is_false()
        {
            Assert.False(Invoke_IsSplitVariantLabel(null));
        }


        // ----- IsMultiChart behavior -----


        /// <summary>
        /// Helper: to invoke the private static <c>IsMultiChart(string)</c>.
        /// </summary>
        /// <param name="param">Label/parameter to check.</param>
        /// <returns><c>true</c> if it is a multi-series group; otherwise <c>false</c>.</returns>
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


        /// <summary>
        /// Recognizes multi-series groups (IMU + EXG families).
        /// Expected: true for IMU groups ("Low-Noise Accelerometer", "Gyroscope", ...),
        /// and EXG groups ("ECG", "EMG", "EXG Test", "Respiration", "EXG").
        /// </summary>
        /// <param name="group">Group label.</param>
        [Theory(DisplayName = "IsMultiChart: recognizes multi-series groups (IMU + EXG)")]
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


        /// <summary>
        /// Same recognition must hold even when UI adornments (arrow/split) are present.
        /// Expected: true for split/adorned variants of multi-series groups.
        /// </summary>
        /// <param name="labeled">Adorned label.</param>
        [Theory(DisplayName = "IsMultiChart: true even with UI adornments (arrow/split)")]
        [InlineData("    → Gyroscope — separate charts (X·Y·Z)")]
        [InlineData("    → ECG — separate charts (EXG1·EXG2)")]
        [InlineData("Magnetometer - separate charts (X·Y·Z)")]
        public void IsMultiChart_true_with_adornments(string labeled)
        {
            Assert.True(Invoke_IsMultiChart(labeled));
        }


        /// <summary>
        /// Must be false for single-channel names and non-group parameters,
        /// as well as empty/whitespace strings.
        /// Expected: false for EXG1/Exg2/single IMU channels/single params and ""/"   ".
        /// </summary>
        /// <param name="name">Candidate parameter name.</param>
        [Theory(DisplayName = "IsMultiChart: false for single channels and non-group parameters")]
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
        [InlineData("")]           // Empty string
        [InlineData("   ")]        // Spaces only
        public void IsMultiChart_false_singles_and_others(string name)
        {
            Assert.False(Invoke_IsMultiChart(name));
        }


        // ----- GetSubParameters behavior -----


        /// <summary>
        /// Gyroscope group must map to the XYZ channel list.
        /// Expected: ["GyroscopeX", "GyroscopeY", "GyroscopeZ"].
        /// </summary>
        [Fact(DisplayName = "GetSubParameters: gyroscope returns X,Y,Z")]
        public void GetSubParameters_returns_xyz_for_gyroscope()
        {
            var list = DataPageViewModel.GetSubParameters("Gyroscope");
            Assert.Equal(new[] { "GyroscopeX", "GyroscopeY", "GyroscopeZ" }, list);
        }


        /// <summary>
        /// EXG groups must map to ["Exg1", "Exg2"].
        /// Expected: ["Exg1", "Exg2"].
        /// </summary>
        [Fact(DisplayName = "GetSubParameters: EXG groups return Exg1, Exg2")]
        public void GetSubParameters_returns_exg1_exg2_for_exg_groups()
        {
            var list = DataPageViewModel.GetSubParameters("ECG");
            Assert.Equal(new[] { "Exg1", "Exg2" }, list);
        }


        /// <summary>
        /// Unknown groups should return an empty list.
        /// Expected: empty list.
        /// </summary>
        [Fact(DisplayName = "GetSubParameters: unknown group -> empty list")]
        public void GetSubParameters_empty_for_unknown_group()
        {
            var list = DataPageViewModel.GetSubParameters("SomethingElse");
            Assert.Empty(list);
        }


        // ----- GetLegendLabel behavior -----


        /// <summary>
        /// Returns user-friendly legend label for given group/sub-parameter pair.
        /// Expected:
        /// - ("Gyroscope","GyroscopeX") → "X", etc.
        /// - ("ECG","Exg1") → "EXG1"
        /// - ("EMG","Exg2") → "EXG2"
        /// - ("BatteryVoltage","BatteryVoltage") → "BatteryVoltage".
        /// </summary>
        /// <param name="group">Group label (may be adorned).</param>
        /// <param name="sub">Sub-parameter key.</param>
        /// <param name="expected">Expected legend label.</param>
        [Theory(DisplayName = "GetLegendLabel: returns compact/readable label")]
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


        /// <summary>
        /// Even with UI adornments, axis letters must be recognized from the sub-parameter.
        /// Expected: group "    → Gyroscope — separate charts (X·Y·Z)" with sub "GyroscopeY" → "Y".
        /// </summary>
        [Fact(DisplayName = "GetLegendLabel: adorned group label -> still compresses to axis letter")]
        public void GetLegendLabel_group_with_adornments_returns_axis_letter()
        {
            var groupWithUi = "    → Gyroscope — separate charts (X·Y·Z)";
            var label = DataPageViewModel.GetLegendLabel(groupWithUi, "GyroscopeY");
            Assert.Equal("Y", label);
        }


        // ----- GetCurrentSubParameters behavior -----


        /// <summary>
        /// For a single parameter selection, returns a single cleaned name.
        /// Expected: ["BatteryVoltage"].
        /// </summary>
        [Fact(DisplayName = "GetCurrentSubParameters: single parameter -> single-item (cleaned name)")]
        public void GetCurrentSubParameters_single_returns_single_clean_name()
        {
            var vm = NewVm();
            vm.SelectedParameter = "BatteryVoltage";   // single
            var subs = vm.GetCurrentSubParameters();

            Assert.Single(subs);
            Assert.Equal("BatteryVoltage", subs[0]);
        }


        /// <summary>
        /// For an IMU group (Gyroscope), returns X/Y/Z sub-parameters.
        /// Expected: contains GyroscopeX/Y/Z (count==3).
        /// </summary>
        [Fact(DisplayName = "GetCurrentSubParameters: IMU group (Gyroscope) -> X, Y, Z")]
        public void GetCurrentSubParameters_group_imu_returns_xyz()
        {
            var vm = NewVm();
            vm.SelectedParameter = "Gyroscope";        

            var subs = vm.GetCurrentSubParameters();

            Assert.Equal(3, subs.Count);
            Assert.Contains("GyroscopeX", subs);
            Assert.Contains("GyroscopeY", subs);
            Assert.Contains("GyroscopeZ", subs);
        }


        /// <summary>
        /// For an EXG group with UI adornments, returns Exg1/Exg2 after cleaning.
        /// Expected: ["Exg1","Exg2"].
        /// </summary>
        [Fact(DisplayName = "GetCurrentSubParameters: EXG group with UI adornments -> Exg1, Exg2")]
        public void GetCurrentSubParameters_exg_group_with_ui_adornments_returns_exg1_exg2()
        {
            var vm = NewVm();
            vm.SelectedParameter = "    → ECG — separate charts (EXG1·EXG2)";

            var subs = vm.GetCurrentSubParameters();

            Assert.Equal(2, subs.Count);
            Assert.Contains("Exg1", subs);
            Assert.Contains("Exg2", subs);
        }


        /// <summary>
        /// Trims spaces for single parameter selection; the cleaned single name is returned.
        /// Expected: ["BatteryVoltage"] even when SelectedParameter has surrounding spaces.
        /// </summary>
        [Fact(DisplayName = "GetCurrentSubParameters: trims spaces for single selection")]
        public void GetCurrentSubParameters_trims_spaces_when_single()
        {
            var vm = NewVm();
            vm.SelectedParameter = "   BatteryVoltage   ";

            var subs = vm.GetCurrentSubParameters();

            Assert.Single(subs);
            Assert.Equal("BatteryVoltage", subs[0]);
        }


        // ----- TryGetNumeric behavior -----


        /// <summary>
        /// Helper: Invokes the private static <c>TryGetNumeric</c> helper and returns both success and value.
        /// </summary>
        /// <param name="sample">Sample object; may be null.</param>
        /// <param name="field">Field/property name to extract from <paramref name="sample"/>.</param>
        /// <param name="val">Out: parsed numeric value as float on success (or 0 on failure).</param>
        /// <returns><c>true</c> if a numeric value was extracted; otherwise <c>false</c>.</returns>
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


        /// <summary>
        /// Wrapper with a numeric <c>Data</c> property.
        /// </summary>
        private sealed class WrapperNum
        {
            public double Data { get; set; }
        }


        /// <summary>
        /// Wrapper with an object <c>Data</c> property (may be null/non-numeric).
        /// </summary>
        private sealed class WrapperObj
        {
            public object? Data { get; set; }
        }


        /// <summary>
        /// Sample with various property shapes for testing.
        /// </summary>
        private sealed class NumSample
        {
            public int A { get; set; }
            public object? B { get; set; }
            public WrapperNum? C { get; set; }
            public WrapperObj? D { get; set; }
        }


        /// <summary>
        /// Primitive int field/property should be read as float successfully.
        /// Expected: true, value == 42f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: primitive int field -> true and correct value")]
        public void TryGetNumeric_int_field_returns_true_and_value()
        {
            var s = new NumSample { A = 42 };
            var ok = InvokeTryGetNumeric(s, "A", out var v);
            Assert.True(ok);
            Assert.Equal(42f, v);
        }


        /// <summary>
        /// Boxed double inside an object property should parse successfully.
        /// Expected: true, value == 12.5f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: double boxed in object -> true and correct value")]
        public void TryGetNumeric_double_in_object_returns_true_and_value()
        {
            var s = new NumSample { B = 12.5d };
            var ok = InvokeTryGetNumeric(s, "B", out var v);
            Assert.True(ok);
            Assert.Equal(12.5f, v, 3);
        }


        /// <summary>
        /// Wrapper with numeric <c>.Data</c> should parse successfully.
        /// Expected: true, value ≈ 3.14159f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: wrapper with numeric .Data -> true and correct value")]
        public void TryGetNumeric_wrapper_with_numeric_Data_returns_true()
        {
            var s = new NumSample { C = new WrapperNum { Data = 3.14159 } };
            var ok = InvokeTryGetNumeric(s, "C", out var v);
            Assert.True(ok);
            Assert.Equal(3.14159f, v, 4);
        }


        /// <summary>
        /// Wrapper with <c>.Data = null</c> should fail parsing.
        /// Expected: false; out value == 0f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: wrapper with .Data = null -> false")]
        public void TryGetNumeric_wrapper_with_null_Data_returns_false()
        {
            var s = new NumSample { D = new WrapperObj { Data = null } };
            var ok = InvokeTryGetNumeric(s, "D", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }


        /// <summary>
        /// Non-existent member should fail parsing.
        /// Expected: false; out value == 0f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: missing field/property -> false")]
        public void TryGetNumeric_missing_field_returns_false()
        {
            var s = new NumSample { A = 7 };
            var ok = InvokeTryGetNumeric(s, "Z", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }


        /// <summary>
        /// Non-numeric string field should fail parsing.
        /// Expected: false; out value == 0f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: non-numeric string -> false")]
        public void TryGetNumeric_non_numeric_string_returns_false()
        {
            var s = new NumSample { B = "ciao" };
            var ok = InvokeTryGetNumeric(s, "B", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }


        /// <summary>
        /// Null sample should fail parsing.
        /// Expected: false; out value == 0f.
        /// </summary>
        [Fact(DisplayName = "TryGetNumeric: null sample -> false")]
        public void TryGetNumeric_null_sample_returns_false()
        {
            var ok = InvokeTryGetNumeric(null, "Qualsiasi", out var v);
            Assert.False(ok);
            Assert.Equal(0f, v);
        }


        // ----- HasProp behavior -----


        // Test support types


        /// <summary>
        /// Test type exposing a public property <c>Foo</c>.
        /// </summary>
        private sealed class WithProp
        {
            public int Foo { get; set; }
        }


        /// <summary>
        /// Test type exposing a public field <c>Bar</c>.
        /// </summary>
        private sealed class WithField
        {
            public int Bar = 42;
        }


        /// <summary>
        /// Test type exposing both a property and a field.
        /// </summary>
        private sealed class WithBoth
        {
            public int Foo { get; set; }
            public string Baz = "x";
        }


        /// <summary>
        /// Helper: that invokes the private static <c>HasProp(obj, name)</c>.
        /// </summary>
        /// <param name="obj">Object to inspect (may be null).</param>
        /// <param name="name">Member name (case-sensitive).</param>
        /// <returns><c>true</c> if a property or field exists; otherwise <c>false</c>.</returns>
        private static bool Call_HasProp(object? obj, string name)
        {
            var mi = typeof(DataPageViewModel).GetMethod(
                "HasProp", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            return (bool)mi!.Invoke(null, new object?[] { obj!, name })!;
        }


        /// <summary>
        /// Should be true when a public property exists.
        /// Expected: <c>HasProp(o,"Foo")</c> → true.
        /// </summary>
        [Fact(DisplayName = "HasProp: true when a public property exists")]
        public void HasProp_true_on_public_property()
        {
            var o = new WithProp { Foo = 1 };
            Assert.True(Call_HasProp(o, "Foo"));
        }


        /// <summary>
        /// Should be true when a public field exists.
        /// Expected: <c>HasProp(o,"Bar")</c> → true.
        /// </summary>
        [Fact(DisplayName = "HasProp: true when a public field exists")]
        public void HasProp_true_on_public_field()
        {
            var o = new WithField();
            Assert.True(Call_HasProp(o, "Bar"));
        }


        /// <summary>
        /// Should be false when the member does not exist.
        /// Expected: <c>HasProp(o,"DoesNotExist")</c> → false.
        /// </summary>
        [Fact(DisplayName = "HasProp: false when the member does not exist")]
        public void HasProp_false_when_missing()
        {
            var o = new WithBoth();
            Assert.False(Call_HasProp(o, "DoesNotExist"));
        }


        /// <summary>
        /// Should be false when the object is null.
        /// Expected: <c>HasProp(null,"Foo")</c> → false.
        /// </summary>
        [Fact(DisplayName = "HasProp: false when the object is null")]
        public void HasProp_false_on_null_object()
        {
            Assert.False(Call_HasProp(null!, "Foo"));
        }


        /// <summary>
        /// Lookup is case-sensitive by default.
        /// Expected: <c>HasProp(o,"foo")</c> → false when only "Foo" exists.
        /// </summary>
        [Fact(DisplayName = "HasProp: case-sensitive lookup (mismatched case -> false)")]
        public void HasProp_case_sensitive()
        {
            var o = new WithProp();
            Assert.False(Call_HasProp(o, "foo"));
        }


        // ----- UpdateYAxisSettings behavior -----


        /// <summary>
        /// Helper: Creates an instance of <see cref="DataPageViewModel"/> without invoking its constructor.
        /// Useful to isolate private methods without side effects from constructors.
        /// </summary>
        /// <returns>Uninitialized instance of <see cref="DataPageViewModel"/>.</returns>
        private static object CreateVMWithoutCtor() =>
            FormatterServices.GetUninitializedObject(typeof(DataPageViewModel));


        /// <summary>
        /// Helper: Invokes a private instance method by name on <paramref name="instance"/> with optional <paramref name="args"/>.
        /// </summary>
        /// <param name="instance">Target instance.</param>
        /// <param name="methodName">Private method name.</param>
        /// <param name="args">Method arguments.</param>
        private static void CallPrivate(object instance, string methodName, params object[] args)
        {
            var mi = instance.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi);
            mi!.Invoke(instance, args);
        }


        /// <summary>
        /// Helpers: Reads a property (public or non-public) by name and returns its value cast to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Expected property type.</typeparam>
        /// <param name="instance">Target instance.</param>
        /// <param name="propName">Property name.</param>
        /// <returns>Property value.</returns>
        private static T GetProp<T>(object instance, string propName)
        {
            var pi = instance.GetType().GetProperty(propName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(pi);
            return (T)(pi!.GetValue(instance)!);
        }


        /// <summary>
        /// Verifies that <c>UpdateYAxisSettings</c> sets Y-axis label, unit, chart title, and default bounds
        /// for both grouped and single-channel parameters.
        /// Expected: For each inline row, YAxisLabel/Unit/Title/Min/Max match the provided tuple.
        /// </summary>
        /// <param name="input">The parameter name (group or single channel) to apply.</param>
        /// <param name="expLabel">Expected Y-axis label.</param>
        /// <param name="expUnit">Expected Y-axis unit.</param>
        /// <param name="expTitle">Expected chart title.</param>
        /// <param name="expMin">Expected default Y-axis minimum.</param>
        /// <param name="expMax">Expected default Y-axis maximum.</param>
        [Theory(DisplayName = "UpdateYAxisSettings: sets label/unit/title and default bounds")]
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
            var vm = CreateVMWithoutCtor();

            // Initialize properties read/written by the method
            SetProp(vm, "YAxisLabel", string.Empty);
            SetProp(vm, "YAxisUnit", string.Empty);
            SetProp(vm, "ChartTitle", string.Empty);
            SetProp(vm, "YAxisMin", 0d);
            SetProp(vm, "YAxisMax", 0d);

            CallPrivate(vm, "UpdateYAxisSettings", input);

            Assert.Equal(expLabel, GetProp<string>(vm, "YAxisLabel"));
            Assert.Equal(expUnit, GetProp<string>(vm, "YAxisUnit"));
            Assert.Equal(expTitle, GetProp<string>(vm, "ChartTitle"));
            Assert.Equal(expMin, Math.Round(GetProp<double>(vm, "YAxisMin"), 6));
            Assert.Equal(expMax, Math.Round(GetProp<double>(vm, "YAxisMax"), 6));
        }


        /// <summary>
        /// Ensures <c>UpdateYAxisSettings</c> respects <c>CleanParameterName</c> (leading/trailing whitespace only).
        /// Expected: " GyroscopeZ " → label "Gyroscope Z" with [-250, 250];
        /// "\tMagnetometerX\n" → label "Magnetometer X" with [-5, 5].
        /// </summary>
        /// <param name="noisy">Noisy input containing leading/trailing whitespace.</param>
        /// <param name="expLabel">Expected clean Y-axis label.</param>
        /// <param name="expMin">Expected Y-axis minimum.</param>
        /// <param name="expMax">Expected Y-axis maximum.</param>
        [Theory(DisplayName = "UpdateYAxisSettings: respects CleanParameterName trimming")]
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


        // ----- TryParseDouble behavior -----


        /// <summary>
        /// Validates <c>TryParseDouble</c> for common inputs, accepting both '.' and ',' decimals,
        /// along with optional sign and temporary editing states.
        /// Expected: Success flag equals <paramref name="ok"/>; when true, parsed value equals <paramref name="value"/> within tolerance.
        /// </summary>
        /// <param name="input">User text to parse.</param>
        /// <param name="ok">Expected success flag.</param>
        /// <param name="value">Expected numeric result when parsing succeeds.</param>
        [Theory(DisplayName = "TryParseDouble: common cases with dot/comma and signs")]
        [InlineData("12.5", true, 12.5)]
        [InlineData("12,5", true, 12.5)]
        [InlineData("-3.25", true, -3.25)]
        [InlineData("+7", true, 7.0)]
        [InlineData("+", true, 0.0)]      // temporary editing state accepted
        [InlineData("abc", false, 0.0)]
        [InlineData("7x", false, 0.0)]
        public void TryParseDouble_various_inputs(string input, bool ok, double value)
        {
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


        /// <summary>
        /// Covers edge cases for <c>TryParseDouble</c>, including trimming, leading separators, invalid formats,
        /// and explicit failure scenarios.
        /// Expected: Success and value behave as specified in each inline row (value checked only when success is true).
        /// </summary>
        /// <param name="input">User text to parse.</param>
        /// <param name="ok">Expected success flag.</param>
        /// <param name="value">Expected numeric result when success is true.</param>
        [Theory(DisplayName = "TryParseDouble: edge cases (trimming, multiple separators, invalid forms)")]
        // Valid
        [InlineData(" .5 ", true, 0.5)]          
        [InlineData(",5", true, 0.5)]            
        [InlineData("1.234", true, 1.234)]       
        [InlineData("1,234", true, 1.234)]       
        [InlineData("-0.0", true, -0.0)]         
        [InlineData("+ ", true, 0.0)]            
        [InlineData("- ", true, 0.0)]           

        // Invalid
        [InlineData("", false, 0.0)]             
        [InlineData("   ", false, 0.0)]          
        [InlineData("- 0.1", false, 0.0)]        
        [InlineData("1-2", false, 0.0)]          
        [InlineData("1.2.3", false, 0.0)]        
        [InlineData("1,2,3", false, 0.0)]        
        [InlineData("++1", false, 0.0)]          
        [InlineData("NaN", false, 0.0)]          
        public void TryParseDouble_edge_cases(string input, bool ok, double value)
        {
            var prev = CultureInfo.CurrentCulture;

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


        // ----- TryParseInt behavior -----


        /// <summary>
        /// Validates <c>TryParseInt</c> for common integer inputs with optional sign and whitespace.
        /// Expected: Success equals <paramref name="ok"/>; when true, parsed integer equals <paramref name="value"/>.
        /// </summary>
        /// <param name="input">User text to parse as integer.</param>
        /// <param name="ok">Expected success flag.</param>
        /// <param name="value">Expected integer when parsing succeeds.</param>
        [Theory(DisplayName = "TryParseInt: common cases (+/-, trimming)")]
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


        /// <summary>
        /// Covers edge cases for <c>TryParseInt</c> such as leading zeros, sign-only, internal spaces,
        /// thousand separators, underscore, and hex-like prefixes.
        /// Expected: Success and parsed value match the inline specification.
        /// </summary>
        /// <param name="input">User text to parse as integer.</param>
        /// <param name="ok">Expected success flag.</param>
        /// <param name="value">Expected integer when parsing succeeds.</param>
        [Theory(DisplayName = "TryParseInt: edge cases (leading zeros, separators, invalid forms)")]
        // Valid
        [InlineData("0", true, 0)]
        [InlineData("+0", true, 0)]
        [InlineData("-0", true, 0)]
        [InlineData("007", true, 7)]
        [InlineData("  -0012 ", true, -12)]

        // Invalid
        [InlineData("", false, 0)]   
        [InlineData("   ", false, 0)]   
        [InlineData("+", false, 0)]   
        [InlineData("-", false, 0)]   
        [InlineData("+ 1", false, 0)]   
        [InlineData("1 2", false, 0)]   
        [InlineData("1-2", false, 0)]   
        [InlineData("++1", false, 0)]   
        [InlineData("1,000", false, 0)]   
        [InlineData("1_000", false, 0)]   
        [InlineData("0x10", false, 0)]
        public void TryParseInt_edge_cases(string input, bool ok, int value)
        {
            var success = DataPageViewModel.TryParseInt(input, out var v);
            Assert.Equal(ok, success);
            if (ok)
                Assert.Equal(value, v);
        }


        // GetCurrentSensorConfiguration behavior


        /// <summary>
        /// Helper: Creates an uninitialized instance of type T (constructor is skipped).
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <returns>Uninitialized instance of <typeparamref name="T"/>.</returns>
        private static T CreateViewModel<T>() =>
            (T)FormatterServices.GetUninitializedObject(typeof(T));


        /// <summary>
        /// Helper: Shorthand factory that creates a <see cref="DataPageViewModel"/> without running its constructor.
        /// </summary>
        /// <returns>Uninitialized <see cref="DataPageViewModel"/> instance.</returns>
        private static DataPageViewModel CreateViewModel() => CreateViewModel<DataPageViewModel>();


        /// <summary>
        /// Helper: Sets a boolean value on a property or field (public or private) of the target object.
        /// </summary>
        /// <param name="obj">Target object.</param>
        /// <param name="name">Name of the property or field.</param>
        /// <param name="value">Value to assign.</param>
        /// <exception cref="MissingMemberException">Thrown when no bool property/field with that name exists.</exception>
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


        /// <summary>
        /// Helper: Sets the same boolean value on all the private sensor flags of the ViewModel.
        /// </summary>
        /// <param name="vm">ViewModel instance (can be uninitialized).</param>
        /// <param name="v">Boolean value to apply to all flags.</param>
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


        /// <summary>
        /// Helper: Sets EXG flags (global enable and per-mode flags) on the ViewModel.
        /// </summary>
        /// <param name="vm">The <see cref="DataPageViewModel"/> instance.</param>
        /// <param name="enableExg">Global EXG enable flag.</param>
        /// <param name="ecg">ECG mode flag.</param>
        /// <param name="emg">EMG mode flag.</param>
        /// <param name="test">Test mode flag.</param>
        /// <param name="resp">Respiration mode flag.</param>
        private static void SetExgPriv(DataPageViewModel vm, bool enableExg, bool ecg, bool emg, bool test, bool resp)
        {
            SetBool(vm, "enableExg", enableExg);
            SetBool(vm, "exgModeECG", ecg);
            SetBool(vm, "exgModeEMG", emg);
            SetBool(vm, "exgModeTest", test);
            SetBool(vm, "exgModeRespiration", resp);
        }


        /// <summary>
        /// Verifies that with all sensor and EXG flags disabled, the snapshot contains only false values.
        /// Expected: Every flag in the result is <c>false</c>.
        /// </summary>
        [Fact(DisplayName = "GetCurrentSensorConfiguration: all flags disabled -> all false")]
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


        /// <summary>
        /// Verifies mutual exclusivity of EXG modes when EXG is enabled with exactly one mode set.
        /// Expected: <c>EnableExg</c> is <c>true</c>; exactly one among ECG/EMG/Test/Respiration is <c>true</c> (sum == 1).
        /// </summary>
        /// <param name="ecg">Turns ECG mode on/off.</param>
        /// <param name="emg">Turns EMG mode on/off.</param>
        /// <param name="test">Turns Test mode on/off.</param>
        /// <param name="resp">Turns Respiration mode on/off.</param>
        [Theory(DisplayName = "GetCurrentSensorConfiguration: EXG enabled with a single mode -> exactly that mode is true")]
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

            int modes = (ecg ? 1 : 0) + (emg ? 1 : 0) + (test ? 1 : 0) + (resp ? 1 : 0);
            Assert.Equal(1, modes);
        }


        /// <summary>
        /// Verifies that when only EXG is enabled and ECG mode is selected, the snapshot copies EXG flags correctly.
        /// Expected: Only EXG flags reflect the VM (EnableExg=true, ECG=true) while all sensor flags remain <c>false</c>.
        /// </summary>
        [Fact(DisplayName = "GetCurrentSensorConfiguration: EXG only -> copies EXG flags, keeps sensors false")]
        public void GetCurrentSensorConfiguration_ExgOnly_copies_exg_flags()
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, false);
            SetExgPriv(vm, true, true, false, false, false);

            var snap = vm.GetCurrentSensorConfiguration();

            // Sensors remain false
            Assert.False(snap.EnableLowNoiseAccelerometer);
            Assert.False(snap.EnableWideRangeAccelerometer);
            Assert.False(snap.EnableGyroscope);
            Assert.False(snap.EnableMagnetometer);
            Assert.False(snap.EnablePressureTemperature);
            Assert.False(snap.EnableBattery);
            Assert.False(snap.EnableExtA6);
            Assert.False(snap.EnableExtA7);
            Assert.False(snap.EnableExtA15);

            // EXG flags copied
            Assert.True(snap.EnableExg);
            Assert.True(snap.IsExgModeECG);
            Assert.False(snap.IsExgModeEMG);
            Assert.False(snap.IsExgModeTest);
            Assert.False(snap.IsExgModeRespiration);
        }


        /// <summary>
        /// Verifies that a mixed sensor configuration and a specific EXG mode (EMG) are reflected in the snapshot.
        /// Expected: Each flag in the snapshot matches the VM flags one-to-one.
        /// </summary>
        [Fact(DisplayName = "GetCurrentSensorConfiguration: mixed pattern -> snapshot matches VM flags")]
        public void GetCurrentSensorConfiguration_MixedPattern_matches_vm_flags()
        {
            var vm = CreateViewModel();

            // Mixed sensors
            SetBool(vm, "enableLowNoiseAccelerometer", true);
            SetBool(vm, "enableWideRangeAccelerometer", false);
            SetBool(vm, "enableGyroscope", true);
            SetBool(vm, "enableMagnetometer", false);
            SetBool(vm, "enablePressureTemperature", true);
            SetBool(vm, "enableBattery", false);
            SetBool(vm, "enableExtA6", true);
            SetBool(vm, "enableExtA7", false);
            SetBool(vm, "enableExtA15", true);

            // EXG: enabled, EMG only
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


        /// <summary>
        /// Verifies that each call returns a new snapshot instance (no reference reuse) while values remain equal.
        /// Expected: Two different instances (NotSame), with all corresponding flags equal.
        /// </summary>
        [Fact(DisplayName = "GetCurrentSensorConfiguration: returns a new instance each call (values equal)")]
        public void GetCurrentSensorConfiguration_returns_new_instance_each_call()
        {
            var vm = CreateViewModel();
            SetAllSensorsPriv(vm, true);
            SetExgPriv(vm, true, true, false, false, false);

            var first = vm.GetCurrentSensorConfiguration();
            var second = vm.GetCurrentSensorConfiguration();

            Assert.NotSame(first, second);

            // Flags equal
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


        // ----- ResetAllTimestamps behavior -----


        /// <summary>
        /// Helper: Ensures the VM reflects a desired sampling rate by attempting multiple
        /// assignment paths (text, display, backing fields), then returns the
        /// effective device sampling rate actually used by the VM.
        /// </summary>
        /// <param name="vm">The uninitialized or constructed view model instance.</param>
        /// <param name="desiredHz">The requested sampling rate in Hertz.</param>
        /// <returns>The effective sampling rate (Hz) as reported by the VM.</returns>
        private static double EnsureDeviceSamplingRate(object vm, double desiredHz)
        {
            var t = vm.GetType();

            // Try multiple "entry points"
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

            // Read effective rate
            var pEff = t.GetProperty("DeviceSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pEff == null)
                throw new MissingMemberException("DeviceSamplingRate non trovato.");

            return (double)pEff.GetValue(vm)!;
        }


        /// <summary>
        /// Helper: Sets the value of a property or field by name, supporting public and non-public members.
        /// Throws if neither a settable property nor a field is found.
        /// </summary>
        /// <param name="obj">The target object.</param>
        /// <param name="name">The property or field name.</param>
        /// <param name="value">The value to set.</param>
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

            throw new MissingMemberException($"{t.Name} has no member '{name}'.");
        }


        /// <summary>
        /// Helper: Creates a <c>Dictionary&lt;string, List&lt;T&gt;&gt;</c> compatible with the VM's concrete
        /// <c>timeStampsCollections</c> type, assigns it to the VM, and returns both the dictionary
        /// instance and the element type <c>T</c>.
        /// </summary>
        /// <param name="vm">The view model instance.</param>
        /// <returns>A tuple containing the created dictionary and the list element type.</returns>
        private static (object dict, Type elementType) InitTimeStampDict(object vm)
        {
            var type = vm.GetType();
            MemberInfo? member =
                type.GetField("timeStampsCollections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? (MemberInfo?)type.GetProperty("timeStampsCollections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (member == null) throw new MissingMemberException("timeStampsCollections not found.");

            var memberType = member is FieldInfo fi ? fi.FieldType : ((PropertyInfo)member).PropertyType;

            if (!memberType.IsGenericType) throw new InvalidOperationException("timeStampsCollections it's not generic.");
            var args = memberType.GetGenericArguments(); // [TKey, TValue]
            var keyType = args[0];
            var valueType = args[1];  // expected List<X>

            if (keyType != typeof(string)) throw new InvalidOperationException("timeStampsCollections must have string key.");
            if (!valueType.IsGenericType || valueType.GetGenericTypeDefinition() != typeof(List<>))
                throw new InvalidOperationException("timeStampsCollections must be List<> as value.");

            var elemType = valueType.GetGenericArguments()[0]; // X

            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dictInstance = Activator.CreateInstance(dictType)!;

            SetRef(vm, "timeStampsCollections", dictInstance);
            return (dictInstance, elemType);
        }


        /// <summary>
        /// Helper: Creates a <c>List&lt;T&gt;</c> of the specified element type and populates it with the provided items.
        /// </summary>
        /// <param name="elemType">The list element type <c>T</c>.</param>
        /// <param name="items">Items to be added (converted to <c>T</c>).</param>
        /// <returns>A new <c>List&lt;T&gt;</c> instance.</returns>
        private static object MakeListOf(Type elemType, params object[] items)
        {
            var listType = typeof(List<>).MakeGenericType(elemType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            foreach (var it in items)
                list.Add(Convert.ChangeType(it, elemType));
            return list;
        }


        /// <summary>
        /// Helper: Adds a series to the dictionary using either an <c>Add(key, value)</c> method or the indexer as a fallback.
        /// </summary>
        /// <param name="dict">The dictionary instance (string → List&lt;T&gt;).</param>
        /// <param name="key">Series key.</param>
        /// <param name="list">Series list instance.</param>
        private static void AddSeries(object dict, string key, object list)
        {

            // Try Add(key, value)
            var add = dict.GetType().GetMethod("Add", new[] { typeof(string), list.GetType() });
            if (add != null)
            {
                add.Invoke(dict, new object[] { key, list });
                return;
            }

            // fallback: dict[key] = value via indexer
            var itemProp = dict.GetType().GetProperty("Item");
            if (itemProp == null) throw new MissingMemberException("Indexer 'Item' not found in dictionary.");
            itemProp.SetValue(dict, list, new object[] { key });
        }


        /// <summary>
        /// Helper: Asserts that a generic numeric list matches the expected sequence within a tolerance sensible for its element type.
        /// Supports element types <c>int</c>, <c>float</c>, and <c>double</c>.
        /// </summary>
        /// <param name="listObj">The list instance.</param>
        /// <param name="expected">The expected numeric values.</param>
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
                    throw new InvalidOperationException($"Unexpected item type: {elemType}");
                }
            }
        }


        /// <summary>
        /// Helper: Reads the effective device sampling rate from the VM.
        /// </summary>
        /// <param name="vm">The view model instance.</param>
        /// <returns>The effective rate (Hz).</returns>
        private static double GetEffectiveRate(object vm)
        {
            var pEff = vm.GetType().GetProperty("DeviceSamplingRate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (double)pEff!.GetValue(vm)!;
        }


        /// <summary>
        /// Helper: Builds the expected integer millisecond timestamps for a given count and effective rate,
        /// using the same formula as production: <c>(int)(i * (1000.0 / effRate))</c>.
        /// </summary>
        /// <param name="count">Number of samples/points.</param>
        /// <param name="effRate">Effective sampling rate (Hz).</param>
        /// <returns>Array of expected millisecond timestamps.</returns>
        private static double[] ExpectedStamps(int count, double effRate)
        {
            var arr = new double[count];
            for (int i = 0; i < count; i++)
                arr[i] = (int)(i * (1000.0 / effRate)); // Same formula as VM
            return arr;
        }


        /// <summary>
        /// Rebuilds timestamps for multiple series using the effective device sampling rate, producing evenly spaced ms values.
        /// Expected: For each series, values equal <c>(int)(i * (1000 / effRate))</c> computed from the VM's effective rate (e.g., 51.2 Hz).
        /// </summary>
        [Fact(DisplayName = "ResetAllTimestamps: even spacing for multiple series (uses effective rate)")]
        public void ResetAllTimestamps_builds_even_spacing_for_50Hz_on_multiple_series()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

            // Request 50 Hz; VM may clamp to 51.2 -> adapt to effective rate in assertions
            EnsureDeviceSamplingRate(vm, 50.0);
            var eff = GetEffectiveRate(vm);

            var (dict, elemType) = InitTimeStampDict(vm);
            AddSeries(dict, "Accel", MakeListOf(elemType, 999, 999, 999, 999));  // 4 elements
            AddSeries(dict, "Gyro", MakeListOf(elemType, 111, 111, 111));        // 3 elements

            vm.ResetAllTimestamps();

            var idx = dict.GetType().GetProperty("Item");
            var accelList = idx!.GetValue(dict, new object[] { "Accel" });
            var gyroList = idx!.GetValue(dict, new object[] { "Gyro" });

            AssertSeq(accelList!, ExpectedStamps(4, eff)); // e.g., 0, 19, 39, 58 for 51.2 Hz
            AssertSeq(gyroList!, ExpectedStamps(3, eff));  // e.g., 0, 19, 39 for 51.2 Hz
        }


        /// <summary>
        /// Confirms the algorithm uses the VM's effective sampling rate and integer casting identical to production.
        /// Expected: Timestamps are computed as <c>(int)(i * (1000 / effRate))</c> for the **effective** rate (not the requested one, if clamped).
        /// </summary>
        [Fact(DisplayName = "ResetAllTimestamps: uses effective sampling rate and int casting (production parity)")]
        public void ResetAllTimestamps_uses_sampling_rate_and_casts_to_int_like_code()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

            EnsureDeviceSamplingRate(vm, 7.5);  // VM may clamp to its supported rate
            var eff = GetEffectiveRate(vm);

            var (dict, elemType) = InitTimeStampDict(vm);
            AddSeries(dict, "Flux", MakeListOf(elemType, 0, 0, 0));

            vm.ResetAllTimestamps();

            var idx = dict.GetType().GetProperty("Item");
            var flux = idx!.GetValue(dict, new object[] { "Flux" });

            AssertSeq(flux!, ExpectedStamps(3, eff)); // e.g., 0, 19, 39 for 51.2 Hz
        }


        /// <summary>
        /// Ensures <c>ResetAllTimestamps</c> only rewrites values and never changes the series length.
        /// Expected: Each series retains its original count after the call.
        /// </summary>
        [Fact(DisplayName = "ResetAllTimestamps: preserves series lengths (values rewritten only)")]
        public void ResetAllTimestamps_keeps_lengths_unchanged()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());

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


        /// <summary>
        /// Calls <c>ResetAllTimestamps</c> on an empty dictionary and verifies it is a no-op with no exceptions.
        /// Expected: No exception thrown; dictionary remains empty before and after.
        /// </summary>
        [Fact]
        public void ResetAllTimestamps_empty_dictionary_no_throw_and_stays_empty()
        {
            var vm = CreateViewModel();
            SetRef(vm, "_dataLock", new object());
            EnsureDeviceSamplingRate(vm, 100.0);

            var (dict, _) = InitTimeStampDict(vm); // Do not add series

            var countBefore = (int)dict.GetType().GetProperty("Count")!.GetValue(dict)!;
            Assert.Equal(0, countBefore);          // Confirm empty

            // Should not throw
            vm.ResetAllTimestamps();

            var countAfter = (int)dict.GetType().GetProperty("Count")!.GetValue(dict)!;
            Assert.Equal(0, countAfter); // Still empty
        }
    }
}
