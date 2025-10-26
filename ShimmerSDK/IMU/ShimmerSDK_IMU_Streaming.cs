/* 
 * ShimmerSDK_IMU — This partial handles the connect/stream/disconnect lifecycle.
 * Applies sampling rate & sensor bitmap, refreshes metadata, and exposes IsConnected().
 * Pure lifecycle logic; configuration flags/properties live in other partials.
 */


using System;
using System.Threading;
using System.Threading.Tasks;


namespace ShimmerSDK.IMU
{

    /// <summary>
    /// Partial class that provides methods to manage the connection, streaming, and disconnection
    /// lifecycle of a Shimmer IMU device.
    /// </summary>
    public partial class ShimmerSDK_IMU
    {

        /// <summary>
        /// Connects to the Shimmer IMU device if not already connected.
        /// </summary>
        public void Connect()
        {

#if ANDROID

            if (shimmerAndroid == null)
                throw new InvalidOperationException("ConfigureAndroid was not called");

            // Connect
            shimmerAndroid.Connect();
            return;

#elif WINDOWS

            if (IsConnected()) return;

            var dev = shimmer ?? throw new InvalidOperationException("Shimmer device not configured (null).");
            dev.Connect();

            var sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);

            // Apply sampling rate
            dev.WriteSamplingRate(sr);
            System.Threading.Thread.Sleep(150);

            // Apply sensor bitmap
            dev.WriteSensors(_winEnabledSensors);
            System.Threading.Thread.Sleep(200);

            // Refresh metadata
            dev.Inquiry();
            System.Threading.Thread.Sleep(200);


#elif MACCATALYST || IOS

            return;

#endif

        }


        /// <summary>
        /// Starts data streaming from the Shimmer IMU device after a short delay.
        /// </summary>
        public async void StartStreaming()
        {

            await Task.Yield();

#if ANDROID

            // Not configured
            if (shimmerAndroid == null) return;
            await StartStreamingAndroidSequenceAsync();
            return;

#elif WINDOWS

            var s = shimmer;
            if (s is null)
                throw new InvalidOperationException("Device not initialized. Call Configure/Connect before StartStreaming().");

            await DelayWork(1000);
            s.StartStreaming();

#elif MACCATALYST || IOS

            await StartStreamingMacAsync();

#endif

        }


        /// <summary>
        /// Stops data streaming from the Shimmer IMU device and waits briefly.
        /// </summary>
        public async void StopStreaming()
        {

            await Task.Yield();

#if ANDROID

            shimmerAndroid?.StopStreaming();
            await DelayWork(1000);
            return;

#elif WINDOWS

            shimmer.StopStreaming();
            await DelayWork(1000);

#elif MACCATALYST || IOS

            await StopStreamingMacAsync();

#endif

        }


        /// <summary>
        /// Returns whether the Shimmer IMU device is currently connected.
        /// </summary>
        /// <returns>True if connected; otherwise, false.</returns>
        public bool IsConnected()
        {

#if ANDROID

            return shimmerAndroid != null && shimmerAndroid.IsConnected();

#elif WINDOWS
            
            var s = Volatile.Read(ref shimmer);
            return s != null && s.IsConnected();

#elif MACCATALYST || IOS

            return IsConnectedMac();

#else

            return false;

#endif

        }


        /// <summary>
        /// Asynchronously waits for the specified number of milliseconds.
        /// Used to provide delay between operations.
        /// </summary>
        /// <param name="t">Time in milliseconds to wait.</param>
        private async Task DelayWork(int t)
        {
            await Task.Delay(t);
        }


#if ANDROID

        /// <summary>
        /// Android start sequence: ensures CONNECTED state, reapplies config, and starts streaming.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if Android configuration is missing.</exception>
        /// <returns>A task that completes when the start sequence has finished.</returns>
        private async Task StartStreamingAndroidSequenceAsync()
        {
            try
            {
                if (shimmerAndroid == null)
                    throw new InvalidOperationException("Shimmer Android is not configured");

                // Ensure connection is fully established (wait for CONNECTED state)
                _androidConnectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (shimmerAndroid.IsConnected())
                    _androidConnectedTcs.TrySetResult(true);

                var connectedReady = await Task.WhenAny(_androidConnectedTcs.Task, Task.Delay(5000));
                if (connectedReady != _androidConnectedTcs.Task)
                    global::Android.Util.Log.Warn("Shimmer", "Timeout waiting for CONNECTED (continuing)");

                // Ensure not streaming; clear sensors (no flush)
                shimmerAndroid.StopStreaming();
                await DelayWork(150);
                shimmerAndroid.WriteSensors(0);
                await DelayWork(150);

                // Apply config in order: SR → ranges/power → sensors
                int sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
                shimmerAndroid.WriteSamplingRate(sr);
                await DelayWork(180);

                shimmerAndroid.WriteAccelRange(0);
                shimmerAndroid.WriteGyroRange(0);
                shimmerAndroid.SetLowPowerAccel(false);
                shimmerAndroid.SetLowPowerGyro(false);
                shimmerAndroid.WriteInternalExpPower(0);
                await DelayWork(150);

                int sensors = _androidEnabledSensors;
                shimmerAndroid.WriteSensors(sensors);
                await DelayWork(180);

                // Refresh metadata (packet size, signal names/indices)
                shimmerAndroid.Inquiry();
                await DelayWork(350);

                // Reload generic calibrations
                shimmerAndroid.ReadCalibrationParameters("All");
                await DelayWork(250);

                // Prepare waiters for streaming ACK and first data packet
                _androidStreamingAckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _androidFirstPacketTcs  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Start streaming
                shimmerAndroid.StartStreaming();

                // Wait for STREAMING state (ACK) then first packet
                var ackReady = await Task.WhenAny(_androidStreamingAckTcs.Task, Task.Delay(2000));
                if (ackReady != _androidStreamingAckTcs.Task)
                    global::Android.Util.Log.Warn("Shimmer", "Timeout waiting for ACK/STREAMING (continuing)");

                var firstReady = await Task.WhenAny(_androidFirstPacketTcs.Task, Task.Delay(2000));
                if (firstReady != _androidFirstPacketTcs.Task)
                    global::Android.Util.Log.Warn("Shimmer", "Timeout waiting for first DATA_PACKET (continuing)");

                global::Android.Util.Log.Info("Shimmer", $"StartStreaming OK (sensors=0x{sensors:X}, SR={sr})");
            }
            catch (Exception ex)
            {

                if (OperatingSystem.IsAndroid() && OperatingSystem.IsAndroidVersionAtLeast(21))
                {
                    global::Android.Util.Log.Error("Shimmer", "StartStreamingAndroidSequenceAsync exception:");
                    global::Android.Util.Log.Error("Shimmer", ex.ToString());
                }

                System.Diagnostics.Debug.WriteLine(ex);
                throw;
            }
        }

#endif

    }
}
