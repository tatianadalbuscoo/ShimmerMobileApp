/*
 * ShimmerSDK_IMU_SettingsTests.cs
 * Purpose: Unit tests for ShimmerSDK_IMUSettings file.
 */


using ShimmerSDK.IMU;
using Xunit;


namespace ShimmerSDKTests.IMUTests
{

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_IMU"/> (configuration partial):
    /// validates constructor defaults, property round-trips, and independence
    /// between boolean flags and sampling rate.
    /// </summary>
    public class ShimmerSDK_IMU_SettingsTests
    {

        // ----- Constructor behavior -----


        /// <summary>
        /// Ensures the parameterless constructor sets sensible defaults.
        /// Expected:
        /// - <c>SamplingRate</c> ≈ 51.2 Hz (within [51.199, 51.201])
        /// - All IMU-related sensor flags default to <c>true</c>:
        ///   LowNoiseAccelerometer, WideRangeAccelerometer, Gyroscope, Magnetometer,
        ///   Pressure/Temperature, Battery, ExtA6, ExtA7, ExtA15.
        /// </summary>
        [Fact]
        public void Ctor_Defaults_Are_Sensible()
        {
            var sut = new ShimmerSDK_IMU();

            // SamplingRate default ~51.2 Hz
            Assert.InRange(sut.SamplingRate, 51.199, 51.201);

            Assert.True(sut.EnableLowNoiseAccelerometer);
            Assert.True(sut.EnableWideRangeAccelerometer);
            Assert.True(sut.EnableGyroscope);
            Assert.True(sut.EnableMagnetometer);
            Assert.True(sut.EnablePressureTemperature);
            Assert.True(sut.EnableBattery);
            Assert.True(sut.EnableExtA6);
            Assert.True(sut.EnableExtA7);
            Assert.True(sut.EnableExtA15);
        }


        // ----- SamplingRate behavior -----


        /// <summary>
        /// Verifies a set/get round-trip for <c>SamplingRate</c> across representative values.
        /// Expected:
        /// - After assignment, the getter returns exactly the assigned value (high precision).
        /// - No side effects on other properties asserted here.
        /// </summary>
        [Theory]
        [InlineData(12.5)]
        [InlineData(51.2)]
        [InlineData(200.0)]
        [InlineData(256.0)]
        public void SamplingRate_RoundTrip(double value)
        {
            var sut = new ShimmerSDK_IMU();
            sut.SamplingRate = value;
            Assert.Equal(value, sut.SamplingRate, precision: 10);
        }


        // ----- Boolean flags behavior -----


        /// <summary>
        /// Helper: Provides (name, getter, setter, defaultValue) tuples for each boolean sensor flag
        /// to drive the parameterized test.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> of <see cref="object"/> arrays where each entry is:
        /// <c>{ string name, Func&lt;ShimmerSDK_IMU,bool&gt; getter, Action&lt;ShimmerSDK_IMU,bool&gt; setter, bool defaultValue }</c>.
        /// </returns>
        public static IEnumerable<object[]> BoolProps()
        {
            yield return new object[] { "EnableLowNoiseAccelerometer", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableLowNoiseAccelerometer), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableLowNoiseAccelerometer = v), true };
            yield return new object[] { "EnableWideRangeAccelerometer", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableWideRangeAccelerometer), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableWideRangeAccelerometer = v), true };
            yield return new object[] { "EnableGyroscope", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableGyroscope), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableGyroscope = v), true };
            yield return new object[] { "EnableMagnetometer", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableMagnetometer), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableMagnetometer = v), true };
            yield return new object[] { "EnablePressureTemperature", (Func<ShimmerSDK_IMU, bool>)(s => s.EnablePressureTemperature), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnablePressureTemperature = v), true };
            yield return new object[] { "EnableBattery", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableBattery), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableBattery = v), true };
            yield return new object[] { "EnableExtA6", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableExtA6), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableExtA6 = v), true };
            yield return new object[] { "EnableExtA7", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableExtA7), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableExtA7 = v), true };
            yield return new object[] { "EnableExtA15", (Func<ShimmerSDK_IMU, bool>)(s => s.EnableExtA15), (Action<ShimmerSDK_IMU, bool>)((s, v) => s.EnableExtA15 = v), true };
        }


        /// <summary>
        /// Validates default, toggle, and round-trip behavior for each boolean flag.
        /// Expected:
        /// - Getter returns the documented default.
        /// - After setting to the opposite value, getter reflects the change.
        /// - After setting back to the default, getter returns the original default.
        /// </summary>
        [Theory]
        [MemberData(nameof(BoolProps))]
        public void Bool_Property_Default_Then_Toggle_RoundTrip(
            string name,
            Func<ShimmerSDK_IMU, bool> getter,
            Action<ShimmerSDK_IMU, bool> setter,
            bool defaultValue)
        {
            var sut = new ShimmerSDK_IMU();

            Assert.Equal(defaultValue, getter(sut));
            setter(sut, !defaultValue);
            Assert.True(getter(sut) == !defaultValue, $"Property '{name}' did not toggle as expected.");
            setter(sut, defaultValue);
            Assert.True(getter(sut) == defaultValue, $"Property '{name}' did not round-trip as expected.");
        }


        // ----- Independence checks -----


        /// <summary>
        /// Changing one flag must not affect the others; <c>SamplingRate</c> must remain unchanged.
        /// Expected:
        /// - Only <c>EnableWideRangeAccelerometer</c> and <c>EnableBattery</c> change (the ones toggled).
        /// - All other flags remain identical to the initial snapshot.
        /// - <c>SamplingRate</c> stays ≈ 51.2 Hz (within [51.199, 51.201]).
        /// </summary>
        [Fact]
        public void Changing_One_Flag_Does_Not_Affect_Others()
        {
            var sut = new ShimmerSDK_IMU();

            // initial snapshot
            var before = SnapshotFlags(sut);

            // change ONLY WideRangeAccelerometer and Battery
            sut.EnableWideRangeAccelerometer = !before.EnableWideRangeAccelerometer;
            sut.EnableBattery = !before.EnableBattery;

            var after = SnapshotFlags(sut);

            // the modified ones should differ
            Assert.Equal(!before.EnableWideRangeAccelerometer, after.EnableWideRangeAccelerometer);
            Assert.Equal(!before.EnableBattery, after.EnableBattery);

            // all the others should be identical
            Assert.Equal(before.EnableLowNoiseAccelerometer, after.EnableLowNoiseAccelerometer);
            Assert.Equal(before.EnableGyroscope, after.EnableGyroscope);
            Assert.Equal(before.EnableMagnetometer, after.EnableMagnetometer);
            Assert.Equal(before.EnablePressureTemperature, after.EnablePressureTemperature);
            Assert.Equal(before.EnableExtA6, after.EnableExtA6);
            Assert.Equal(before.EnableExtA7, after.EnableExtA7);
            Assert.Equal(before.EnableExtA15, after.EnableExtA15);

            // SamplingRate should not change as a side effect of flag changes
            Assert.InRange(sut.SamplingRate, 51.199, 51.201);
        }


        /// <summary>
        /// Changing <c>SamplingRate</c> must not alter any sensor flags.
        /// Expected:
        /// - After setting <c>SamplingRate</c> to a different value, the complete flag snapshot is unchanged.
        /// </summary>
        [Fact]
        public void Changing_SamplingRate_Does_Not_Affect_Flags()
        {
            var sut = new ShimmerSDK_IMU();
            var before = SnapshotFlags(sut);

            sut.SamplingRate = 200.0;

            var after = SnapshotFlags(sut);
            Assert.Equal(before, after);
        }


        // ----- helpers -----


        /// <summary>
        /// Helper: Captures a snapshot of all boolean flags for equality comparison.
        /// </summary>
        /// <param name="s">The <see cref="ShimmerSDK_IMU"/> instance to snapshot.</param>
        /// <returns>
        /// An immutable <see cref="Flags"/> record containing the current values of all boolean flags.
        /// </returns>
        private static Flags SnapshotFlags(ShimmerSDK_IMU s) => new Flags(
            s.EnableLowNoiseAccelerometer,
            s.EnableWideRangeAccelerometer,
            s.EnableGyroscope,
            s.EnableMagnetometer,
            s.EnablePressureTemperature,
            s.EnableBattery,
            s.EnableExtA6,
            s.EnableExtA7,
            s.EnableExtA15
        );


        /// <summary>
        /// Helper: Immutable struct used to compare complete flag states.
        /// </summary>
        /// <param name="EnableLowNoiseAccelerometer">Current value of the LowNoiseAccelerometer flag.</param>
        /// <param name="EnableWideRangeAccelerometer">Current value of the WideRangeAccelerometer flag.</param>
        /// <param name="EnableGyroscope">Current value of the Gyroscope flag.</param>
        /// <param name="EnableMagnetometer">Current value of the Magnetometer flag.</param>
        /// <param name="EnablePressureTemperature">Current value of the Pressure/Temperature flag.</param>
        /// <param name="EnableBattery">Current value of the Battery flag.</param>
        /// <param name="EnableExtA6">Current value of the ExtA6 flag.</param>
        /// <param name="EnableExtA7">Current value of the ExtA7 flag.</param>
        /// <param name="EnableExtA15">Current value of the ExtA15 flag.</param>
        private readonly record struct Flags(
            bool EnableLowNoiseAccelerometer,
            bool EnableWideRangeAccelerometer,
            bool EnableGyroscope,
            bool EnableMagnetometer,
            bool EnablePressureTemperature,
            bool EnableBattery,
            bool EnableExtA6,
            bool EnableExtA7,
            bool EnableExtA15
        );
    }
}
