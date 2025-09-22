/* 
 * ShimmerSDK_EXG — This partial handles the connect/stream/disconnect lifecycle.
 * Applies sampling rate & sensor bitmap, refreshes metadata, and exposes IsConnected().
 * Pure lifecycle logic; configuration flags/properties live in other partials.
 */


using System;
using System.Threading.Tasks;

#if ANDROID
using ShimmerSDK.Android;
#endif


namespace ShimmerSDK.EXG
{

    /// <summary>
    /// Partial class that provides methods to manage the connection, streaming, and disconnection
    /// lifecycle of a Shimmer EXG device.
    /// </summary>
    public partial class ShimmerSDK_EXG
    {

        /// <summary>
        /// Connects to the Shimmer EXG device if not already connected.
        /// </summary>
        public void Connect()
        {

#if ANDROID

            if (shimmerAndroid == null)
                throw new InvalidOperationException("ConfigureAndroid was not called");

            shimmerAndroid.Connect();
            return;
            
#elif WINDOWS

            if (IsConnected()) return;
            shimmer.Connect();

            var sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);

            // Apply sampling rate
            shimmer.WriteSamplingRate(sr);
            System.Threading.Thread.Sleep(150);

            // Apply sensor bitmap
            shimmer.WriteSensors(_winEnabledSensors);
            System.Threading.Thread.Sleep(200);

            // Refresh metadata
            shimmer.Inquiry();
            System.Threading.Thread.Sleep(200);

#elif MACCATALYST || IOS

            return;

#endif
        }



        /// <summary>
        /// Starts data streaming. On Android, safely reapplies config first
        /// (stop → clear sensors → set sampling/ranges/power → restore sensors → inquiry + calibrations),
        /// then starts the stream; on other platforms, issues a simple start.
        /// </summary>
        public async void StartStreaming()
        {


#if ANDROID
            
            // Not configured
            if (shimmerAndroid == null) return;

            // Ensure not streaming before reconfig
            shimmerAndroid.StopStreaming();
            await DelayWork(150);

            // Clear sensor bitmap to avoid partial packets
            shimmerAndroid.WriteSensors(0);
            await DelayWork(150);

            // Apply sampling rate
            int sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
            shimmerAndroid.WriteSamplingRate(sr);
            await DelayWork(180);

            // Apply default ranges and power settings
            shimmerAndroid.WriteAccelRange(0);
            shimmerAndroid.WriteGyroRange(0);
            shimmerAndroid.SetLowPowerAccel(false);
            shimmerAndroid.SetLowPowerGyro(false);
            shimmerAndroid.WriteInternalExpPower(0);
            await DelayWork(150);

            // Restore selected sensors (EXG1/EXG2 included if enabled)
            shimmerAndroid.WriteSensors(_androidEnabledSensors);
            await DelayWork(180);

            // Refresh packet metadata (size, signal names/indices)
            shimmerAndroid.Inquiry();
            await DelayWork(350);

            // Reload calibration parameters
            shimmerAndroid.ReadCalibrationParameters("All");
            await DelayWork(250);

            // Start streaming
            shimmerAndroid.StartStreaming();
            return;

#elif WINDOWS

            await Task.Delay(1000);
            shimmer.StartStreaming();

#elif MACCATALYST || IOS

    await StartStreamingMacAsync();

#endif
        }


        /// <summary>
        /// Stops data streaming from the Shimmer EXG device and waits briefly.
        /// </summary>
        public async void StopStreaming()
        {

#if ANDROID

            shimmerAndroid?.StopStreaming();
            await DelayWork(1000);
            return;

#elif WINDOWS

            shimmer.StopStreaming();
            await Task.Delay(1000);

#elif MACCATALYST || IOS

            await StopStreamingMacAsync();
#endif

        }


        /// <summary>
        /// Returns whether the Shimmer EXG device is currently connected.
        /// </summary>
        /// <returns>True if connected; otherwise, false.</returns>
        public bool IsConnected()
        {

#if ANDROID

            return shimmerAndroid != null && shimmerAndroid.IsConnected();

#elif WINDOWS

            return shimmer != null && shimmer.IsConnected();

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
    }
}
