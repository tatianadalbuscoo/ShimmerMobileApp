// tests/Stubs/ViewStubs.cs
using System.Threading.Tasks;
using ShimmerInterface.Models;
using Microsoft.Maui.Controls;

namespace ShimmerInterface.Views
{
    public sealed class LoadingPage : Page
    {
        public ShimmerDevice Device { get; }
        public TaskCompletionSource<object?> Tcs { get; }
        public LoadingPage(ShimmerDevice device, TaskCompletionSource<object?> tcs)
        {
            Device = device; Tcs = tcs;
        }
    }

    public sealed class DataPage : Page
    {
        public object Shimmer { get; }
        public ShimmerDevice Device { get; }
        public DataPage(ShimmerSDK.IMU.ShimmerSDK_IMU imu, ShimmerDevice device) { Shimmer = imu; Device = device; }
        public DataPage(ShimmerSDK.EXG.ShimmerSDK_EXG exg, ShimmerDevice device) { Shimmer = exg; Device = device; }
    }
}
