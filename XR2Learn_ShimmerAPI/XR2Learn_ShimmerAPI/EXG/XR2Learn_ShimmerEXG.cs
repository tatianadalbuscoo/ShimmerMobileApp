using System;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS
using ShimmerAPI;
#endif

namespace XR2Learn_ShimmerAPI.GSR
{
    public enum ExgMode
    {
        ECG,
        EMG,
        ExGTest,
        Respiration
    }

    // NOTE: Windows-only for now (same style as IMU, but restricted)
    public partial class XR2Learn_ShimmerEXG
    {
        public event EventHandler<dynamic>? SampleReceived;

#if WINDOWS
        private int _winEnabledSensors;
        private ShimmerLogAndStreamSystemSerialPortV2? shimmer;
        private volatile bool _reconfigInProgress = false;

        private bool firstDataPacket = true;

        // indices for signals we care about
        private int indexTimeStamp;
        private int indexLowNoiseAccX;
        private int indexLowNoiseAccY;
        private int indexLowNoiseAccZ;
        private int indexWideAccX;
        private int indexWideAccY;
        private int indexWideAccZ;
        private int indexGyroX;
        private int indexGyroY;
        private int indexGyroZ;
        private int indexMagX;
        private int indexMagY;
        private int indexMagZ;
        private int indexBMP180Temperature;
        private int indexBMP180Pressure;
        private int indexBatteryVoltage;
        private int indexExtA6;
        private int indexExtA7;
        private int indexExtA15;

        // EXG (2 linee da mostrare sempre)
        private int indexExgCh1;   // CH1 generico (EXG_CH1/EXG1_CH1/ECG_CH1/EMG_CH1…)
        private int indexExgCh2;   // CH2 generico (EXG_CH2/EXG2_CH1/ECG_CH2/EMG_CH2…)
        private int indexExgResp;  // opzionale, solo in modalità Respiration
#endif

        public XR2Learn_ShimmerEXG()
        {
            _samplingRate = 51.2;

            // default enable (same idea as IMU defaults)
            _enableLowNoiseAccelerometer = true;
            _enableWideRangeAccelerometer = true;
            _enableGyroscope = true;
            _enableMagnetometer = true;
            _enablePressureTemperature = true;
            _enableBatteryVoltage = true;
            _enableExtA6 = true;
            _enableExtA7 = true;
            _enableExtA15 = true;

            _enableExg = false; // off by default
            _exgMode = ExgMode.ECG;
        }

        /// <summary>
        /// Applies the nearest sampling rate supported by the firmware and returns it.
        /// </summary>
        public double SetFirmwareSamplingRateNearest(double requestedHz)
        {
            if (requestedHz <= 0) throw new ArgumentOutOfRangeException(nameof(requestedHz));

            double clock = 32768.0; // Shimmer3 default; Shimmer2 uses 1024 Hz
#if WINDOWS
            try
            {
                if (shimmer != null && !shimmer.isShimmer3withUpdatedSensors())
                    clock = 1024.0;
            }
            catch { /* keep 32768 */ }
#endif
            int divider = Math.Max(1, (int)Math.Round(clock / requestedHz, MidpointRounding.AwayFromZero));
            double applied = clock / divider;

#if WINDOWS
            shimmer?.WriteSamplingRate(applied);
#endif
            SamplingRate = applied;
            return applied;
        }

#if WINDOWS
        public void ConfigureWindows(
            string deviceName,
            string comPort,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBatteryVoltage,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15,
            bool enableExg,
            ExgMode exgMode
        )
        {
            _reconfigInProgress = true;
            try
            {
                // cleanup previous instance
                if (shimmer != null)
                {
                    try { shimmer.UICallback -= this.HandleEvent; } catch { }
                    try { shimmer.StopStreaming(); } catch { }
                    try { shimmer.Disconnect(); } catch { }
                    shimmer = null;
                }

                // store flags
                _enableLowNoiseAccelerometer = enableLowNoiseAcc;
                _enableWideRangeAccelerometer = enableWideRangeAcc;
                _enableGyroscope = enableGyro;
                _enableMagnetometer = enableMag;
                _enablePressureTemperature = enablePressureTemp;
                _enableBatteryVoltage = enableBatteryVoltage;
                _enableExtA6 = enableExtA6;
                _enableExtA7 = enableExtA7;
                _enableExtA15 = enableExtA15;
                _enableExg = enableExg;
                _exgMode = exgMode;

                // build sensor bitmap (Shimmer3)
                int enabled = 0;
                if (_enableLowNoiseAccelerometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
                if (_enableWideRangeAccelerometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
                if (_enableGyroscope)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
                if (_enableMagnetometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
                if (_enablePressureTemperature)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
                if (_enableBatteryVoltage)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
                if (_enableExtA6)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
                if (_enableExtA7)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
                if (_enableExtA15)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

                if (_enableExg)
                {
                    // abilita entrambi i blocchi EXG
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT;
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;
                }

                _winEnabledSensors = enabled;

                firstDataPacket = true; // force remap on next packet

                shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                try { shimmer.UICallback -= this.HandleEvent; } catch { }
                shimmer.UICallback += this.HandleEvent;
            }
            finally
            {
                _reconfigInProgress = false;
            }
        }

        private static SensorData? GetSafe(ObjectCluster oc, int idx)
            => idx >= 0 ? oc.GetData(idx) : null;

        // Cerca su più NOMI e FORMATI (CAL → RAW → UNCAL → default/null)
        private static (int idx, string? name, string? fmt) FindSignal(ObjectCluster oc, string[] names)
        {
            string[] formats = new[]
            {
                ShimmerConfiguration.SignalFormats.CAL,
                "RAW",
                "UNCAL"
            };
            foreach (var f in formats)
                foreach (var n in names)
                {
                    int i = oc.GetIndex(n, f);
                    if (i >= 0) return (i, n, f);
                }
            foreach (var n in names)
            {
                int i = oc.GetIndex(n, null);
                if (i >= 0) return (i, n, null);
            }
            return (-1, null, null);
        }

        private void HandleEvent(object sender, EventArgs args)
        {
            if (_reconfigInProgress) return;
            var eventArgs = (CustomEventArgs)args;

            if (eventArgs.getIndicator() == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
            {
                ObjectCluster oc = (ObjectCluster)eventArgs.getObject();

                if (firstDataPacket)
                {
                    // IMU e vari
                    indexTimeStamp = oc.GetIndex(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccX     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccY     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccZ     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroX        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroY        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroZ        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagX         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagY         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagZ         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Temperature = oc.GetIndex(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Pressure    = oc.GetIndex(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBatteryVoltage    = oc.GetIndex(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA6             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA7             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA15            = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL);

                    // === EXG: vogliamo due serie sempre ===
                    var ch1 = FindSignal(oc, new[]
                    {
                        // EXG primo canale
                        "EXG_CH1",                       // naming comune
                        Shimmer3Configuration.SignalNames.EXG1_CH1,
                        "EXG1_CH1","EXG1 CH1","EXG CH1",
                        // alias quando la board è in ECG/EMG
                        "ECG_CH1","ECG CH1","EMG_CH1","EMG CH1",
                        "ECG RA-LL","ECG LL-RA","ECG_RA-LL","ECG_LL-RA"
                    });
                    var ch2 = FindSignal(oc, new[]
                    {
                        // secondo tracciato: può chiamarsi EXG_CH2 o EXG2_CH1 a seconda del FW
                        "EXG_CH2",
                        Shimmer3Configuration.SignalNames.EXG2_CH1,
                        "EXG2_CH1","EXG2 CH1","EXG CH2",
                        "ECG_CH2","ECG CH2","EMG_CH2","EMG CH2",
                        "ECG LA-RA","ECG_RA-LA","ECG_LA-RA"
                    });
                    var rr = FindSignal(oc, new[] { "RESPIRATION","EXG_RESPIRATION","RESP","Respiration" });

                    indexExgCh1 = ch1.idx;
                    indexExgCh2 = ch2.idx;
                    indexExgResp = rr.idx;

                    System.Diagnostics.Debug.WriteLine(
                        $"[EXG map] CH1 idx={indexExgCh1} name={ch1.name} fmt={ch1.fmt} | " +
                        $"CH2 idx={indexExgCh2} name={ch2.name} fmt={ch2.fmt} | " +
                        $"RESP idx={indexExgResp} name={rr.name} fmt={rr.fmt}");

                    firstDataPacket = false;
                }

                var latest = new XR2Learn_ShimmerEXGData(
                    GetSafe(oc, indexTimeStamp),
                    GetSafe(oc, indexLowNoiseAccX),
                    GetSafe(oc, indexLowNoiseAccY),
                    GetSafe(oc, indexLowNoiseAccZ),
                    GetSafe(oc, indexWideAccX),
                    GetSafe(oc, indexWideAccY),
                    GetSafe(oc, indexWideAccZ),
                    GetSafe(oc, indexGyroX),
                    GetSafe(oc, indexGyroY),
                    GetSafe(oc, indexGyroZ),
                    GetSafe(oc, indexMagX),
                    GetSafe(oc, indexMagY),
                    GetSafe(oc, indexMagZ),
                    GetSafe(oc, indexBMP180Temperature),
                    GetSafe(oc, indexBMP180Pressure),
                    GetSafe(oc, indexBatteryVoltage),
                    GetSafe(oc, indexExtA6),
                    GetSafe(oc, indexExtA7),
                    GetSafe(oc, indexExtA15),

                    // DUE TRACCE sempre presenti (se EXG abilitato)
                    _enableExg ? GetSafe(oc, indexExgCh1) : null,
                    _enableExg ? GetSafe(oc, indexExgCh2) : null,

                    // Respiration solo nella modalità dedicata
                    (_enableExg && _exgMode == ExgMode.Respiration) ? GetSafe(oc, indexExgResp) : null
                );

                try { SampleReceived?.Invoke(this, latest); } catch { }
            }
        }
#endif
    }
}
