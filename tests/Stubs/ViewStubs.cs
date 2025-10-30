/*
 * ViewStubs.cs
 * Purpose: Test-only MAUI view stubs to exercise navigation and page wiring
 *          without relying on real UI components.
 */


using ShimmerInterface.Models;


namespace ShimmerInterface.Views
{

    /// <summary>
    /// Lightweight page shown while a Shimmer device is initializing or connecting.
    /// Captures the device and a completion source to signal when loading completes.
    /// </summary>
    public sealed class LoadingPage : Page
    {

        /// <summary>
        /// The Shimmer device associated with this loading operation.
        /// </summary>
        public ShimmerDevice Device { get; }


        /// <summary>
        /// A completion source that tests can await to observe completion/cancellation.
        /// </summary>
        public TaskCompletionSource<object?> Tcs { get; }


        /// <summary>
        /// Initializes a new <see cref="LoadingPage"/> for the specified device.
        /// </summary>
        /// <param name="device">The device being initialized.</param>
        /// <param name="tcs">Completion source to signal when loading is done.</param>
        public LoadingPage(ShimmerDevice device, TaskCompletionSource<object?> tcs)
        {
            Device = device; Tcs = tcs;
        }
    }


    /// <summary>
    /// Minimal data page stub that wraps either an IMU or EXG Shimmer SUT plus its model.
    /// Used by tests to validate navigation and page construction without rendering charts.
    /// </summary>
    public sealed class DataPage : Page
    {

        /// <summary>
        /// The Shimmer SUT instance (either <c>ShimmerSDK_IMU</c> or <c>ShimmerSDK_EXG</c>).
        /// </summary>
        public object Shimmer { get; }


        /// <summary>
        /// The logical device model used by the UI.
        /// </summary>
        public ShimmerDevice Device { get; }


        /// <summary>
        /// Creates a data page bound to an IMU Shimmer SUT.
        /// </summary>
        /// <param name="imu">The IMU SUT instance.</param>
        /// <param name="device">The device model.</param>
        public DataPage(ShimmerSDK.IMU.ShimmerSDK_IMU imu, ShimmerDevice device) 
        { 
            Shimmer = imu; 
            Device = device; 
        }


        /// <summary>
        /// Creates a data page bound to an EXG Shimmer SUT.
        /// </summary>
        /// <param name="exg">The EXG SUT instance.</param>
        /// <param name="device">The device model.</param>
        public DataPage(ShimmerSDK.EXG.ShimmerSDK_EXG exg, ShimmerDevice device) 
        { 
            Shimmer = exg; 
            Device = device; 
        }
    }
}
