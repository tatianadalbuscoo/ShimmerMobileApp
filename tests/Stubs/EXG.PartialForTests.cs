// Partial che completa i campi/proprietà usati da ShimmerSDK_EXG.cs
#nullable enable
namespace ShimmerSDK.EXG
{
    public partial class ShimmerSDK_EXG
    {
        internal double _samplingRate;
        public double SamplingRate { get => _samplingRate; set => _samplingRate = value; }

        internal bool _enableLowNoiseAccelerometer;
        internal bool _enableWideRangeAccelerometer;
        internal bool _enableGyroscope;
        internal bool _enableMagnetometer;
        internal bool _enablePressureTemperature;
        internal bool _enableBatteryVoltage;
        internal bool _enableExtA6;
        internal bool _enableExtA7;
        internal bool _enableExtA15;
        internal bool _enableExg;
        internal ExgMode _exgMode;
    }

    // Snapshot molto semplice: utile per assertare che l’evento arrivi
    public class ShimmerSDK_EXGData
    {
        public object?[] Values { get; }
        public ShimmerSDK_EXGData(params object?[] values) => Values = values;
    }
}
