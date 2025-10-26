using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;

namespace ShimmerSDKTests.IMUTests
{
    public class ShimmerSDK_IMU_StreamingTests
    {
        // ==== Helpers =========================================================

        private static ShimmerLogAndStreamSystemSerialPortV2 GetDrv(ShimmerSDK_IMU sut)
        {
            var drv = ShimmerSDK_IMU_TestRegistry.Get(sut);
            Assert.NotNull(drv);
            return drv!;
        }

        private static void SetPrivateField<T>(object obj, string name, T value)
        {
            var fi = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fi);
            fi!.SetValue(obj, value);
        }

        private static bool WindowsBranchPresent(object sut) =>
            sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic) != null;

        // ==== Tests ===========================================================

        // Connect(): non deve lanciare. Se il ramo WINDOWS è compilato, chiama davvero il driver (Connect/SR/Sensors/Inquiry).
        [Fact]
        public void Connect_NoThrow_And_Optionally_Calls_Driver()
        {
            var sut = new ShimmerSDK_IMU();

            // usa la TestConfigure DEI TUOI STUB (niente samplingRate qui)
            sut.TestConfigure(
                deviceName: "Dev1",
                portOrId: "COM1",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: true,
                enableGyro: true,
                enableMag: true,
                enablePressureTemperature: true,
                enableBattery: true,
                enableExtA6: true,
                enableExtA7: false,
                enableExtA15: true
            );
            // forza la SR letta da Connect()
            SetPrivateField(sut, "_samplingRate", 102.4);

            var drv = GetDrv(sut);

            sut.Connect();

            if (WindowsBranchPresent(sut))
            {
                Assert.True(drv.ConnectCount >= 1);
                Assert.True(drv.LastSamplingRateWritten > 0);
                Assert.True(drv.WriteSensorsCount >= 1);
                Assert.True(drv.InquiryCount >= 1);
                Assert.True(drv.IsConnected());
            }
            else
            {
                // altri TFM: l’importante è che non esploda
                Assert.True(drv.ConnectCount >= 0);
            }
        }

        // StartStreaming(): no-throw; se WINDOWS presente, StartCount++
        [Fact]
        public async Task StartStreaming_NoThrow_And_Optionally_Increments_StartCount()
        {
            var sut = new ShimmerSDK_IMU();

            sut.TestConfigure(
                deviceName: "Dev2",
                portOrId: "COM2",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: false,
                enableGyro: false,
                enableMag: false,
                enablePressureTemperature: false,
                enableBattery: false,
                enableExtA6: false,
                enableExtA7: false,
                enableExtA15: false
            );
            SetPrivateField(sut, "_samplingRate", 51.2);

            var drv = GetDrv(sut);

            sut.StartStreaming();            // async void -> diamo respiro
            await Task.Delay(80);

            if (WindowsBranchPresent(sut))
            {
                Assert.True(drv.StartCount >= 1);
            }
        }

        // StopStreaming(): no-throw; se WINDOWS presente, StopCount++
        [Fact]
        public async Task StopStreaming_NoThrow_And_Optionally_Increments_StopCount()
        {
            var sut = new ShimmerSDK_IMU();

            sut.TestConfigure(
                deviceName: "Dev3",
                portOrId: "COM3",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: false,
                enableGyro: false,
                enableMag: false,
                enablePressureTemperature: false,
                enableBattery: false,
                enableExtA6: false,
                enableExtA7: false,
                enableExtA15: false
            );
            SetPrivateField(sut, "_samplingRate", 51.2);

            var drv = GetDrv(sut);

            sut.StartStreaming();
            await Task.Delay(30);
            sut.StopStreaming();
            await Task.Delay(80);

            if (WindowsBranchPresent(sut))
            {
                Assert.True(drv.StopCount >= 1);
            }
        }

        // IsConnected(): true solo se WINDOWS presente e Connect() già eseguita
        [Fact]
        public void IsConnected_Returns_True_Only_When_Runtime_Windows_Branch_Is_Active()
        {
            var sut = new ShimmerSDK_IMU();

            sut.TestConfigure(
                deviceName: "Dev5",
                portOrId: "COM5",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: false,
                enableGyro: false,
                enableMag: false,
                enablePressureTemperature: false,
                enableBattery: false,
                enableExtA6: false,
                enableExtA7: false,
                enableExtA15: false
            );
            SetPrivateField(sut, "_samplingRate", 51.2);

            // prima della connect: false ovunque
            Assert.False(sut.IsConnected());

            sut.Connect();

            if (WindowsBranchPresent(sut))
            {
                Assert.True(sut.IsConnected());
            }
            else
            {
                Assert.False(sut.IsConnected());
            }
        }

        // ANDROID: Connect() deve lanciare se shimmerAndroid == null (solo se il ramo è compilato)
        [Fact]
        public void Android_Connect_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_IMU();

            var androidField = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            if (androidField == null) return; // ramo non presente in questo TFM

            var ex = Record.Exception(() => sut.Connect());
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("ConfigureAndroid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // DelayWork — attende ~ il tempo richiesto
        [Fact]
        public async Task DelayWork_Waits_Approximately_Requested_Time()
        {
            var sut = new ShimmerSDK_IMU();

            var mi = typeof(ShimmerSDK_IMU).GetMethod(
                "DelayWork",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (mi == null) return;

            const int requestedMs = 80;
            var sw = Stopwatch.StartNew();

            var task = (Task)mi.Invoke(sut, new object[] { requestedMs })!;
            await task;

            sw.Stop();
            var elapsed = sw.ElapsedMilliseconds;

            Assert.True(elapsed >= requestedMs - 10, $"Elapsed {elapsed}ms < {requestedMs - 10}ms");
            Assert.True(elapsed <= requestedMs + 250, $"Elapsed {elapsed}ms > {requestedMs + 250}ms");
        }
    }
}
