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
    var ok = shimmerAndroid.Connect();
    Android.Util.Log.Info("Shimmer", $"Android Connect() -> {ok}");
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
    await DelayWork(200);
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
    shimmerAndroid?.StartStreaming();
    await DelayWork(100); // opzionale
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
    await DelayWork(100); // opzionale
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