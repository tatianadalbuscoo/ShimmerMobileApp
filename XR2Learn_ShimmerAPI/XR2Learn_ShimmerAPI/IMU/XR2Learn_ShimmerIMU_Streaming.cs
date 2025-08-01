
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
            if (IsConnected()) return;
            shimmer.Connect();
        }

        /// <summary>
        /// Disconnects from the Shimmer IMU device and clears the UI callback.
        /// Includes a short delay to allow disconnection to complete.
        /// </summary>
        public async void Disconnect()
        {
            shimmer.Disconnect();
            await DelayWork(1000);
            shimmer.UICallback = null;
        }

        /// <summary>
        /// Starts data streaming from the Shimmer IMU device after a short delay.
        /// </summary>
        public async void StartStreaming()
        {
            await DelayWork(1000);
            shimmer.StartStreaming();
        }

        /// <summary>
        /// Stops data streaming from the Shimmer IMU device and waits briefly.
        /// </summary>
        public async void StopStreaming()
        {
            shimmer.StopStreaming();
            await DelayWork(1000);
        }

        /// <summary>
        /// Returns whether the Shimmer IMU device is currently connected.
        /// </summary>
        /// <returns>True if connected; otherwise, false.</returns>
        public bool IsConnected()
        {
            return shimmer.IsConnected();
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
