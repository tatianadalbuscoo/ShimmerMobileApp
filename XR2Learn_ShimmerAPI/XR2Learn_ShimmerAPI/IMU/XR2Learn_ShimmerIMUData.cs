// Represents a data frame containing accelerometer, gyroscope, and magnetometer readings from the Shimmer IMU.

using ShimmerAPI;

namespace XR2Learn_ShimmerAPI.IMU
{
    public class XR2Learn_ShimmerIMUData
    {
        public readonly SensorData TimeStamp;

        public readonly SensorData AccelerometerX;
        public readonly SensorData AccelerometerY;
        public readonly SensorData AccelerometerZ;

        public readonly SensorData GyroscopeX;
        public readonly SensorData GyroscopeY;
        public readonly SensorData GyroscopeZ;

        public readonly SensorData MagnetometerX;
        public readonly SensorData MagnetometerY;
        public readonly SensorData MagnetometerZ;

        public XR2Learn_ShimmerIMUData(
            SensorData timeStamp,
            SensorData accelerometerX, SensorData accelerometerY, SensorData accelerometerZ,
            SensorData gyroscopeX, SensorData gyroscopeY, SensorData gyroscopeZ,
            SensorData magnetometerX, SensorData magnetometerY, SensorData magnetometerZ
        )
        {
            TimeStamp = timeStamp;
            AccelerometerX = accelerometerX;
            AccelerometerY = accelerometerY;
            AccelerometerZ = accelerometerZ;

            GyroscopeX = gyroscopeX;
            GyroscopeY = gyroscopeY;
            GyroscopeZ = gyroscopeZ;

            MagnetometerX = magnetometerX;
            MagnetometerY = magnetometerY;
            MagnetometerZ = magnetometerZ;
        }
    }
}
