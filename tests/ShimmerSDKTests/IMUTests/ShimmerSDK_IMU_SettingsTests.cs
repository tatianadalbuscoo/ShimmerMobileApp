using System;
using System.Collections.Generic;
using ShimmerSDK.IMU;
using Xunit;

namespace ShimmerSDKTests.IMUTests
{
    public class ShimmerSDK_IMU_SettingsTests
    {
        // Ctor() — behavior
        // Defaults from ctor (dall’altro partial della classe)
        [Fact]
        public void Ctor_Defaults_Are_Sensible()
        {
            var sut = new ShimmerSDK_IMU();

            // SamplingRate default ~51.2 Hz
            Assert.InRange(sut.SamplingRate, 51.199, 51.201);

            // Flag sensori default (come impostati nel costruttore dell’altra partial)
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

        // SamplingRate — behavior
        // set/get round-trip
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

        // Bool flags — behavior
        // Tutti i bool getter/setter: round-trip e verifica default
        public static IEnumerable<object[]> BoolProps()
        {
            // name, getter, setter, defaultValue
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

        // Bool flags — behavior
        // Ogni proprietà booleana: default poi toggle e round-trip (property: {name})
        [Theory]
        [MemberData(nameof(BoolProps))]
        public void Bool_Property_Default_Then_Toggle_RoundTrip(
            string name,
            Func<ShimmerSDK_IMU, bool> getter,
            Action<ShimmerSDK_IMU, bool> setter,
            bool defaultValue)
        {
            var sut = new ShimmerSDK_IMU();

            Assert.Equal(defaultValue, getter(sut)); // usa getter
            setter(sut, !defaultValue);
            Assert.True(getter(sut) == !defaultValue, $"Property '{name}' did not toggle as expected.");
            setter(sut, defaultValue);
            Assert.True(getter(sut) == defaultValue, $"Property '{name}' did not round-trip as expected.");
        }

        // Flags independence — behavior
        // Settare un flag non modifica gli altri
        [Fact]
        public void Changing_One_Flag_Does_Not_Affect_Others()
        {
            var sut = new ShimmerSDK_IMU();

            // snapshot iniziale
            var before = SnapshotFlags(sut);

            // cambia SOLO WideRangeAccelerometer e Battery
            sut.EnableWideRangeAccelerometer = !before.EnableWideRangeAccelerometer;
            sut.EnableBattery = !before.EnableBattery;

            var after = SnapshotFlags(sut);

            // Quelli modificati devono essere cambiati
            Assert.Equal(!before.EnableWideRangeAccelerometer, after.EnableWideRangeAccelerometer);
            Assert.Equal(!before.EnableBattery, after.EnableBattery);

            // Tutti gli altri invariati
            Assert.Equal(before.EnableLowNoiseAccelerometer, after.EnableLowNoiseAccelerometer);
            Assert.Equal(before.EnableGyroscope, after.EnableGyroscope);
            Assert.Equal(before.EnableMagnetometer, after.EnableMagnetometer);
            Assert.Equal(before.EnablePressureTemperature, after.EnablePressureTemperature);
            Assert.Equal(before.EnableExtA6, after.EnableExtA6);
            Assert.Equal(before.EnableExtA7, after.EnableExtA7);
            Assert.Equal(before.EnableExtA15, after.EnableExtA15);

            // SamplingRate non deve cambiare per effetto dei flag
            Assert.InRange(sut.SamplingRate, 51.199, 51.201);
        }

        // SamplingRate independence — behavior
        // Cambiare SamplingRate non altera i flag
        [Fact]
        public void Changing_SamplingRate_Does_Not_Affect_Flags()
        {
            var sut = new ShimmerSDK_IMU();
            var before = SnapshotFlags(sut);

            sut.SamplingRate = 200.0;

            var after = SnapshotFlags(sut);
            Assert.Equal(before, after);
        }

        // ==== helpers ====
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
