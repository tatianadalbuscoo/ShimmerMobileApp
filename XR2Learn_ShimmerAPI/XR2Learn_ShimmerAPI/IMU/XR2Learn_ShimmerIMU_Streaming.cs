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

    // Applica sampling rate e bitmap sensori calcolata in ConfigureWindows(...)
    var sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
    shimmer.WriteSamplingRate(sr);
    System.Threading.Thread.Sleep(150);

    shimmer.WriteSensors(_winEnabledSensors);
    System.Threading.Thread.Sleep(200);

    // Aggiorna la mappa dei campi (nomi/indici) lato API
    shimmer.Inquiry();
    System.Threading.Thread.Sleep(200);
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
private async System.Threading.Tasks.Task StartStreamingAndroidSequenceAsync()
{
    try
    {
        if (shimmerAndroid == null)
            throw new InvalidOperationException("Shimmer Android non configurato.");

        // A) Assicurati che la connessione sia COMPLETATA (stato CONNECTED dall’API)
        _androidConnectedTcs = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        if (shimmerAndroid.IsConnected())
            _androidConnectedTcs.TrySetResult(true); // nel caso fossi già connected

        var connectedReady = await System.Threading.Tasks.Task.WhenAny(_androidConnectedTcs.Task, System.Threading.Tasks.Task.Delay(5000));
        if (connectedReady != _androidConnectedTcs.Task)
            global::Android.Util.Log.Warn("Shimmer", "Timeout in attesa di CONNECTED (proseguo)");

        // B) Stop idempotente e reset sensori (ma NIENTE FlushConnection!)
        shimmerAndroid.StopStreaming();
        await DelayWork(150);
        shimmerAndroid.WriteSensors(0);
        await DelayWork(150);

        // C) Applica sampling + ranges + sensori (ordine importante)
        int sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
        shimmerAndroid.WriteSamplingRate(sr);
        await DelayWork(180);

        shimmerAndroid.WriteAccelRange(0);
        shimmerAndroid.WriteGyroRange(0);
        shimmerAndroid.WriteGSRRange(0);
        shimmerAndroid.SetLowPowerAccel(false);
        shimmerAndroid.SetLowPowerGyro(false);
        shimmerAndroid.WriteInternalExpPower(0);
        await DelayWork(150);

        int sensors = _androidEnabledSensors;
        shimmerAndroid.WriteSensors(sensors);
        await DelayWork(180);

        // D) **QUI** fai l’Inquiry per aggiornare PacketSize/SignalNameArray in ShimmerAPI
        shimmerAndroid.Inquiry();
        await DelayWork(350);

        // (Opzionale) calibrazioni generiche
        shimmerAndroid.ReadCalibrationParameters("All");
        await DelayWork(250);

        // E) Prepara attese StartStreaming
        _androidStreamingAckTcs = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        _androidFirstPacketTcs  = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        // F) Start
        shimmerAndroid.StartStreaming();

        // G) Attendi stato STREAMING (ACK di start gestito dall’API → event STATE_CHANGE)
        var ackReady = await System.Threading.Tasks.Task.WhenAny(_androidStreamingAckTcs.Task, System.Threading.Tasks.Task.Delay(2000));
        if (ackReady != _androidStreamingAckTcs.Task)
            global::Android.Util.Log.Warn("Shimmer", "Timeout in attesa di ACK/STREAMING (proseguo)");

        // H) Attendi il primo pacchetto dati valido (allinea indici)
        var firstReady = await System.Threading.Tasks.Task.WhenAny(_androidFirstPacketTcs.Task, System.Threading.Tasks.Task.Delay(2000));
        if (firstReady != _androidFirstPacketTcs.Task)
            global::Android.Util.Log.Warn("Shimmer", "Timeout in attesa del primo DATA_PACKET (proseguo)");

        global::Android.Util.Log.Info("Shimmer", $"StartStreaming OK (sensors=0x{sensors:X}, SR={sr})");
    }
    catch (Exception ex)
    {
        global::Android.Util.Log.Error("Shimmer", "StartStreamingAndroidSequenceAsync exception:");
        global::Android.Util.Log.Error("Shimmer", ex.ToString());
        System.Diagnostics.Debug.WriteLine(ex);
        throw;
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

