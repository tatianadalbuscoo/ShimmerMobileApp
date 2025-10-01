/* 
 * ShimmerSDK_EXG — This partial holds configuration: sampling rate and per-sensor enable flags (including EXG).
 * Pure config (no I/O or streaming). Other partials use these values to build sensor bitmaps
 * and decide which CAL signals are produced.
 */


namespace ShimmerSDK.EXG
{

    /// <summary>
    /// Partial class that manages configuration parameters and sensor enable flags
    /// for a Shimmer EXG device.
    /// </summary>
    public partial class ShimmerSDK_EXG
    {

        // Sampling rate + sensor toggles (LNA, WRA, gyro, mag, BMP180, battery, Ext A6/A7/A15, Exg).
        private double _samplingRate;
        private bool _enableLowNoiseAccelerometer;
        private bool _enableWideRangeAccelerometer;
        private bool _enableGyroscope;
        private bool _enableMagnetometer;
        private bool _enablePressureTemperature;
        private bool _enableBatteryVoltage;
        private bool _enableExtA6;
        private bool _enableExtA7;
        private bool _enableExtA15;
        private bool _enableExg;

        // EXG operating mode (enum: ECG/EMG/etc.).
        private ExgMode _exgMode;


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
        public bool EnableBatteryVoltage 
        { 
            get => _enableBatteryVoltage; 
            set => _enableBatteryVoltage = value; 
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


        /// <summary>
        /// Gets or sets whether EXG acquisition is enabled.
        /// </summary>
        public bool EnableExg { get => _enableExg; set => _enableExg = value; }


        /// <summary>
        /// Gets or sets the EXG operating mode (e.g., ECG, EMG).
        /// </summary>
        public ExgMode ExgMode { get => _exgMode; set => _exgMode = value; }
    }
}
