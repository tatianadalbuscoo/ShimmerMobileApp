// Partial che completa i campi/proprietà usati da ShimmerSDK_IMU.cs
#nullable enable
namespace ShimmerSDK.IMU
{
    public partial class ShimmerSDK_IMU
    {
        internal double _samplingRate;
        public double SamplingRate { get => _samplingRate; set => _samplingRate = value; }

        internal bool _enableLowNoiseAccelerometer;
        internal bool _enableWideRangeAccelerometer;
        internal bool _enableGyroscope;
        internal bool _enableMagnetometer;
        internal bool _enablePressureTemperature;
        internal bool _enableBattery;
        internal bool _enableExtA6;
        internal bool _enableExtA7;
        internal bool _enableExtA15;
    }

    // Snapshot molto semplice: utile per assertare che l’evento arrivi
    public class ShimmerSDK_IMUData
    {
        public object?[] Values { get; }
        public ShimmerSDK_IMUData(params object?[] values) => Values = values;
    }
}
