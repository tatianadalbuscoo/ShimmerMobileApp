using System;
using System.Threading.Tasks;


#if ANDROID
 using ShimmerSDK.Android;
#endif

namespace XR2Learn_ShimmerAPI.GSR
{
    /// <summary>
    /// Windows-focused streaming lifecycle (mirrors IMU style)
    /// </summary>
    public partial class XR2Learn_ShimmerEXG
    {
        public void Connect()
        {
#if WINDOWS
            if (IsConnected()) return;
            shimmer.Connect();

            var sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
            shimmer.WriteSamplingRate(sr);
            System.Threading.Thread.Sleep(150);

            shimmer.WriteSensors(_winEnabledSensors);
            System.Threading.Thread.Sleep(200);

            shimmer.Inquiry();
            System.Threading.Thread.Sleep(200);
#elif ANDROID
            if (shimmerAndroid == null)
                throw new InvalidOperationException("ConfigureAndroid non chiamata");

            shimmerAndroid.Connect();
            global::Android.Util.Log.Info("Shimmer-EXG", $"Android Connect() -> {shimmerAndroid.IsConnected()}");
            return;
#elif MACCATALYST || IOS
    return;
#else
            throw new PlatformNotSupportedException("Windows-only in this wrapper.");
#endif
        }

        public async void Disconnect()
        {
#if WINDOWS
            shimmer.Disconnect();
            await Task.Delay(1000);
            shimmer.UICallback = null;
#elif ANDROID
            shimmerAndroid?.StopStreaming();
            shimmerAndroid?.Disconnect();
            await DelayWork(1000);
            return;
#elif MACCATALYST || IOS
    await DisconnectMacAsync();
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("Windows-only in this wrapper.");
#endif
        }

        public async void StartStreaming()
        {
#if WINDOWS
            await Task.Delay(1000);
            shimmer.StartStreaming();
#elif ANDROID
            if (shimmerAndroid == null)
                throw new InvalidOperationException("ConfigureAndroid non chiamata");

            // Sequenza speculare alla IMU (senza TCS/ACK, semplice e robusta)
            shimmerAndroid.StopStreaming();
            await DelayWork(150);

            shimmerAndroid.WriteSensors(0);
            await DelayWork(150);

            int sr = (int)Math.Round(_samplingRate <= 0 ? 51.2 : _samplingRate);
            shimmerAndroid.WriteSamplingRate(sr);
            await DelayWork(180);

            // Allinea ranges/power (come su IMU)
            shimmerAndroid.WriteAccelRange(0);
            shimmerAndroid.WriteGyroRange(0);
            shimmerAndroid.WriteGSRRange(0);
            shimmerAndroid.SetLowPowerAccel(false);
            shimmerAndroid.SetLowPowerGyro(false);
            shimmerAndroid.WriteInternalExpPower(0);
            await DelayWork(150);

            // Riattiva sensori selezionati (inclusi EXG1/EXG2 se abilitati in ConfigureAndroid)
            shimmerAndroid.WriteSensors(_androidEnabledSensors);
            await DelayWork(180);

            // Aggiorna mappa nomi/indici e calibrazioni
            shimmerAndroid.Inquiry();
            await DelayWork(350);

            shimmerAndroid.ReadCalibrationParameters("All");
            await DelayWork(250);

            shimmerAndroid.StartStreaming();
            global::Android.Util.Log.Info("Shimmer-EXG", $"StartStreaming OK (sensors=0x{_androidEnabledSensors:X}, SR={sr})");
            return;
#elif MACCATALYST || IOS
    await StartStreamingMacAsync();
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("Windows-only in this wrapper.");
#endif
        }

        public async void StopStreaming()
        {
#if WINDOWS
            shimmer.StopStreaming();
            await Task.Delay(1000);
#elif ANDROID
            shimmerAndroid?.StopStreaming();
            await DelayWork(1000);
            return;
#elif MACCATALYST || IOS
    await StopStreamingMacAsync();
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("Windows-only in this wrapper.");
#endif
        }

        public bool IsConnected()
        {
#if WINDOWS
            return shimmer != null && shimmer.IsConnected();
#elif ANDROID
            return shimmerAndroid != null && shimmerAndroid.IsConnected();
#elif MACCATALYST || IOS
    return IsConnectedMac();
#else
            return false;
#endif
        }

#if ANDROID
        // piccolo helper per uniformare i Delay come nella IMU
        private Task DelayWork(int ms) => Task.Delay(ms);
#endif
    }
}
