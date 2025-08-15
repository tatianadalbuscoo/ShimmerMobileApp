using System;
using System.Threading.Tasks;

namespace XR2Learn_ShimmerAPI.IMU
{

    /// <summary>
    /// Partial class that provides methods to manage the connection, streaming, and disconnection
    /// lifecycle of a Shimmer IMU device.
    /// </summary>
    public partial class XR2Learn_ShimmerIMU
    {

        /// <summary>
        /// Connects to the Shimmer IMU device if not already connected.
        /// </summary>
        public void Connect()
        {
#if ANDROID
            if (shimmerAndroid == null)
                throw new InvalidOperationException("ConfigureAndroid non chiamata");

            shimmerAndroid.Connect();
            global::Android.Util.Log.Info("Shimmer", $"Android Connect() -> {shimmerAndroid.IsConnected()}");
            return;
#elif WINDOWS
            if (IsConnected()) return;
            shimmer.Connect();
#elif MACCATALYST
            if (IsConnectedMac()) return;
            ConnectMac();
#else
            Console.WriteLine("Connect() non supportato su questa piattaforma.");
#endif
        }

        /// <summary>
        /// Disconnects from the Shimmer IMU device and clears the UI callback.
        /// Includes a short delay to allow disconnection to complete.
        /// </summary>
        public async void Disconnect()
        {
#if ANDROID
            shimmerAndroid?.StopStreaming();
            shimmerAndroid?.Disconnect();
            await DelayWork(1000);
            return;
#elif WINDOWS
            shimmer.Disconnect();
            await DelayWork(1000);
            shimmer.UICallback = null;
#elif MACCATALYST
            await DisconnectMacAsync();
#else
            Console.WriteLine("Disconnect() non supportato su questa piattaforma.");
#endif
        }

        /// <summary>
        /// Starts data streaming from the Shimmer IMU device after a short delay.
        /// </summary>
        public async void StartStreaming()
        {
#if ANDROID
            if (shimmerAndroid == null) return;
            await StartStreamingAndroidSequenceAsync(); // sequenza speculare a Windows
            return;
#elif WINDOWS
            await DelayWork(1000);
            shimmer.StartStreaming();
#elif MACCATALYST
            await StartStreamingMacAsync();
#else
            Console.WriteLine("StartStreaming() non supportato su questa piattaforma.");
#endif
        }

        /// <summary>
        /// Stops data streaming from the Shimmer IMU device and waits briefly.
        /// </summary>
        public async void StopStreaming()
        {
#if ANDROID
            shimmerAndroid?.StopStreaming();
            await DelayWork(1000); // come Windows
            return;
#elif WINDOWS
            shimmer.StopStreaming();
            await DelayWork(1000);
#elif MACCATALYST
            await StopStreamingMacAsync();
#else
            Console.WriteLine("StopStreaming() non supportato su questa piattaforma.");
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
            return shimmer.IsConnected();
#elif MACCATALYST
            return IsConnectedMac();
#else
            return false;
#endif
        }

#if ANDROID
        // Sequenza Android resa analoga a Windows
        private async Task StartStreamingAndroidSequenceAsync()
        {
            try
            {
                // 0) Stop idempotente
                shimmerAndroid.StopStreaming();
                await DelayWork(120);

                // 1) Inquiry + Calibrazione (serve per layout/scale corretti)
                shimmerAndroid.Inquiry();
                await DelayWork(250);
                shimmerAndroid.ReadCalibrationParameters("All");
                await DelayWork(250);

                // 2) Applica bitmap calcolato in ConfigureAndroid (speculare a Windows)
                var sensors = _androidEnabledSensors;

                // pulisci poi imposta
                shimmerAndroid.WriteSensors(0);
                await DelayWork(120);
                shimmerAndroid.WriteSensors(sensors);
                await DelayWork(120);

                // 3) Ranges / LowPower / ExpPower (placeholder finché non esponi impostazioni)
                shimmerAndroid.WriteAccelRange(0);
                shimmerAndroid.WriteGyroRange(0);
                shimmerAndroid.WriteGSRRange(0);
                shimmerAndroid.SetLowPowerAccel(false);
                shimmerAndroid.SetLowPowerGyro(false);
                shimmerAndroid.WriteInternalExpPower(0);
                await DelayWork(120);

                // 4) Sampling rate come Windows
                int sr = (int)Math.Round(_samplingRate);
                if (sr <= 0) sr = 51;
                shimmerAndroid.WriteSamplingRate(sr);
                await DelayWork(200);

                // 5) reset mapping al primo pacchetto
                firstDataPacketAndroid = true;

                // 6) Avvia streaming
                shimmerAndroid.StartStreaming();
                await DelayWork(100);

                global::Android.Util.Log.Info("Shimmer", $"Android StartStreaming OK (sensors=0x{sensors:X}, SR={sr})");
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("Shimmer", "StartStreamingAndroidSequenceAsync exception:");
                global::Android.Util.Log.Error("Shimmer", ex.ToString());
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
#endif

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
