
namespace XR2Learn_ShimmerAPI.IMU
{

    /// <summary>
    /// Partial class that manages configuration parameters and sensor enable flags
    /// for a Shimmer IMU device.
    /// </summary>
    public partial class XR2Learn_ShimmerIMU
    {

        private double _samplingRate;
        private bool _enableLowNoiseAccelerometer;
        private bool _enableWideRangeAccelerometer;
        private bool _enableGyroscope;
        private bool _enableMagnetometer;
        private bool _enablePressureTemperature;
        private bool _enableBattery;
        private bool _enableExtA6;
        private bool _enableExtA7;
        private bool _enableExtA15;

#if ANDROID
private string? _endpointMac;
private string? _deviceId;
#endif


        /// <summary>
        /// Gets or sets the sampling rate in Hz for the Shimmer device.
        /// </summary>
        public double SamplingRate
        {
            get => _samplingRate;
            set => _samplingRate = value;
        }

        /// <summary>
        /// Gets or sets whether the low-noise accelerometer is enabled.
        /// </summary>
        public bool EnableLowNoiseAccelerometer
        {
            get => _enableLowNoiseAccelerometer;
            set => _enableLowNoiseAccelerometer = value;
        }

        /// <summary>
        /// Gets or sets whether the wide-range accelerometer is enabled.
        /// </summary>
        public bool EnableWideRangeAccelerometer
        {
            get => _enableWideRangeAccelerometer;
            set => _enableWideRangeAccelerometer = value;
        }

        /// <summary>
        /// Gets or sets whether the gyroscope is enabled.
        /// </summary>
        public bool EnableGyroscope
        {
            get => _enableGyroscope;
            set => _enableGyroscope = value;
        }

        /// <summary>
        /// Gets or sets whether the magnetometer is enabled.
        /// </summary>
        public bool EnableMagnetometer
        {
            get => _enableMagnetometer;
            set => _enableMagnetometer = value;
        }

        /// <summary>
        /// Gets or sets whether the BMP180 pressure and temperature sensors are enabled.
        /// </summary>
        public bool EnablePressureTemperature
        {
            get => _enablePressureTemperature;
            set => _enablePressureTemperature = value;
        }

        /// <summary>
        /// Gets or sets whether battery voltage monitoring is enabled.
        /// </summary>
        public bool EnableBattery
        {
            get => _enableBattery;
            set => _enableBattery = value;
        }

        /// <summary>
        /// Gets or sets whether the external ADC channel A6 is enabled.
        /// </summary>
        public bool EnableExtA6
        {
            get => _enableExtA6;
            set => _enableExtA6 = value;
        }

        /// <summary>
        /// Gets or sets whether the external ADC channel A7 is enabled.
        /// </summary>
        public bool EnableExtA7
        {
            get => _enableExtA7;
            set => _enableExtA7 = value;
        }

        /// <summary>
        /// Gets or sets whether the external ADC channel A15 is enabled.
        /// </summary>
        public bool EnableExtA15
        {
            get => _enableExtA15;
            set => _enableExtA15 = value;
        }

    }
}
