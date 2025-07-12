// Provides methods to control the Shimmer IMU connection and streaming lifecycle.

using System.Threading.Tasks;

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
        public void Connect()
        {
            if (IsConnected()) return;
            shimmer.Connect();
        }

        public async void Disconnect()
        {
            shimmer.Disconnect();
            await DelayWork(1000);
            shimmer.UICallback = null;
        }

        public async void StartStreaming()
        {
            await DelayWork(1000);
            shimmer.StartStreaming();
        }

        public async void StopStreaming()
        {
            shimmer.StopStreaming();
            await DelayWork(1000);
        }

        public bool IsConnected()
        {
            return shimmer.IsConnected();
        }

        private async Task DelayWork(int t)
        {
            await Task.Delay(t);
        }
    }
}
