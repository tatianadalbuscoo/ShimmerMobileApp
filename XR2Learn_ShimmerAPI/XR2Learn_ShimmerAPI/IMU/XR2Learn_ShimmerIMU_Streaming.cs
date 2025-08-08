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
#if WINDOWS
            if (IsConnected()) return;
            shimmer.Connect();
#else
            throw new PlatformNotSupportedException("Shimmer IMU non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Disconnects from the Shimmer IMU device and clears the UI callback.
        /// Includes a short delay to allow disconnection to complete.
        /// </summary>
        public async void Disconnect()
        {
#if WINDOWS
            shimmer.Disconnect();
            await DelayWork(1000);
            shimmer.UICallback = null;
#else
            throw new PlatformNotSupportedException("Shimmer IMU non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Starts data streaming from the Shimmer IMU device after a short delay.
        /// </summary>
        public async void StartStreaming()
        {
#if WINDOWS
            await DelayWork(1000);
            shimmer.StartStreaming();
#else
            throw new PlatformNotSupportedException("Shimmer IMU non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Stops data streaming from the Shimmer IMU device and waits briefly.
        /// </summary>
        public async void StopStreaming()
        {
#if WINDOWS
            shimmer.StopStreaming();
            await DelayWork(1000);
#else
            throw new PlatformNotSupportedException("Shimmer IMU non supportato su questa piattaforma. Funziona solo su Windows.");
#endif
        }

        /// <summary>
        /// Returns whether the Shimmer IMU device is currently connected.
        /// </summary>
        /// <returns>True if connected; otherwise, false.</returns>
        public bool IsConnected()
        {
#if WINDOWS
            return shimmer.IsConnected();
#else
            throw new PlatformNotSupportedException("Shimmer IMU non supportato su questa piattaforma. Funziona solo su Windows.");
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