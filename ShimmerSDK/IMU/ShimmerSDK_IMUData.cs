/* 
 * ShimmerSDK_IMUData — immutable snapshot of one IMU frame.
 * Includes timestamp, LNA/WRA accel, gyro, mag, BMP180 (temperature/pressure), battery, Ext A6/A7/A15.
 * Types: SensorData on Windows/Android; object-only on iOS/macOS.
 */


#if WINDOWS || ANDROID
using ShimmerAPI;
#endif


namespace ShimmerSDK.IMU
{

    /// <summary>
    /// Represents a full sensor data frame acquired from a Shimmer IMU device.
    /// Contains readings from low-noise accelerometer, wide-range accelerometer, 
    /// gyroscope, magnetometer,
    /// BMP180 temperature and pressure sensors,
    /// battery voltage, and external ADCs.
    /// </summary>
    public class ShimmerSDK_IMUData
    {

#if WINDOWS || ANDROID

        // Timestamp
        public readonly SensorData? TimeStamp;

        // Low-noise accelerometer
        public readonly SensorData? LowNoiseAccelerometerX;
        public readonly SensorData? LowNoiseAccelerometerY;
        public readonly SensorData? LowNoiseAccelerometerZ;

        // Wide-range accelerometer
        public readonly SensorData? WideRangeAccelerometerX;
        public readonly SensorData? WideRangeAccelerometerY;
        public readonly SensorData? WideRangeAccelerometerZ;

        // Gyroscope
        public readonly SensorData? GyroscopeX;
        public readonly SensorData? GyroscopeY;
        public readonly SensorData? GyroscopeZ;

        // Magnetometer
        public readonly SensorData? MagnetometerX;
        public readonly SensorData? MagnetometerY;
        public readonly SensorData? MagnetometerZ;

        // Pressure and Temperature
        public readonly SensorData? Pressure_BMP180;
        public readonly SensorData? Temperature_BMP180;

        // Battery
        public readonly SensorData? BatteryVoltage;

        // External ADCs
        public readonly SensorData? ExtADC_A6;
        public readonly SensorData? ExtADC_A7;
        public readonly SensorData? ExtADC_A15;


        /// <summary>
        /// Constructs a full Shimmer IMU data frame with all sensor channels.
        /// </summary>
        public ShimmerSDK_IMUData(
            SensorData? timeStamp,
            SensorData? accelerometerX, SensorData? accelerometerY, SensorData? accelerometerZ,
            SensorData? wideAccelerometerX, SensorData? wideAccelerometerY, SensorData? wideAccelerometerZ,
            SensorData? gyroscopeX, SensorData? gyroscopeY, SensorData? gyroscopeZ,
            SensorData? magnetometerX, SensorData? magnetometerY, SensorData? magnetometerZ,
            SensorData? temperatureBMP180, SensorData? pressureBMP180,
            SensorData? batteryVoltage,
            SensorData? extADC_A6, SensorData? extADC_A7, SensorData? extADC_A15
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

#elif IOS || MACCATALYST

        // Timestamp
        public readonly object? TimeStamp;

        // Low-noise accelerometer
        public readonly object? LowNoiseAccelerometerX;
        public readonly object? LowNoiseAccelerometerY;
        public readonly object? LowNoiseAccelerometerZ;

        // Wide-range accelerometer
        public readonly object? WideRangeAccelerometerX;
        public readonly object? WideRangeAccelerometerY;
        public readonly object? WideRangeAccelerometerZ;

        // Gyroscope
        public readonly object? GyroscopeX;
        public readonly object? GyroscopeY;
        public readonly object? GyroscopeZ;

        // Magnetometer
        public readonly object? MagnetometerX;
        public readonly object? MagnetometerY;
        public readonly object? MagnetometerZ;

        // Pressure and Temperature
        public readonly object? Pressure_BMP180;
        public readonly object? Temperature_BMP180;

        // Battery
        public readonly object? BatteryVoltage;

        // External ADCs
        public readonly object? ExtADC_A6;
        public readonly object? ExtADC_A7;
        public readonly object? ExtADC_A15;


        /// <summary>
        /// Constructs a full Shimmer IMU data frame with all sensor channels.
        /// </summary>
        public ShimmerSDK_IMUData(
            object? timeStamp = null,
            object? accelerometerX = null, object? accelerometerY = null, object? accelerometerZ = null,
            object? wideAccelerometerX = null, object? wideAccelerometerY = null, object? wideAccelerometerZ = null,
            object? gyroscopeX = null, object? gyroscopeY = null, object? gyroscopeZ = null,
            object? magnetometerX = null, object? magnetometerY = null, object? magnetometerZ = null,
            object? temperatureBMP180 = null, object? pressureBMP180 = null,
            object? batteryVoltage = null,
            object? extADC_A6 = null, object? extADC_A7 = null, object? extADC_A15 = null
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

            Temperature_BMP180 = temperatureBMP180;
            Pressure_BMP180 = pressureBMP180;

            BatteryVoltage = batteryVoltage;

            ExtADC_A6 = extADC_A6;
            ExtADC_A7 = extADC_A7;
            ExtADC_A15 = extADC_A15;
        }

#endif

    }
}
