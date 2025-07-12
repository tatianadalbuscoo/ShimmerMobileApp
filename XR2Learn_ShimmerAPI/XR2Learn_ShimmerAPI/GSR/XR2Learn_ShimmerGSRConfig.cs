// Defines configurable parameters and sensor activation flags for the Shimmer3 device used in biosignal analysis.

using ShimmerAPI;

namespace XR2Learn_ShimmerAPI
{
    public partial class XR2Learn_ShimmerGSR
    {
        #region Default values

        public static readonly int      DefaultNumberOfHeartBeatsToAverage = 10;
        public static readonly int      DefaultTrainingPeriodPPG = 10;
        public static readonly double   DefaultLowPassFilterCutoff = 5;
        public static readonly double   DefaultHighPassFilterCutoff = 0.5;
        public static readonly double   DefaultSamplingRate = 10;
        public static readonly bool     DefaultEnableAccelerator = true;
        public static readonly bool     DefaultEnableGSR = true;
        public static readonly bool     DefaultEnablePPG = true;

        #endregion

        #region Properties

        /// <summary>
        /// Number of Heart Beats required to calculate an average
        /// </summary>
        public int NumberOfHeartBeatsToAverage
        {
            get { return _numberOfHeartBeatsToAverage; }
            set { _numberOfHeartBeatsToAverage = value; }
        }
        private int _numberOfHeartBeatsToAverage;

        /// <summary>
        /// PPG algorithm training period in [s]
        /// </summary>
        public int TrainingPeriodPPG
        {
            get { return _trainingPeriodPPG; }
            set { _trainingPeriodPPG = value; }
        }
        private int _trainingPeriodPPG;

        /// <summary>
        /// PPG low-pass filter cutoff in [Hz]
        /// </summary>
        public double LowPassFilterCutoff
        {
            get { return _LowPassFilterCutoff; }
            set { _LowPassFilterCutoff = value; }
        }
        private double _LowPassFilterCutoff;

        /// <summary>
        /// PPG high-pass filter cutoff in [Hz]
        /// </summary>
        public double HighPassFilterCutoff
        {
            get { return _HighPassFilterCutoff; }
            set { _HighPassFilterCutoff = value; }
        }
        private double _HighPassFilterCutoff;

        /// <summary>
        /// Shimmer device internal sampling rate in [Hz]
        /// </summary>
        public double SamplingRate
        {
            get { return _samplingRate; }
            set { _samplingRate = value; }
        }
        private double _samplingRate;

        /// <summary>
        /// Flag to enable/disable the Accelerator sensor
        /// </summary>
        public bool EnableAccelerator
        {
            get { return _enableAccelerator == (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL; }
            set { _enableAccelerator = value ? (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL : 0; }
        }
        private int _enableAccelerator;

        /// <summary>
        /// Flag to enable/disable the GSR sensor
        /// </summary>
        public bool EnableGSR
        {
            get { return _enableGSR == (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_GSR; }
            set { _enableGSR = value ? (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_GSR : 0; }
        }
        private int _enableGSR;

        /// <summary>
        /// Flag to enable/disable the PPG sensor
        /// </summary>
        public bool EnablePPG
        {
            get { return _enablePPG == (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_INT_A13; }
            set { _enablePPG = value ? (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_INT_A13 : 0; }
        }
        private int _enablePPG;

        #endregion
    }
}
