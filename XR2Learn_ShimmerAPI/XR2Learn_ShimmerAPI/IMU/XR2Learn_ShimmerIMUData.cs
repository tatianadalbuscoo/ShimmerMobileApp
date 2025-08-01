
using ShimmerAPI;

namespace XR2Learn_ShimmerAPI.IMU
{

    /// <summary>
    /// Represents a full sensor data frame acquired from a Shimmer IMU device.
    /// Contains readings from low-noise accelerometer, gyroscope, magnetometer,
    /// wide-range accelerometer, BMP180 temperature and pressure sensors,
    /// battery voltage, and external ADCs.
    /// </summary>
    public class XR2Learn_ShimmerIMUData
    {
        // Timestamp
        public readonly SensorData TimeStamp;

        // Low-noise accelerometer
        public readonly SensorData LowNoiseAccelerometerX;
        public readonly SensorData LowNoiseAccelerometerY;
        public readonly SensorData LowNoiseAccelerometerZ;

        // Wide-range accelerometer
        public readonly SensorData WideRangeAccelerometerX;
        public readonly SensorData WideRangeAccelerometerY;
        public readonly SensorData WideRangeAccelerometerZ;

        // Gyroscope
        public readonly SensorData GyroscopeX;
        public readonly SensorData GyroscopeY;
        public readonly SensorData GyroscopeZ;

        // Magnetometer
        public readonly SensorData MagnetometerX;
        public readonly SensorData MagnetometerY;
        public readonly SensorData MagnetometerZ;

        // Pressure and Temperature
        public readonly SensorData Pressure_BMP180;
        public readonly SensorData Temperature_BMP180;

        // Shimmer Battery
        public readonly SensorData BatteryVoltage;

        // External ADCs
        public readonly SensorData ExtADC_A6;
        public readonly SensorData ExtADC_A7;
        public readonly SensorData ExtADC_A15;

        /// <summary>
        /// Constructs a full Shimmer IMU data frame with all sensor channels.
        /// </summary>
        public XR2Learn_ShimmerIMUData(
            SensorData timeStamp,
            SensorData accelerometerX, SensorData accelerometerY, SensorData accelerometerZ,
            SensorData gyroscopeX, SensorData gyroscopeY, SensorData gyroscopeZ,
            SensorData magnetometerX, SensorData magnetometerY, SensorData magnetometerZ,
            SensorData wideAccelerometerX, SensorData wideAccelerometerY, SensorData wideAccelerometerZ,
            SensorData temperatureBMP180, SensorData pressureBMP180,
            SensorData batteryVoltage,
            SensorData extADC_A6, SensorData extADC_A7, SensorData extADC_A15
        )
        {
            TimeStamp = timeStamp;

            LowNoiseAccelerometerX = accelerometerX;
            LowNoiseAccelerometerY = accelerometerY;
            LowNoiseAccelerometerZ = accelerometerZ;


            WideRangeAccelerometerX = wideAccelerometerX;
            WideRangeAccelerometerY = wideAccelerometerY;
            WideRangeAccelerometerZ = wideAccelerometerZ;

            GyroscopeX = gyroscopeX;
            GyroscopeY = gyroscopeY;
            GyroscopeZ = gyroscopeZ;

            MagnetometerX = magnetometerX;
            MagnetometerY = magnetometerY;
            MagnetometerZ = magnetometerZ;

            Pressure_BMP180 = pressureBMP180;
            Temperature_BMP180 = temperatureBMP180;

            BatteryVoltage = batteryVoltage;

            ExtADC_A6 = extADC_A6;
            ExtADC_A7 = extADC_A7;
            ExtADC_A15 = extADC_A15;

        }
    }
}
