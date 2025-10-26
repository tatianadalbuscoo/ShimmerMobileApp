#if TEST_STUBS
#nullable enable
namespace ShimmerSDK.IMU
{
    public partial class ShimmerSDK_IMU
    {
        // Intenzionalmente vuoto: i campi/proprietà reali sono nella partial originale.
        // Non ridefinire _samplingRate, _enable*, SamplingRate, ecc.
    }

    // Snapshot molto semplice: utile per assertare che l’evento arrivi.
    public class ShimmerSDK_IMUData
    {
        public object?[] Values { get; }
        public ShimmerSDK_IMUData(params object?[] values) => Values = values;
    }
}
#endif
