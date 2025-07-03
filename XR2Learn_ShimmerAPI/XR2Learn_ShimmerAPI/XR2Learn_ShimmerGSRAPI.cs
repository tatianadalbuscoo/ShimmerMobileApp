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
            if (IsConnected()) return;
            Shimmer.Connect();
        }

        /// <summary>
        /// Disconnects the Shimmer device if connected
        /// </summary>
        public async void Disconnect()
        {
            Shimmer.Disconnect();
            await DelayWork(1000);
            Shimmer.UICallback = null;
        }

        /// <summary>
        /// Tells the Shimmer device to start streaming data
        /// </summary>
        public async void StartStreaming()
        {
            await DelayWork(1000);
            Shimmer.StartStreaming();
        }

        /// <summary>
        /// Tells the Shimmer device to stop streaming data
        /// </summary>
        public async void StopStreaming()
        {
            Shimmer.StopStreaming();
            await DelayWork(1000);
        }

        /// <summary>
        /// Returns the Shimmer device connection status
        /// </summary>
        /// <returns>True if connected, False otherwise</returns>
        public bool IsConnected()
        {
            return Shimmer.IsConnected();
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
