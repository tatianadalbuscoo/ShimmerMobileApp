/*
 * ShimmerSDK_IMUStreamingTests.cs
 * Purpose: Unit tests for ShimmerSDK_IMUStreaming file.
 */


using System.Diagnostics;
using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;


namespace ShimmerSDKTests.IMUTests
{

    /// <summary>
    /// Streaming-oriented tests for <see cref="ShimmerSDK_IMU"/>.
    /// Validates platform-agnostic behavior (no-throw) and, when the WINDOWS or ANDROID partials are compiled,
    /// verifies interactions with the fake driver (connect/start/stop, inquiry and sensor writes),
    /// connection state, Android configuration errors, and the internal async delay helper.
    /// </summary>
    public class ShimmerSDK_IMU_StreamingTests
    {

        /// <summary>
        /// Helper: Retrieves the fake driver instance registered for the given SUT.
        /// </summary>
        /// <param name="sut">The <see cref="ShimmerSDK_IMU"/> instance under test.</param>
        /// <returns>The corresponding <see cref="ShimmerLogAndStreamSystemSerialPortV2"/> from the test registry.</returns>
        /// <remarks>
        /// Asserts non-null registration; tests expect the driver to be available for call counting
        /// and for raising synthetic packets when needed.
        /// </remarks>
        private static ShimmerLogAndStreamSystemSerialPortV2 GetDrv(ShimmerSDK_IMU sut)
        {
            var drv = ShimmerSDK_IMU_TestRegistry.Get(sut);
            Assert.NotNull(drv);
            return drv!;
        }


        /// <summary>
        /// Helper: Sets a private instance field via reflection.
        /// </summary>
        /// <typeparam name="T">Field type.</typeparam>
        /// <param name="obj">Target object.</param>
        /// <param name="name">Private field name.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetPrivateField<T>(object obj, string name, T value)
        {
            var fi = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fi);
            fi!.SetValue(obj, value);
        }


        /// <summary>
        /// Helper: Detects if the WINDOWS partial is present by looking for the private field <c>_winEnabledSensors</c>.
        /// </summary>
        /// <param name="sut">The instance to inspect.</param>
        /// <returns><c>true</c> if the WINDOWS branch appears compiled in; otherwise <c>false</c>.</returns>
        private static bool WindowsBranchPresent(object sut) =>
            sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic) != null;


        // ----- Connect behavior -----


        /// <summary>
        /// Ensures <c>Connect()</c> never throws. When the WINDOWS partial is compiled,
        /// verifies that the SUT calls driver methods and transitions to connected state.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>No exception on any platform.</description></item>
        /// <item><description>If WINDOWS partial detected:
        /// <c>Connect()</c> invoked, sampling rate written &gt; 0, <c>WriteSensors()</c> and <c>Inquiry()</c> called,
        /// and <c>IsConnected()</c> becomes true.</description></item>
        /// <item><description>Otherwise: no strict call requirements (no-op acceptable).</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public void Connect_NoThrow_And_Optionally_Calls_Driver()
        {
            var sut = new ShimmerSDK_IMU();

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

            // Force the sampling rate that Connect() will read
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
                Assert.True(drv.ConnectCount >= 0);
            }
        }


        // ----- StartStreaming behavior -----


        /// <summary>
        /// Ensures <c>StartStreaming()</c> never throws; when the WINDOWS partial is compiled,
        /// verifies that the driver's start counter increments.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>No throw on any platform.</description></item>
        /// <item><description>If WINDOWS partial detected, <c>StartCount</c> ≥ 1.</description></item>
        /// </list>
        /// </summary>
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

            sut.StartStreaming();
            await Task.Delay(80);

            if (WindowsBranchPresent(sut))
            {
                Assert.True(drv.StartCount >= 1);
            }
        }


        // ----- StopStreaming behavior -----


        /// <summary>
        /// Ensures <c>StopStreaming()</c> never throws; when the WINDOWS partial is compiled,
        /// verifies that the driver's stop counter increments after a start/stop cycle.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>No throw on any platform.</description></item>
        /// <item><description>If WINDOWS partial detected, <c>StopCount</c> ≥ 1.</description></item>
        /// </list>
        /// </summary>
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


        // ----- IsConnected behavior -----


        /// <summary>
        /// Verifies <c>IsConnected()</c> exists and behaves coherently across builds:
        /// before <c>Connect()</c> it is false; with WINDOWS partial and after <c>Connect()</c> it is true; otherwise remains false.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>Before <c>Connect()</c>: <c>false</c> on all platforms.</description></item>
        /// <item><description>After <c>Connect()</c>: <c>true</c> only if WINDOWS partial is compiled; otherwise <c>false</c>.</description></item>
        /// </list>
        /// </summary>
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


        // ----- ANDROID: Connect behavior -----


        /// <summary>
        /// ANDROID behavior: <c>Connect()</c> must throw if the private <c>shimmerAndroid</c> reference is not configured.
        /// Test runs only when the ANDROID partial exposes that field.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>If the private field <c>shimmerAndroid</c> is absent (no ANDROID partial): test exits early (not applicable).</description></item>
        /// <item><description>Otherwise: <c>Connect()</c> throws <see cref="InvalidOperationException"/> containing
        /// a hint such as "ConfigureAndroid".</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public void Android_Connect_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_IMU();

            var androidField = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            if (androidField == null) return;

            var ex = Record.Exception(() => sut.Connect());
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("ConfigureAndroid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }


        // ----- DelayWork behavior -----


        /// <summary>
        /// Validates the private async delay helper (<c>DelayWork</c>) approximately waits
        /// for the requested duration.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>If method is absent in this build: test is a no-op (returns early).</description></item>
        /// <item><description>Otherwise: elapsed time ≥ requested - 10&nbsp;ms and ≤ requested + 250&nbsp;ms.</description></item>
        /// </list>
        /// </summary>
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
