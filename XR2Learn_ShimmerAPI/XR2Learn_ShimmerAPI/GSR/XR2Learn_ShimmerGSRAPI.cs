// Provides connection management and streaming control for a Shimmer3 device, including async delays for stable communication.

using System.Threading.Tasks;

namespace XR2Learn_ShimmerAPI
{
    public partial class XR2Learn_ShimmerGSR
    {
        /// <summary>
        /// Connects to the Shimmer device
        /// </summary>
        public void Connect()
        {
#if WINDOWS
            if (IsConnected()) return;
            Shimmer.Connect();
#else
            throw new PlatformNotSupportedException("Shimmer GSR non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Disconnects the Shimmer device if connected
        /// </summary>
        public async void Disconnect()
        {
#if WINDOWS
            Shimmer.Disconnect();
            await DelayWork(1000);
            Shimmer.UICallback = null;
#else
            throw new PlatformNotSupportedException("Shimmer GSR non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Tells the Shimmer device to start streaming data
        /// </summary>
        public async void StartStreaming()
        {
#if WINDOWS
            await DelayWork(1000);
            Shimmer.StartStreaming();
#else
            throw new PlatformNotSupportedException("Shimmer GSR non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Tells the Shimmer device to stop streaming data
        /// </summary>
        public async void StopStreaming()
        {
#if WINDOWS
            Shimmer.StopStreaming();
            await DelayWork(1000);
#else
            throw new PlatformNotSupportedException("Shimmer GSR non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Returns the Shimmer device connection status
        /// </summary>
        /// <returns>True if connected, False otherwise</returns>
        public bool IsConnected()
        {
#if WINDOWS
            return Shimmer.IsConnected();
#else
            throw new PlatformNotSupportedException("Shimmer GSR non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Delays work by 't' milliseconds for the current thread
        /// </summary>
        /// <param name="t">Delay in [ms]</param>
        /// <returns>Handle to the async call</returns>
        private async Task DelayWork(int t)
        {
            await Task.Delay(t);
        }
    }
}