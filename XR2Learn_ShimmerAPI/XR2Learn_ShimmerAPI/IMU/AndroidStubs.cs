#if ANDROID
#nullable enable
using System;

namespace XR2Learn_ShimmerAPI.IMU
{
    // Range "compat" usati solo per parametri Android
    internal enum ACCEL_RANGE { RANGE_2G = 0, RANGE_4G = 1, RANGE_8G = 2, RANGE_16G = 3 }
    internal enum GSR_RANGE   { RANGE_40K = 0, RANGE_287K = 1, RANGE_1M = 2, RANGE_3M3 = 3 }

    // EventArgs "compat" per gli handler che usi nel partial
    internal sealed class UICallbackEventArgs : EventArgs
    {
        public string Message { get; }
        public object? Payload { get; }
        public UICallbackEventArgs(string message, object? payload = null)
        { Message = message; Payload = payload; }
    }

    internal sealed class SensorSampleRateCalculatedEventArgs : EventArgs
    {
        public double SampleRate { get; }
        public SensorSampleRateCalculatedEventArgs(double sampleRate) => SampleRate = sampleRate;
    }

    internal sealed class ObjectClusterEventArgs : EventArgs
    {
        public object? ObjectCluster { get; }
        public ObjectClusterEventArgs(object? oc) { ObjectCluster = oc; }
    }
}
#endif
