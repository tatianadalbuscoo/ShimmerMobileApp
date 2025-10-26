#if TEST_STUBS
#nullable enable
namespace ShimmerSDK.EXG
{
    public partial class ShimmerSDK_EXG
    {
        // Intenzionalmente vuoto: i campi/proprietà esistono già nella partial originale.
        // Non ripetere _samplingRate, _enable*, SamplingRate, ecc.
    }

    // Snapshot molto semplice: utile per assertare che l’evento arrivi.
    public class ShimmerSDK_EXGData
    {
        public object?[] Values { get; }
        public ShimmerSDK_EXGData(params object?[] values) => Values = values;
    }
}
#endif
