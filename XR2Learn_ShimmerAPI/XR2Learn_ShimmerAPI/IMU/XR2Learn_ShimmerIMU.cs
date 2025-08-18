using System;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS || ANDROID
using ShimmerAPI;
#endif
#if ANDROID
using XR2Learn_ShimmerAPI.IMU.Android; // per ShimmerLogAndStreamAndroidBluetoothV2
#endif


namespace XR2Learn_ShimmerAPI.IMU
{



    public partial class XR2Learn_ShimmerIMU
    {

#if WINDOWS
private int _winEnabledSensors;
private ShimmerLogAndStreamSystemSerialPortV2? shimmer;
private volatile bool _reconfigInProgress = false;

#endif


#if WINDOWS
        private bool firstDataPacket = true;
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
#endif

#if ANDROID
        // Wrapper Android V2 (trasporto BT, API identiche a Windows)
        private ShimmerLogAndStreamAndroidBluetoothV2 shimmerAndroid;
        private bool firstDataPacketAndroid = true;
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

        // Bitmap sensori calcolato in ConfigureAndroid (speculare a Windows)
        private int _androidEnabledSensors;

        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidConnectedTcs;
        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidStreamingAckTcs;
        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidFirstPacketTcs;
        private bool _firstDataPacketAndroid = true;
#endif

        public XR2Learn_ShimmerIMUData LatestData { get; private set; }

        public XR2Learn_ShimmerIMU()
        {
            _samplingRate = 51.2;
            _enableLowNoiseAccelerometer = true;
            _enableWideRangeAccelerometer = true;
            _enableGyroscope = true;
            _enableMagnetometer = true;
            _enablePressureTemperature = true;
            _enableBattery = true;
            _enableExtA6 = true;
            _enableExtA7 = true;
            _enableExtA15 = true;
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
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15)
        {
            _reconfigInProgress = true;
            try
            {
                // 1) Detach & cleanup eventuale istanza precedente per evitare handler duplicati
                if (shimmer != null)
                {
                    try { shimmer.UICallback -= this.HandleEvent; } catch { /* no-op */ }
                    try { shimmer.StopStreaming(); } catch { /* no-op */ }
                    try { shimmer.Disconnect(); } catch { /* no-op */ }
                    shimmer = null;
                }

                // 2) Memorizza i flag come già fai
                _enableLowNoiseAccelerometer = enableLowNoiseAcc;
                _enableWideRangeAccelerometer = enableWideRangeAcc;
                _enableGyroscope = enableGyro;
                _enableMagnetometer = enableMag;
                _enablePressureTemperature = enablePressureTemp;
                _enableBattery = enableBattery;
                _enableExtA6 = enableExtA6;
                _enableExtA7 = enableExtA7;
                _enableExtA15 = enableExtA15;

                // 3) Ricostruisci la bitmap sensori
                int enabledSensors = 0;
                if (_enableLowNoiseAccelerometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
                if (_enableWideRangeAccelerometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
                if (_enableGyroscope)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
                if (_enableMagnetometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
                if (_enablePressureTemperature)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
                if (_enableBattery)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
                if (_enableExtA6)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
                if (_enableExtA7)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
                if (_enableExtA15)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

                _winEnabledSensors = enabledSensors;

                // 4) Forza il ricalcolo degli indici al prossimo pacchetto
                firstDataPacket = true;

                // 5) Nuova istanza + sottoscrizione **senza duplicati**
                shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                try { shimmer.UICallback -= this.HandleEvent; } catch { /* no-op */ }
                shimmer.UICallback += this.HandleEvent;

                // Se avevi un Thread.Sleep, non serve; se proprio ti è utile per il seriale:
                // Thread.Sleep(100);
            }
            finally
            {
                _reconfigInProgress = false;
            }
        }


        private static SensorData? GetSafe(ObjectCluster oc, int idx)
        {
            return idx >= 0 ? oc.GetData(idx) : null;
        }

    private void HandleEvent(object sender, EventArgs args)
    {
        if (_reconfigInProgress) return; // ignora eventi durante la reconfig

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

                firstDataPacket = false;
            }

            LatestData = new XR2Learn_ShimmerIMUData(
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
                GetSafe(oc, indexExtA15)
            );
        }
    }
#endif

#if ANDROID
        public void ConfigureAndroid(
            string deviceId,
            string mac,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15)
        {
            // 1) Salva flag (identico a Windows)
            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBattery = enableBattery;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;

            // 2) Valida MAC + log (usa SEMPRE global::Android.* per evitare shadowing)
            mac = (mac ?? "").Trim();
            if (!global::Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
                throw new ArgumentException($"Invalid MAC '{mac}'");
            global::Android.Util.Log.Debug("ShimmerBT", $"ConfigureAndroid: deviceId={deviceId}, mac={mac}");

            // 2.5) Calcola bitmap sensori (speculare a Windows) e memorizzalo
            int sensors = 0;
            if (_enableLowNoiseAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
            if (_enableWideRangeAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
            if (_enableGyroscope)             sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
            if (_enableMagnetometer)          sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
            if (_enablePressureTemperature)   sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
            if (_enableBattery)               sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
            if (_enableExtA6)                 sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
            if (_enableExtA7)                 sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
            if (_enableExtA15)                sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

            _androidEnabledSensors = sensors;

            // 3) Istanzia il wrapper V2 (trasporto BT, API identiche a Windows)
            shimmerAndroid = new ShimmerLogAndStreamAndroidBluetoothV2(deviceId, mac);

            // 4) Callback eventi: evita doppia sottoscrizione
            shimmerAndroid.UICallback -= HandleEventAndroid;
            shimmerAndroid.UICallback += HandleEventAndroid;
        }

        public bool ConnectInternalAndroid()
        {
            if (shimmerAndroid == null)
                throw new InvalidOperationException("Shimmer Android non configurato. Chiama ConfigureAndroid() prima.");

            shimmerAndroid.Connect();
            return shimmerAndroid.IsConnected();
        }

        private void HandleEventAndroid(object sender, EventArgs args)
        {
            const string TAG = "ShimmerBT";

            try
            {
                if (args is not CustomEventArgs ev)
                {
                    global::Android.Util.Log.Warn(TAG, $"Evento sconosciuto: {args?.GetType().Name}");
                    return;
                }

                int indicator = ev.getIndicator();


                // Sblocca attese su ACK di start streaming e sul primo pacchetto dati
                if (indicator == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE)
                {
                    // L'oggetto dell'evento è un Int32 con lo stato corrente
                    if (ev.getObject() is int st && st == 3 /* SHIMMER_STATE_STREAMING */)
                        _androidStreamingAckTcs?.TrySetResult(true);
                }
                else if (indicator == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
                {
                    _androidFirstPacketTcs?.TrySetResult(true);
                }

















                // helper locali
                static int SafeIdx(ObjectCluster c, string name, string fmt)
                {
                    var i = c.GetIndex(name, fmt);
                    return i < 0 ? -1 : i;
                }
                static SensorData SafeGet(ObjectCluster c, int idx) => idx >= 0 ? c.GetData(idx) : null;

                static string Val(SensorData s)
                {
                    try
                    {
                        if (s == null) return "—";
                        var u = (s.Unit ?? "").ToString();
                        return string.IsNullOrWhiteSpace(u) ? $"{s.Data:0.###}" : $"{s.Data:0.###} {u}";
                    }
                    catch { return s?.ToString() ?? "—"; }
                }

                // log di servizio sugli indicator
                switch (indicator)
                {
                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE:
                        global::Android.Util.Log.Info(TAG, $"EVENT STATE_CHANGE ({indicator}) obj={ev.getObject()?.GetType().Name ?? "null"}");
                        if (ev.getObject() is int st)
                        {
                            if (st == ShimmerBluetooth.SHIMMER_STATE_CONNECTED)
                                _androidConnectedTcs?.TrySetResult(true);
                            if (st == ShimmerBluetooth.SHIMMER_STATE_STREAMING)
                                _androidStreamingAckTcs?.TrySetResult(true);
                        }
                        return;

                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_NOTIFICATION_MESSAGE:
                        global::Android.Util.Log.Info(TAG, $"EVENT NOTIFICATION ({indicator}) obj={ev.getObject() ?? "null"}");
                        return;

                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_PACKET_RECEPTION_RATE:
                        global::Android.Util.Log.Info(TAG, $"EVENT PRR ({indicator}) value={ev.getObject() ?? "null"}");
                        return;

                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET:
                    _androidFirstPacketTcs?.TrySetResult(true);

                        // gestito sotto
                        break;

                    default:
                        global::Android.Util.Log.Info(TAG, $"EVENT {indicator} obj={ev.getObject()?.GetType().Name ?? "null"}");
                        return;
                }

                // --- DATA_PACKET ---
                var oc = ev.getObject() as ObjectCluster;
                if (oc == null)
                {
                    global::Android.Util.Log.Warn(TAG, "ObjectCluster null su DATA_PACKET");
                    return;
                }

                // mappa indici al primo pacchetto
                if (firstDataPacketAndroid)
                {
                    indexTimeStamp         = SafeIdx(oc, ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, "CAL");
                    indexLowNoiseAccX      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, "CAL");
                    indexLowNoiseAccY      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, "CAL");
                    indexLowNoiseAccZ      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, "CAL");
                    indexWideAccX          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, "CAL");
                    indexWideAccY          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, "CAL");
                    indexWideAccZ          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, "CAL");
                    indexGyroX             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_X, "CAL");
                    indexGyroY             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_Y, "CAL");
                    indexGyroZ             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_Z, "CAL");
                    indexMagX              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_X, "CAL");
                    indexMagY              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, "CAL");
                    indexMagZ              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, "CAL");
                    indexBMP180Temperature = SafeIdx(oc, Shimmer3Configuration.SignalNames.TEMPERATURE, "CAL");
                    indexBMP180Pressure    = SafeIdx(oc, Shimmer3Configuration.SignalNames.PRESSURE, "CAL");
                    indexBatteryVoltage    = SafeIdx(oc, Shimmer3Configuration.SignalNames.V_SENSE_BATT, "CAL");
                    indexExtA6             = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, "CAL");
                    indexExtA7             = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, "CAL");
                    indexExtA15            = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, "CAL");

                    global::Android.Util.Log.Info(TAG, "Indici mappati (CAL) al primo pacchetto.");
                    firstDataPacketAndroid = false;
                }

                // aggiorna LatestData (usa CAL)
                LatestData = new XR2Learn_ShimmerIMUData(
                    SafeGet(oc, indexTimeStamp),
                    SafeGet(oc, indexLowNoiseAccX), SafeGet(oc, indexLowNoiseAccY), SafeGet(oc, indexLowNoiseAccZ),
                    SafeGet(oc, indexWideAccX),    SafeGet(oc, indexWideAccY),    SafeGet(oc, indexWideAccZ),
                    SafeGet(oc, indexGyroX),       SafeGet(oc, indexGyroY),       SafeGet(oc, indexGyroZ),
                    SafeGet(oc, indexMagX),        SafeGet(oc, indexMagY),        SafeGet(oc, indexMagZ),
                    SafeGet(oc, indexBMP180Temperature), SafeGet(oc, indexBMP180Pressure),
                    SafeGet(oc, indexBatteryVoltage),
                    SafeGet(oc, indexExtA6), SafeGet(oc, indexExtA7), SafeGet(oc, indexExtA15)
                );

                // riga sintetica compatta
                string S(params SensorData[] ds) => string.Join(",", Array.ConvertAll(ds, Val));
                global::Android.Util.Log.Info(TAG,
                    $"DATA_PACKET(ts={Val(SafeGet(oc, indexTimeStamp))}) " +
                    $"LNA=({S(SafeGet(oc, indexLowNoiseAccX), SafeGet(oc, indexLowNoiseAccY), SafeGet(oc, indexLowNoiseAccZ))}) " +
                    $"WRA=({S(SafeGet(oc, indexWideAccX), SafeGet(oc, indexWideAccY), SafeGet(oc, indexWideAccZ))}) " +
                    $"GYRO=({S(SafeGet(oc, indexGyroX), SafeGet(oc, indexGyroY), SafeGet(oc, indexGyroZ))}) " +
                    $"MAG=({S(SafeGet(oc, indexMagX), SafeGet(oc, indexMagY), SafeGet(oc, indexMagZ))}) " +
                    $"TEMP={Val(SafeGet(oc, indexBMP180Temperature))} " +
                    $"PRESS={Val(SafeGet(oc, indexBMP180Pressure))} " +
                    $"VBATT={Val(SafeGet(oc, indexBatteryVoltage))} " +
                    $"EXT=({S(SafeGet(oc, indexExtA6), SafeGet(oc, indexExtA7), SafeGet(oc, indexExtA15))})"
                );

                // dump completo CAL/RAW dei segnali che ti interessano
                void DumpOne(ObjectCluster c, string name)
                {
                    var iCal = SafeIdx(c, name, "CAL");
                    if (iCal >= 0) global::Android.Util.Log.Info(TAG, $"{name} [CAL] = {Val(SafeGet(c, iCal))}");
                    var iRaw = SafeIdx(c, name, "RAW");
                    if (iRaw >= 0) global::Android.Util.Log.Info(TAG, $"{name} [RAW] = {Val(SafeGet(c, iRaw))}");
                }

                global::Android.Util.Log.Info(TAG, "---- FULL ObjectCluster DUMP (CAL/RAW) ----");
                string[] names =
                {
                    ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP,
                    Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X,
                    Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y,
                    Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z,
                    Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X,
                    Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y,
                    Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z,
                    Shimmer3Configuration.SignalNames.GYROSCOPE_X,
                    Shimmer3Configuration.SignalNames.GYROSCOPE_Y,
                    Shimmer3Configuration.SignalNames.GYROSCOPE_Z,
                    Shimmer3Configuration.SignalNames.MAGNETOMETER_X,
                    Shimmer3Configuration.SignalNames.MAGNETOMETER_Y,
                    Shimmer3Configuration.SignalNames.MAGNETOMETER_Z,
                    Shimmer3Configuration.SignalNames.TEMPERATURE,
                    Shimmer3Configuration.SignalNames.PRESSURE,
                    Shimmer3Configuration.SignalNames.V_SENSE_BATT,
                    Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6,
                    Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7,
                    Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15
                };
                foreach (var n in names) DumpOne(oc, n);
                global::Android.Util.Log.Info(TAG, "---- END ObjectCluster DUMP --------------");
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error(TAG, "HandleEventAndroid error:");
                global::Android.Util.Log.Error(TAG, ex.ToString());
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
#endif

    }
}
