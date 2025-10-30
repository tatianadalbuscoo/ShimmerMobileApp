/*
 * ShimmerSDK_EXGSettingsTests.cs
 * Purpose: Unit tests for ShimmerSDK_EXGSettings file.
 */


using ShimmerSDK.EXG;
using Xunit;


namespace ShimmerSDKTests.EXGTests
{

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_EXG"/> (configuration partial):
    /// validates ctor defaults, property round-trips, EXG mode, and independence between flags and sampling rate.
    /// </summary>
    public class ShimmerSDK_EXG_SettingsTests
    {

        // ----- Constructor behavior -----


        /// <summary>
        /// Ensures the parameterless constructor sets sensible defaults.
        /// Expected:
        /// - SamplingRate ≈ 51.2 Hz (within [51.199, 51.201])
        /// - All sensor flags default to true: LNA, WRA, Gyro, Mag, Pressure/Temperature,
        ///   BatteryVoltage, ExtA6, ExtA7, ExtA15
        /// - EnableExg defaults to false
        /// - ExgMode defaults to ECG
        /// </summary>
        [Fact]
        public void Ctor_Defaults_Are_Sensible()
        {
            var sut = new ShimmerSDK_EXG();

            Assert.InRange(sut.SamplingRate, 51.199, 51.201);

            Assert.True(sut.EnableLowNoiseAccelerometer);
            Assert.True(sut.EnableWideRangeAccelerometer);
            Assert.True(sut.EnableGyroscope);
            Assert.True(sut.EnableMagnetometer);
            Assert.True(sut.EnablePressureTemperature);
            Assert.True(sut.EnableBatteryVoltage);
            Assert.True(sut.EnableExtA6);
            Assert.True(sut.EnableExtA7);
            Assert.True(sut.EnableExtA15);

            Assert.False(sut.EnableExg);
            Assert.Equal(ExgMode.ECG, sut.ExgMode);
        }


        // ----- SamplingRate behavior -----


        /// <summary>
        /// Verifies round-trip for SamplingRate across representative values.
        /// Expected:
        /// - After setting, getter returns exactly the assigned value (high precision)
        /// - No side effects on other properties asserted here
        /// </summary>
        [Theory]
        [InlineData(12.5)]
        [InlineData(51.2)]
        [InlineData(200.0)]
        [InlineData(256.0)]
        public void SamplingRate_RoundTrip(double value)
        {
            var sut = new ShimmerSDK_EXG();
            sut.SamplingRate = value;
            Assert.Equal(value, sut.SamplingRate, precision: 10);
        }


        // ----- Bool flags behavior -----


        /// <summary>
        /// Helper: Provides (name, getter, setter, defaultValue) tuples for each boolean sensor flag
        /// to drive the parameterized test.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of <see cref="object"/> arrays where each entry is:
        /// <c>{ string name, Func&lt;ShimmerSDK_EXG,bool&gt; getter, Action&lt;ShimmerSDK_EXG,bool&gt; setter, bool defaultValue }</c>.
        /// </returns>
        public static IEnumerable<object[]> BoolProps()
        {
            yield return new object[] { "EnableLowNoiseAccelerometer", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableLowNoiseAccelerometer), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableLowNoiseAccelerometer = v), true };
            yield return new object[] { "EnableWideRangeAccelerometer", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableWideRangeAccelerometer), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableWideRangeAccelerometer = v), true };
            yield return new object[] { "EnableGyroscope", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableGyroscope), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableGyroscope = v), true };
            yield return new object[] { "EnableMagnetometer", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableMagnetometer), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableMagnetometer = v), true };
            yield return new object[] { "EnablePressureTemperature", (Func<ShimmerSDK_EXG, bool>)(s => s.EnablePressureTemperature), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnablePressureTemperature = v), true };
            yield return new object[] { "EnableBatteryVoltage", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableBatteryVoltage), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableBatteryVoltage = v), true };
            yield return new object[] { "EnableExtA6", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableExtA6), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableExtA6 = v), true };
            yield return new object[] { "EnableExtA7", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableExtA7), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableExtA7 = v), true };
            yield return new object[] { "EnableExtA15", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableExtA15), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableExtA15 = v), true };
            yield return new object[] { "EnableExg", (Func<ShimmerSDK_EXG, bool>)(s => s.EnableExg), (Action<ShimmerSDK_EXG, bool>)((s, v) => s.EnableExg = v), false };
        }


        /// <summary>
        /// Validates default, toggle, and round-trip behavior for each boolean flag.
        /// Expected:
        /// - Getter returns the documented default
        /// - After setting to the opposite value, getter reflects the change
        /// - After setting back to the default, getter returns the original default
        /// </summary>
        [Theory]
        [MemberData(nameof(BoolProps))]
        public void Bool_Property_Default_Then_Toggle_RoundTrip(
            string name,
            Func<ShimmerSDK_EXG, bool> getter,
            Action<ShimmerSDK_EXG, bool> setter,
            bool defaultValue)
        {
            var sut = new ShimmerSDK_EXG();

            Assert.Equal(defaultValue, getter(sut));
            setter(sut, !defaultValue);
            Assert.True(getter(sut) == !defaultValue, $"Property '{name}' did not toggle as expected.");
            setter(sut, defaultValue);
            Assert.True(getter(sut) == defaultValue, $"Property '{name}' did not round-trip as expected.");
        }


        // ----- ExgMode behavior -----


        /// <summary>
        /// Verifies round-trip on all enum values for <c>ExgMode</c>.
        /// Expected:
        /// - After setting, <c>sut.ExgMode</c> equals the assigned value for each case
        /// </summary>
        [Theory]
        [InlineData(ExgMode.ECG)]
        [InlineData(ExgMode.EMG)]
        [InlineData(ExgMode.ExGTest)]
        [InlineData(ExgMode.Respiration)]
        public void ExgMode_RoundTrip(ExgMode mode)
        {
            var sut = new ShimmerSDK_EXG();
            sut.ExgMode = mode;
            Assert.Equal(mode, sut.ExgMode);
        }


        // ----- Independence checks -----


        /// <summary>
        /// Changing one flag must not affect the others; SamplingRate must remain unchanged.
        /// Expected:
        /// - Only EnableWideRangeAccelerometer and EnableExg change (the ones toggled)
        /// - All other flags remain identical to the initial snapshot
        /// - SamplingRate stays ≈ 51.2 Hz (within [51.199, 51.201])
        /// </summary>
        [Fact]
        public void Changing_One_Flag_Does_Not_Affect_Others()
        {
            var sut = new ShimmerSDK_EXG();

            // initial snapshot
            var before = SnapshotFlags(sut);

            // change ONLY WideRangeAccelerometer and EXG
            sut.EnableWideRangeAccelerometer = !before.EnableWideRangeAccelerometer;
            sut.EnableExg = !before.EnableExg;

            var after = SnapshotFlags(sut);

            // the modified ones should differ
            Assert.Equal(!before.EnableWideRangeAccelerometer, after.EnableWideRangeAccelerometer);
            Assert.Equal(!before.EnableExg, after.EnableExg);

            // all the others should be identical
            Assert.Equal(before.EnableLowNoiseAccelerometer, after.EnableLowNoiseAccelerometer);
            Assert.Equal(before.EnableGyroscope, after.EnableGyroscope);
            Assert.Equal(before.EnableMagnetometer, after.EnableMagnetometer);
            Assert.Equal(before.EnablePressureTemperature, after.EnablePressureTemperature);
            Assert.Equal(before.EnableBatteryVoltage, after.EnableBatteryVoltage);
            Assert.Equal(before.EnableExtA6, after.EnableExtA6);
            Assert.Equal(before.EnableExtA7, after.EnableExtA7);
            Assert.Equal(before.EnableExtA15, after.EnableExtA15);

            // SamplingRate should not change as a side effect of flag changes
            Assert.InRange(sut.SamplingRate, 51.199, 51.201);
        }


        /// <summary>
        /// Changing <c>SamplingRate</c> must not alter any sensor flags.
        /// Expected:
        /// - After setting SamplingRate to a different value, the complete flag snapshot is unchanged
        /// </summary>
        [Fact]
        public void Changing_SamplingRate_Does_Not_Affect_Flags()
        {
            var sut = new ShimmerSDK_EXG();
            var before = SnapshotFlags(sut);

            sut.SamplingRate = 200.0;

            var after = SnapshotFlags(sut);
            Assert.Equal(before, after);
        }


        // ----- helpers -----


        /// <summary>
        /// Helper: Captures a snapshot of all boolean flags for equality comparison.
        /// </summary>
        /// <param name="s">The <see cref="ShimmerSDK_EXG"/> instance to snapshot.</param>
        /// <returns>
        /// An immutable <see cref="Flags"/> record containing the current values of all boolean flags.
        /// </returns>
        private static Flags SnapshotFlags(ShimmerSDK_EXG s) => new Flags(
            s.EnableLowNoiseAccelerometer,
            s.EnableWideRangeAccelerometer,
            s.EnableGyroscope,
            s.EnableMagnetometer,
            s.EnablePressureTemperature,
            s.EnableBatteryVoltage,
            s.EnableExtA6,
            s.EnableExtA7,
            s.EnableExtA15,
            s.EnableExg
        );


        /// <summary>
        /// Helper: Immutable struct used to compare complete flag states.
        /// </summary>
        /// <param name="EnableLowNoiseAccelerometer">Current value of the LowNoiseAccelerometer flag.</param>
        /// <param name="EnableWideRangeAccelerometer">Current value of the WideRangeAccelerometer flag.</param>
        /// <param name="EnableGyroscope">Current value of the Gyroscope flag.</param>
        /// <param name="EnableMagnetometer">Current value of the Magnetometer flag.</param>
        /// <param name="EnablePressureTemperature">Current value of the Pressure/Temperature flag.</param>
        /// <param name="EnableBatteryVoltage">Current value of the BatteryVoltage flag.</param>
        /// <param name="EnableExtA6">Current value of the ExtA6 flag.</param>
        /// <param name="EnableExtA7">Current value of the ExtA7 flag.</param>
        /// <param name="EnableExtA15">Current value of the ExtA15 flag.</param>
        /// <param name="EnableExg">Current value of the Exg flag.</param>
        private readonly record struct Flags(
            bool EnableLowNoiseAccelerometer,
            bool EnableWideRangeAccelerometer,
            bool EnableGyroscope,
            bool EnableMagnetometer,
            bool EnablePressureTemperature,
            bool EnableBatteryVoltage,
            bool EnableExtA6,
            bool EnableExtA7,
            bool EnableExtA15,
            bool EnableExg
        );
    }
}
