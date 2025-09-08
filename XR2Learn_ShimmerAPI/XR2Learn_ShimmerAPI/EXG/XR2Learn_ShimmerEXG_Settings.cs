namespace XR2Learn_ShimmerAPI.GSR
{
    /// <summary>
    /// Partial class with configuration/flags for GSR device sensors.
    /// </summary>
    public partial class XR2Learn_ShimmerEXG
    {
        private double _samplingRate;

        // Enable flags
        private bool _enableLowNoiseAccelerometer;
        private bool _enableWideRangeAccelerometer;
        private bool _enableGyroscope;
        private bool _enableMagnetometer;
        private bool _enablePressureTemperature;
        private bool _enableBatteryVoltage;
        private bool _enableExtA6;
        private bool _enableExtA7;
        private bool _enableExtA15;

        // ExG
        private bool _enableExg;
        private ExgMode _exgMode;

#if ANDROID
private string? _endpointMac;
private string? _deviceId;
#endif


        public double SamplingRate
        {
            get => _samplingRate;
            set => _samplingRate = value;
        }

        public bool EnableLowNoiseAccelerometer { get => _enableLowNoiseAccelerometer; set => _enableLowNoiseAccelerometer = value; }
        public bool EnableWideRangeAccelerometer { get => _enableWideRangeAccelerometer; set => _enableWideRangeAccelerometer = value; }
        public bool EnableGyroscope { get => _enableGyroscope; set => _enableGyroscope = value; }
        public bool EnableMagnetometer { get => _enableMagnetometer; set => _enableMagnetometer = value; }
        public bool EnablePressureTemperature { get => _enablePressureTemperature; set => _enablePressureTemperature = value; }
        public bool EnableBatteryVoltage { get => _enableBatteryVoltage; set => _enableBatteryVoltage = value; }
        public bool EnableExtA6 { get => _enableExtA6; set => _enableExtA6 = value; }
        public bool EnableExtA7 { get => _enableExtA7; set => _enableExtA7 = value; }
        public bool EnableExtA15 { get => _enableExtA15; set => _enableExtA15 = value; }

        public bool EnableExg { get => _enableExg; set => _enableExg = value; }
        public ExgMode ExgMode { get => _exgMode; set => _exgMode = value; }
    }
}
