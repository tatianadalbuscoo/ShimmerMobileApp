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

        // --- EXG (nuovo schema: 4 canali) ---
        public readonly SensorData Exg1Ch1;   // EXG1_CH1
        public readonly SensorData Exg1Ch2;   // EXG1_CH2
        public readonly SensorData Exg2Ch1;   // EXG2_CH1
        public readonly SensorData Exg2Ch2;   // EXG2_CH2
        public readonly SensorData ExgRespiration;

        // --- Alias legacy per compatibilità (vecchi nomi) ---
        // Manteniamo ExgCh1/ExgCh2 puntati a EXG1 per non rompere la UI esistente
        public readonly SensorData ExgCh1;    // alias di Exg1Ch1
        public readonly SensorData ExgCh2;    // alias di Exg1Ch2

        // === Costruttore NUOVO (consigliato): 4 canali EXG ===
        public XR2Learn_ShimmerEXGData(
            SensorData timeStamp,
            SensorData accelerometerX, SensorData accelerometerY, SensorData accelerometerZ,
            SensorData wideAccelerometerX, SensorData wideAccelerometerY, SensorData wideAccelerometerZ,
            SensorData gyroscopeX, SensorData gyroscopeY, SensorData gyroscopeZ,
            SensorData magnetometerX, SensorData magnetometerY, SensorData magnetometerZ,
            SensorData temperatureBMP180, SensorData pressureBMP180,
            SensorData batteryVoltage,
            SensorData extADC_A6, SensorData extADC_A7, SensorData extADC_A15,
            // 4 canali EXG
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

            // nuovi campi
            Exg1Ch1 = exg1Ch1;
            Exg1Ch2 = exg1Ch2;
            Exg2Ch1 = exg2Ch1;
            Exg2Ch2 = exg2Ch2;
            ExgRespiration = exgRespiration;

            // alias legacy
            ExgCh1 = Exg1Ch1;
            ExgCh2 = Exg1Ch2;
        }

        // === Costruttore LEGACY: 2 canali EXG (compatibilità con codice esistente) ===
        // Mappa automaticamente sui canali di EXG1; EXG2 resta null.
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
                // EXG1 = vecchi parametri
                exgCh1, exgCh2,
                // EXG2 non fornito nel vecchio schema
                null, null,
                exgRespiration
            )
        { }
#else
        public XR2Learn_ShimmerEXGData() { }
#endif
    }
}
