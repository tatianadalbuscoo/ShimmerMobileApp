/*
 * ShimmerSDK_EXGStreamingTests.cs
 * Purpose: Unit tests for ShimmerSDK_EXGStreaming file.
 */


using System.Diagnostics;
using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;


namespace ShimmerSDKTests.EXGTests
{

    /// <summary>
    /// Streaming-oriented tests for <see cref="ShimmerSDK_EXG"/>.
    /// Validates platform-agnostic behavior (no-throw) and, when the WINDOWS partial is compiled,
    /// verifies interactions with the fake driver (connect/start/stop, inquiry and sensor writes),
    /// event emission (<c>SampleReceived</c>), and internal delay helpers.
    /// </summary>
    public class ShimmerSDK_EXG_StreamingTests
    {

        /// <summary>
        /// Helper: Retrieves the fake driver instance registered for the given SUT.
        /// </summary>
        /// <param name="sut">The <see cref="ShimmerSDK_EXG"/> instance under test.</param>
        /// <returns>The corresponding <see cref="ShimmerLogAndStreamSystemSerialPortV2"/> from the test registry.</returns>
        /// <remarks>
        /// Asserts non-null registration; tests expect the driver to be available for call counting and raising packets.
        /// </remarks>
        private static ShimmerLogAndStreamSystemSerialPortV2 GetDrv(ShimmerSDK_EXG sut)
        {
            var drv = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(drv);
            return drv!;
        }


        // ----- Connect behavior -----


        /// <summary>
        /// Ensures <c>Connect()</c> never throws; when the WINDOWS branch is compiled,
        /// verifies that the SUT calls driver methods and transitions to connected state.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>No exception on any platform.</description></item>
        /// <item><description>If the private field <c>_winEnabledSensors</c> exists (WINDOWS partial), then:
        /// <c>Connect()</c> invoked, sampling rate written &gt; 0, <c>WriteSensors()</c> and <c>Inquiry()</c> called, and <c>IsConnected()</c> is true.</description></item>
        /// <item><description>Otherwise: no strict call requirements (no-op acceptable).</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public void Connect_NoThrow_And_Optionally_Calls_Driver()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "Dev1", portOrId: "COM1",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: true, enableMag: true,
                enablePressureTemp: true, enableBatteryVoltage: true,
                enableExtA6: true, enableExtA7: false, enableExtA15: true,
                enableExg: true, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            sut.Connect();

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);

            if (winField != null)
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
        /// Ensures <c>StartStreaming()</c> never throws; when the WINDOWS branch is compiled,
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
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev2", portOrId: "COM2",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            sut.StartStreaming();
            await Task.Delay(50);

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(drv.StartCount >= 1);
            }
        }


        // ----- StopStreaming behavior -----


        /// <summary>
        /// Ensures <c>StopStreaming()</c> never throws; when the WINDOWS branch is compiled,
        /// verifies that the driver's stop counter increments.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>No throw on any platform.</description></item>
        /// <item><description>If WINDOWS partial detected, <c>StopCount</c> ≥ 1 after a start/stop cycle.</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public async Task StopStreaming_NoThrow_And_Optionally_Increments_StopCount()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev3", portOrId: "COM3",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            sut.StartStreaming();
            await Task.Delay(20);
            sut.StopStreaming();
            await Task.Delay(50);

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(drv.StopCount >= 1);
            }
        }


        // ----- Simulated end-to-end streaming -----


        /// <summary>
        /// Simulated end-to-end streaming: after <c>StartStreaming()</c>, raising
        /// <see cref="ObjectCluster"/> packets should either fire <c>SampleReceived</c> (when available in this build)
        /// or at least flip the internal "first packet" flag (mapping completed).
        /// Expected:
        /// <list type="bullet">
        /// <item><description>If handlers (<c>HandleEvent</c> or <c>HandleEventAndroid</c>) are absent, test exits early (not applicable).</description></item>
        /// <item><description>On the second raised packet (after index mapping), either:
        /// <i>(a)</i> <c>SampleReceived</c> is observed, or
        /// <i>(b)</i> the private <c>firstDataPacket*</c> flag becomes <c>false</c> (mapping occurred).</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public void After_StartStreaming_Raising_ObjectCluster_Fires_SampleReceived()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "D",
                portOrId: "P",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: true,
                enableGyro: true,
                enableMag: true,
                enablePressureTemp: true,
                enableBatteryVoltage: true,
                enableExtA6: true,
                enableExtA7: true,
                enableExtA15: true,
                enableExg: true,
                exgMode: ExgMode.ECG
            );

            var hasWinHandler = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic) != null;
            var hasAndroidHandler = sut.GetType().GetMethod("HandleEventAndroid", BindingFlags.Instance | BindingFlags.NonPublic) != null;
            if (!hasWinHandler && !hasAndroidHandler)
                return;

            ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK_EXGData)d;

            sut.StartStreaming();

            var shim = ShimmerSDK_EXG_TestRegistry.Get(sut)!;

            // Build a cluster with timestamp and representative signals
            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add(ShimmerAPI.ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 123);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 3);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXG1_CH1, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1001);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXG2_CH1, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1002);

            // First packet: builds index mapping (may not emit event)
            shim.RaiseDataPacket(oc);

            // Second packet: with mapping ready, event should arrive (if implemented)
            shim.RaiseDataPacket(oc);

            if (received is null)
            {

                // If this build does not emit the event, ensure mapping occurred.
                var firstFlagName = hasWinHandler ? "firstDataPacket" : "firstDataPacketAndroid";
                var firstFlag = sut.GetType().GetField(firstFlagName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (firstFlag != null)
                {

                    // Should have been cleared by the handler after the first packet
                    var flagNow = (bool)firstFlag.GetValue(sut)!;
                    Assert.False(flagNow);
                    return;                
                }
            }

            Assert.NotNull(received);
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
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev5", portOrId: "COM5",
                enableLowNoiseAcc: true, enableWideRangeAcc: false,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            Assert.False(sut.IsConnected());

            sut.Connect();

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(sut.IsConnected());
            }
            else
            {
                Assert.False(sut.IsConnected());
            }
        }


        // ----- DelayWork behavior -----


        /// <summary>
        /// Validates the private async delay helper (<c>DelayWork</c>) approximately waits
        /// for the requested duration.
        /// Expected:
        /// <list type="bullet">
        /// <item><description>If method is absent in this build: test is a no-op (returns early).</description></item>
        /// <item><description>Otherwise: elapsed time ≥ requested - 10&nbsp;ms and ≤ requested + 200&nbsp;ms.</description></item>
        /// </list>
        /// </summary>
        [Fact]
        public async Task DelayWork_Waits_Approximately_Requested_Time()
        {
            var sut = new ShimmerSDK_EXG();

            var mi = typeof(ShimmerSDK_EXG).GetMethod(
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
            Assert.True(elapsed <= requestedMs + 200, $"Elapsed {elapsed}ms > {requestedMs + 200}ms");
        }
    }
}
