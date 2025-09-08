#if WINDOWS || ANDROID
using ShimmerAPI;
#endif

namespace XR2Learn_ShimmerAPI.GSR
{
    public class XR2Learn_ShimmerEXGData
    {
#if WINDOWS || ANDROID
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

        // BMP180
        public readonly SensorData Temperature_BMP180;
        public readonly SensorData Pressure_BMP180;

        // Battery
        public readonly SensorData BatteryVoltage;

        // External ADCs
        public readonly SensorData ExtADC_A6;
        public readonly SensorData ExtADC_A7;
        public readonly SensorData ExtADC_A15;

        // --- EXG (schema a 4 canali) ---
        public readonly SensorData Exg1Ch1;   // EXG1_CH1
        public readonly SensorData Exg1Ch2;   // EXG1_CH2
        public readonly SensorData Exg2Ch1;   // EXG2_CH1
        public readonly SensorData Exg2Ch2;   // EXG2_CH2
        public readonly SensorData ExgRespiration;

        // --- Alias legacy (compat) ---
        public readonly SensorData ExgCh1;    // alias di Exg1Ch1
        public readonly SensorData ExgCh2;    // alias di Exg1Ch2

        // === Costruttore NUOVO (4 canali EXG) ===
        public XR2Learn_ShimmerEXGData(
            SensorData timeStamp,
            SensorData accelerometerX, SensorData accelerometerY, SensorData accelerometerZ,
            SensorData wideAccelerometerX, SensorData wideAccelerometerY, SensorData wideAccelerometerZ,
            SensorData gyroscopeX, SensorData gyroscopeY, SensorData gyroscopeZ,
            SensorData magnetometerX, SensorData magnetometerY, SensorData magnetometerZ,
            SensorData temperatureBMP180, SensorData pressureBMP180,
            SensorData batteryVoltage,
            SensorData extADC_A6, SensorData extADC_A7, SensorData extADC_A15,
            // EXG
            SensorData exg1Ch1, SensorData exg1Ch2,
            SensorData exg2Ch1, SensorData exg2Ch2,
            // opzionale
            SensorData exgRespiration = null
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

            Exg1Ch1 = exg1Ch1;
            Exg1Ch2 = exg1Ch2;
            Exg2Ch1 = exg2Ch1;
            Exg2Ch2 = exg2Ch2;
            ExgRespiration = exgRespiration;

            // alias legacy
            ExgCh1 = Exg1Ch1;
            ExgCh2 = Exg1Ch2;
        }

        // === Costruttore LEGACY (2 canali EXG → mappa su EXG1) ===
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
        : this(
            timeStamp,
            accelerometerX, accelerometerY, accelerometerZ,
            wideAccelerometerX, wideAccelerometerY, wideAccelerometerZ,
            gyroscopeX, gyroscopeY, gyroscopeZ,
            magnetometerX, magnetometerY, magnetometerZ,
            temperatureBMP180, pressureBMP180,
            batteryVoltage,
            extADC_A6, extADC_A7, extADC_A15,
            // EXG1 popolato dai vecchi parametri
            exgCh1, exgCh2,
            // EXG2 assenti nello schema legacy
            null, null,
            exgRespiration
        )
        { }
#else
        // Fallback neutro (iOS/MacCatalyst): evita dipendenze da SensorData
        public readonly object TimeStamp;

        public readonly object LowNoiseAccelerometerX;
        public readonly object LowNoiseAccelerometerY;
        public readonly object LowNoiseAccelerometerZ;

        public readonly object WideRangeAccelerometerX;
        public readonly object WideRangeAccelerometerY;
        public readonly object WideRangeAccelerometerZ;

        public readonly object GyroscopeX;
        public readonly object GyroscopeY;
        public readonly object GyroscopeZ;

        public readonly object MagnetometerX;
        public readonly object MagnetometerY;
        public readonly object MagnetometerZ;

        public readonly object Temperature_BMP180;
        public readonly object Pressure_BMP180;

        public readonly object BatteryVoltage;

        public readonly object ExtADC_A6;
        public readonly object ExtADC_A7;
        public readonly object ExtADC_A15;

        // EXG (oggetti)
        public readonly object Exg1Ch1;
        public readonly object Exg1Ch2;
        public readonly object Exg2Ch1;
        public readonly object Exg2Ch2;
        public readonly object ExgRespiration;

        public readonly object ExgCh1; // alias
        public readonly object ExgCh2; // alias

        public XR2Learn_ShimmerEXGData(
            object timeStamp = null,
            object accelerometerX = null, object accelerometerY = null, object accelerometerZ = null,
            object wideAccelerometerX = null, object wideAccelerometerY = null, object wideAccelerometerZ = null,
            object gyroscopeX = null, object gyroscopeY = null, object gyroscopeZ = null,
            object magnetometerX = null, object magnetometerY = null, object magnetometerZ = null,
            object temperatureBMP180 = null, object pressureBMP180 = null,
            object batteryVoltage = null,
            object extADC_A6 = null, object extADC_A7 = null, object extADC_A15 = null,
            object exg1Ch1 = null, object exg1Ch2 = null,
            object exg2Ch1 = null, object exg2Ch2 = null,
            object exgRespiration = null
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

            Exg1Ch1 = exg1Ch1;
            Exg1Ch2 = exg1Ch2;
            Exg2Ch1 = exg2Ch1;
            Exg2Ch2 = exg2Ch2;
            ExgRespiration = exgRespiration;

            ExgCh1 = exg1Ch1;
            ExgCh2 = exg1Ch2;
        }
#endif
    }
}
