using System;
using System.Threading.Tasks;

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
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("Windows-only in this wrapper.");
#endif
        }

        public bool IsConnected()
        {
#if WINDOWS
            return shimmer != null && shimmer.IsConnected();
#else
            return false;
#endif
        }
    }
}
