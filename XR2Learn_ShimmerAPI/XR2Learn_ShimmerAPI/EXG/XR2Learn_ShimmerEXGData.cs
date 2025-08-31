#if WINDOWS
using ShimmerAPI;
#endif

namespace XR2Learn_ShimmerAPI.GSR
{
    public class XR2Learn_ShimmerEXGData
    {
#if WINDOWS
        public readonly SensorData TimeStamp;

        public readonly SensorData LowNoiseAccelerometerX;
        public readonly SensorData LowNoiseAccelerometerY;
        public readonly SensorData LowNoiseAccelerometerZ;

        public readonly SensorData WideRangeAccelerometerX;
        public readonly SensorData WideRangeAccelerometerY;
        public readonly SensorData WideRangeAccelerometerZ;

        public readonly SensorData GyroscopeX;
        public readonly SensorData GyroscopeY;
        public readonly SensorData GyroscopeZ;

        public readonly SensorData MagnetometerX;
        public readonly SensorData MagnetometerY;
        public readonly SensorData MagnetometerZ;

        public readonly SensorData Temperature_BMP180;
        public readonly SensorData Pressure_BMP180;

        public readonly SensorData BatteryVoltage;

        public readonly SensorData ExtADC_A6;
        public readonly SensorData ExtADC_A7;
        public readonly SensorData ExtADC_A15;

        public readonly SensorData ExgCh1;     
        public readonly SensorData ExgCh2;     
        public readonly SensorData ExgRespiration;

        public XR2Learn_ShimmerEXGData(
            SensorData timeStamp,
            SensorData accelerometerX, SensorData accelerometerY, SensorData accelerometerZ,
            SensorData wideAccelerometerX, SensorData wideAccelerometerY, SensorData wideAccelerometerZ,
            SensorData gyroscopeX, SensorData gyroscopeY, SensorData gyroscopeZ,
            SensorData magnetometerX, SensorData magnetometerY, SensorData magnetometerZ,
            SensorData temperatureBMP180, SensorData pressureBMP180,
            SensorData batteryVoltage,
            SensorData extADC_A6, SensorData extADC_A7, SensorData extADC_A15,
            SensorData exgCh1, SensorData exgCh2, SensorData exgRespiration = null
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

            ExgCh1 = exgCh1;
            ExgCh2 = exgCh2;
            ExgRespiration = exgRespiration;
        }
#else
        public XR2Learn_ShimmerEXGData() { }
#endif
    }
}
