// Defines configurable parameters and sensor enabling flags for the Shimmer IMU.

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
        private double _samplingRate;
        private bool _enableAccelerometer;
        private bool _enableGyroscope;
        private bool _enableMagnetometer;

        public double SamplingRate
        {
            get => _samplingRate;
            set => _samplingRate = value;
        }

        public bool EnableAccelerometer
        {
            get => _enableAccelerometer;
            set => _enableAccelerometer = value;
        }

        public bool EnableGyroscope
        {
            get => _enableGyroscope;
            set => _enableGyroscope = value;
        }

        public bool EnableMagnetometer
        {
            get => _enableMagnetometer;
            set => _enableMagnetometer = value;
        }
    }
}
