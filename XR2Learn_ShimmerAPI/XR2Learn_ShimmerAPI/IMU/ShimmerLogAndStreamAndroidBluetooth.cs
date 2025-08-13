#if ANDROID
using System;

namespace XR2Learn_ShimmerAPI.IMU
{
    // Stub minimale per far compilare Android
    // Implementazione reale del Bluetooth può venire in seguito.
    public class ShimmerLogAndStreamAndroidBluetooth
    {
        // Parametri essenziali passati dal chiamante
        public string DeviceId { get; }
        public string MacAddress { get; }
        public double SamplingRate { get; }
        public int AccelRange { get; }
        public int GsrRange { get; }
        public bool EnableLowPowerAccel { get; }
        public bool EnableLowPowerGyro  { get; }
        public bool EnableLowPowerMag   { get; }
        public int GyroRange { get; }
        public int MagRange  { get; }
        public byte[]? Exg1 { get; }
        public byte[]? Exg2 { get; }
        public bool InternalExpPower { get; }

        // Semplice stato interno per soddisfare IsConnected/Start/Stop
        private bool _connected = false;
        private bool _streaming = false;

        // Lato chiamante si aspetta di potersi sottoscrivere
        public event EventHandler? UICallback;

        public ShimmerLogAndStreamAndroidBluetooth(
            string devId,
            string macAddress,
            double samplingRate,
            int accelRange,
            int gsrRange,
            bool enableLowPowerAccel,
            bool enableLowPowerGyro,
            bool enableLowPowerMag,
            int gyroRange,
            int magRange,
            byte[]? exg1,
            byte[]? exg2,
            bool internalExpPower)
        {
            DeviceId = devId;
            MacAddress = macAddress;
            SamplingRate = samplingRate;
            AccelRange = accelRange;
            GsrRange = gsrRange;
            EnableLowPowerAccel = enableLowPowerAccel;
            EnableLowPowerGyro  = enableLowPowerGyro;
            EnableLowPowerMag   = enableLowPowerMag;
            GyroRange = gyroRange;
            MagRange  = magRange;
            Exg1 = exg1;
            Exg2 = exg2;
            InternalExpPower = internalExpPower;
        }

        public void Connect()
        {
            // TODO: implementazione reale RFCOMM/BT
            _connected = true;
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public void Disconnect()
        {
            _streaming = false;
            _connected = false;
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public bool IsConnected() => _connected;

        public void StartStreaming()
        {
            if (!_connected) return;
            _streaming = true;
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public void StopStreaming()
        {
            _streaming = false;
            UICallback?.Invoke(this, EventArgs.Empty);
        }
    }
}
#endif
