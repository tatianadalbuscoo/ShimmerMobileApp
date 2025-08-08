// Data container class for a single frame of Shimmer3 sensor readings (accelerometer, GSR, PPG, and heart rate).

#if WINDOWS
using ShimmerAPI;
#endif

namespace XR2Learn_ShimmerAPI
{
    /// <summary>
    /// Class representing a Shimmer device dataframe
    /// </summary>
    public class XR2Learn_ShimmerGSRData
    {
        #region Instance variables

#if WINDOWS
        /// <summary>
        /// TimeStamp Data and Unit
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

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="timeStamp">TimeStamp Data and Unit</param>
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
#else
        // Stub version per piattaforme non-Windows
        public readonly object TimeStamp;
        public readonly object AcceleratorX;
        public readonly object AcceleratorY;
        public readonly object AcceleratorZ;
        public readonly object GalvanicSkinResponse;
        public readonly object PhotoPlethysmoGram;
        public readonly int HeartRate;

        /// <summary>
        /// Stub constructor per piattaforme non-Windows
        /// </summary>
        public XR2Learn_ShimmerGSRData(object timeStamp = null, object acceleratorX = null, object acceleratorY = null, object acceleratorZ = null, object galvanicSkinResponse = null, object photoPlethysmoGram = null, int HeartRate = 0)
        {
            // Questa versione non fa nulla - è solo per permettere la compilazione
        }
#endif

        #endregion
    }
}