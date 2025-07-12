using ShimmerAPI;

namespace XR2Learn_ShimmerAPI
{
    /// <summary>
    /// Class representing a Shimmer device dataframe
    /// </summary>
    public class XR2Learn_ShimmerGSRData
    {
        #region Instance variables

        /// <summary>
        /// Heart Rate in BPM 
        /// </summary>
        public readonly SensorData TimeStamp;

        /// <summary>
        /// Accelerator X axis Data and Unit
        /// </summary>
        public readonly SensorData AcceleratorX;
        /// <summary>
        /// Accelerator Y axis Data and Unit
        /// </summary>
        public readonly SensorData AcceleratorY;
        /// <summary>
        /// Accelerator Z axis Data and Unit
        /// </summary>
        public readonly SensorData AcceleratorZ;
        /// <summary>
        /// Galvanic Skip Response (GSR) Data and Unit
        /// </summary>
        public readonly SensorData GalvanicSkinResponse;
        /// <summary>
        /// PhotoPlethysmoGram (PPG) Data and Unit
        /// </summary>
        public readonly SensorData PhotoPlethysmoGram;

        /// <summary>
        /// Heart Rate in BPM
        /// </summary>
        public readonly int HeartRate;

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="timeStamp">Heart Rate in BPM </param>
        /// <param name="acceleratorX">Accelerator X axis Data and Unit</param>
        /// <param name="acceleratorY">Accelerator Y axis Data and Unit</param>
        /// <param name="acceleratorZ">Accelerator Z axis Data and Unit</param>
        /// <param name="galvanicSkinResponse">Galvanic Skip Response (GSR) Data and Unit</param>
        /// <param name="photoPlethysmoGram">PhotoPlethysmoGram (PPG) Data and Unit</param>
        /// <param name="HeartRate">Heart Rate in BPM</param>
        public XR2Learn_ShimmerGSRData(SensorData timeStamp, SensorData acceleratorX, SensorData acceleratorY, SensorData acceleratorZ, SensorData galvanicSkinResponse, SensorData photoPlethysmoGram, int HeartRate)
        {
            TimeStamp = timeStamp;
            AcceleratorX = acceleratorX;
            AcceleratorY = acceleratorY;
            AcceleratorZ = acceleratorZ;
            GalvanicSkinResponse = galvanicSkinResponse;
            PhotoPlethysmoGram = photoPlethysmoGram;
            this.HeartRate = HeartRate;
        }
    }
}
