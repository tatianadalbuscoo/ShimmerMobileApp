#if ANDROID
using System;
using System.Threading.Tasks;

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
        // Istanza Android
        private ShimmerLogAndStreamAndroidBluetooth? _shimAnd;

        // --- Bridge fields SOLO per Android (evitano i CS0103) ---
        // Se hai già dei range/flag “veri” nelle Settings, mappali qui.
        private int _accelRange = 0;
        private int _gsrRange = 0;
        private int _gyroRange = 0;
        private int _magRange  = 1;
        private bool _enableLowPowerAccel = false;
        private bool _enableLowPowerGyro  = false;
        private bool _enableLowPowerMag   = false;
        private byte[]? _exg1 = null, _exg2 = null;
        private bool _internalExpPower = false;
        private string _lastStatusMessage = string.Empty;


        // NOTA: usiamo _samplingRate dalle Settings (già esiste), NON _samplingRateHz

        private Task<bool> ConnectInternalAsync(string macOrName, int timeoutMs = 10000)
        {
            try
            {
                _shimAnd = new ShimmerLogAndStreamAndroidBluetooth(
                    devId: _deviceId ?? "Shimmer-Android",
                    macAddress: macOrName,
                    samplingRate: _samplingRate,       // <-- esiste nelle Settings
                    accelRange: _accelRange,
                    gsrRange: _gsrRange,
                    enableLowPowerAccel: _enableLowPowerAccel,
                    enableLowPowerGyro:  _enableLowPowerGyro,
                    enableLowPowerMag:   _enableLowPowerMag,
                    gyroRange: _gyroRange,
                    magRange:  _magRange,
                    exg1: _exg1, exg2: _exg2,
                    internalExpPower: _internalExpPower
                );

                _shimAnd.UICallback += HandleEvent;
                _lastStatusMessage = "Connecting...";
                _shimAnd.Connect(); // apre RFCOMM
                _lastStatusMessage = _shimAnd.IsConnected() ? "Connected" : "Not connected";
                return Task.FromResult(_shimAnd.IsConnected());
            }
            catch (Exception ex)
            {
                _lastStatusMessage = $"Connect failed: {ex.Message}";
                return Task.FromResult(false);
            }
        }

        private Task<bool> StartStreamingInternalAsync()
        {
            try
            {
                if (_shimAnd == null || !_shimAnd.IsConnected())
                    return Task.FromResult(false);

                _shimAnd.StartStreaming();
                _lastStatusMessage = "Streaming started";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _lastStatusMessage = $"StartStreaming failed: {ex.Message}";
                return Task.FromResult(false);
            }
        }

        private Task<bool> StopInternalAsync()
        {
            try
            {
                _shimAnd?.StopStreaming();
                _shimAnd?.Disconnect();
                _shimAnd = null;
                _lastStatusMessage = "Stopped";
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _lastStatusMessage = $"Stop failed: {ex.Message}";
                return Task.FromResult(false);
            }
        }

        // handler minimo per compilare
        private void HandleEvent(object? sender, EventArgs e) { /* opzionale: log */ }
    }
}
#endif
