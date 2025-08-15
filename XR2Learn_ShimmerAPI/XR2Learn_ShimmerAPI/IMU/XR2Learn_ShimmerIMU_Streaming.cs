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
#if ANDROID
private async Task StartStreamingAndroidSequenceAsync()
{
    try
    {
        // 0) Stop idempotente
        shimmerAndroid.StopStreaming();
        await DelayWork(150);

        // 1) Svuota stato/ACK vecchi
        shimmerAndroid.WriteSensors(0);
        await DelayWork(100);
        shimmerAndroid.Flush();
        shimmerAndroid.FlushInput();
        await DelayWork(120);

        // 2) Sampling + ranges + low power (prima dei sensori)
        int sr = (int)Math.Round(_samplingRate);
        if (sr <= 0) sr = 51;
        shimmerAndroid.WriteSamplingRate(sr);
        await DelayWork(150);

        shimmerAndroid.WriteAccelRange(0);   // default
        shimmerAndroid.WriteGyroRange(0);    // default
        shimmerAndroid.WriteGSRRange(0);     // se non serve GSR, va bene lasciarlo 0
        shimmerAndroid.SetLowPowerAccel(false);
        shimmerAndroid.SetLowPowerGyro(false);
        shimmerAndroid.WriteInternalExpPower(0);
        await DelayWork(120);

        // 3) Applica la bitmap sensori calcolata in ConfigureAndroid
        int sensors = _androidEnabledSensors;
        shimmerAndroid.WriteSensors(sensors);
        await DelayWork(150);

        // 4) Leggi calibrazioni DOPO ranges+bitmap
        shimmerAndroid.ReadCalibrationParameters("All");
        await DelayWork(250);

        // 5) Reset mapping e scarico residui
        firstDataPacketAndroid = true;
        shimmerAndroid.FlushInput();
        await DelayWork(80);

        // 6) Avvio streaming
        shimmerAndroid.StartStreaming();
        await DelayWork(120);

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
