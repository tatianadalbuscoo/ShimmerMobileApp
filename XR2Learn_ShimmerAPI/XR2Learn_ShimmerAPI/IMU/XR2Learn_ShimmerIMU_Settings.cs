// Defines configurable parameters and sensor enabling flags for the Shimmer IMU.

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
        private double _samplingRate;
        private bool _enableGyroscope;
        private bool _enableMagnetometer;
        private bool _enableLowNoiseAccelerometer;
        private bool _enableWideRangeAccelerometer;
        private bool _enablePressureTemperature;
        private bool _enableBattery;
        private bool _enableExtA6;
        private bool _enableExtA7;
        private bool _enableExtA15;

        public bool EnableExtA6
        {
            get => _enableExtA6;
            set => _enableExtA6 = value;
        }

        public bool EnableExtA7
        {
            get => _enableExtA7;
            set => _enableExtA7 = value;
        }

        public bool EnableExtA15
        {
            get => _enableExtA15;
            set => _enableExtA15 = value;
        }

        public double SamplingRate
        {
            get => _samplingRate;
            set => _samplingRate = value;
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

        public bool EnableLowNoiseAccelerometer
        {
            get => _enableLowNoiseAccelerometer;
            set => _enableLowNoiseAccelerometer = value;
        }

        public bool EnableWideRangeAccelerometer
        {
            get => _enableWideRangeAccelerometer;
            set => _enableWideRangeAccelerometer = value;
        }

        public bool EnablePressureTemperature
        {
            get => _enablePressureTemperature;
            set => _enablePressureTemperature = value;
        }

        public bool EnableBattery
        {
            get => _enableBattery;
            set => _enableBattery = value;
        }

    }
}
