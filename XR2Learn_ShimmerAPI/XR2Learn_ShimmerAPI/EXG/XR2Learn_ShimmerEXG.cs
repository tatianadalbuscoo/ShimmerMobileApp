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

        // ExG indices (mode dependent)
        private int indexExgCh1;
        private int indexExgCh2;
        private int indexExgResp;
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
                    try
                    {
                        enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT;
                        enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;
                    }
                    catch { }
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

        private static int FirstIndex(ObjectCluster oc, string[] names, string? fmt = null)
        {
            foreach (var n in names)
            {
                var i = oc.GetIndex(n, fmt ?? ShimmerConfiguration.SignalFormats.CAL);
                if (i >= 0) return i;
            }
            return -1;
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

                    indexExgCh1 = FirstIndex(oc, new[] { "EXG_CH1", "EXG1", "ECG_CH1", "EMG_CH1", "ExG CH1" });
                    indexExgCh2 = FirstIndex(oc, new[] { "EXG_CH2", "EXG2", "ECG_CH2", "EMG_CH2", "ExG CH2" });
                    indexExgResp = FirstIndex(oc, new[] { "RESPIRATION", "EXG_RESPIRATION", "RESP", "Respiration" });

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
                    _enableExg ? GetSafe(oc, indexExgCh1) : null,
                    _enableExg ? GetSafe(oc, indexExgCh2) : null,
                    (_enableExg && _exgMode == ExgMode.Respiration) ? GetSafe(oc, indexExgResp) : null
                );

                try { SampleReceived?.Invoke(this, latest); } catch { }
            }
        }
#endif
    }
}
