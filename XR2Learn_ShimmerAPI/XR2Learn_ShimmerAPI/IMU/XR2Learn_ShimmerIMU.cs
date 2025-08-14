using System;
using System.Threading;
#if WINDOWS || ANDROID
using ShimmerAPI;
#endif

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
#if WINDOWS
        private ShimmerLogAndStreamSystemSerialPortV2 shimmer;
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
        private ShimmerLogAndStreamAndroidBluetooth shimmerAndroid;
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
            int enabledSensors = 0;

            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBattery = enableBattery;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;

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

            Thread.Sleep(500);

            shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
            shimmer.UICallback += this.HandleEvent;
        }

        private void HandleEvent(object sender, EventArgs args)
        {
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
                    indexWideAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroX = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroY = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroZ = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagX = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagY = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagZ = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Temperature = oc.GetIndex(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Pressure = oc.GetIndex(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBatteryVoltage = oc.GetIndex(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA6 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA7 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA15 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL);

                    firstDataPacket = false;
                }

                LatestData = new XR2Learn_ShimmerIMUData(
                    oc.GetData(indexTimeStamp),
                    oc.GetData(indexLowNoiseAccX),
                    oc.GetData(indexLowNoiseAccY),
                    oc.GetData(indexLowNoiseAccZ),
                    oc.GetData(indexWideAccX),
                    oc.GetData(indexWideAccY),
                    oc.GetData(indexWideAccZ),
                    oc.GetData(indexGyroX),
                    oc.GetData(indexGyroY),
                    oc.GetData(indexGyroZ),
                    oc.GetData(indexMagX),
                    oc.GetData(indexMagY),
                    oc.GetData(indexMagZ),
                    oc.GetData(indexBMP180Temperature),
                    oc.GetData(indexBMP180Pressure),
                    oc.GetData(indexBatteryVoltage),
                    oc.GetData(indexExtA6),
                    oc.GetData(indexExtA7),
                    oc.GetData(indexExtA15)
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
    // Salva le impostazioni
    _enableLowNoiseAccelerometer = enableLowNoiseAcc;
    _enableWideRangeAccelerometer = enableWideRangeAcc;
    _enableGyroscope = enableGyro;
    _enableMagnetometer = enableMag;
    _enablePressureTemperature = enablePressureTemp;
    _enableBattery = enableBattery;
    _enableExtA6 = enableExtA6;
    _enableExtA7 = enableExtA7;
    _enableExtA15 = enableExtA15;

    mac = (mac ?? "").Trim();
    if (!Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
        throw new ArgumentException($"Invalid MAC '{mac}'");
    Android.Util.Log.Debug("ShimmerBT", $"ConfigureAndroid: deviceId={deviceId}, mac={mac}");


    // Inizializza la connessione Bluetooth Android
    shimmerAndroid = new ShimmerLogAndStreamAndroidBluetooth(
        deviceId, mac, _samplingRate, 0, 0, false, false, false, 0, 0, null, null, false);

    shimmerAndroid.UICallback += HandleEventAndroid;
}

public bool ConnectInternalAndroid()
{
    if (shimmerAndroid == null)
        throw new InvalidOperationException("Shimmer Android non configurato. Chiama ConfigureAndroid() prima.");

    return shimmerAndroid.Connect();
}

private void HandleEventAndroid(object sender, EventArgs args)
{
    var eventArgs = (CustomEventArgs)args;

    if (eventArgs.getIndicator() == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
    {
        ObjectCluster oc = (ObjectCluster)eventArgs.getObject();

        if (firstDataPacketAndroid)
        {
            indexTimeStamp = oc.GetIndex(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL);
            indexLowNoiseAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
            indexLowNoiseAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
            indexLowNoiseAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
            indexWideAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
            indexWideAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
            indexWideAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
            indexGyroX = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL);
            indexGyroY = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL);
            indexGyroZ = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL);
            indexMagX = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL);
            indexMagY = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
            indexMagZ = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
            indexBMP180Temperature = oc.GetIndex(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL);
            indexBMP180Pressure = oc.GetIndex(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL);
            indexBatteryVoltage = oc.GetIndex(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL);
            indexExtA6 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL);
            indexExtA7 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL);
            indexExtA15 = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL);

            firstDataPacketAndroid = false;
        }

        LatestData = new XR2Learn_ShimmerIMUData(
            oc.GetData(indexTimeStamp),
            oc.GetData(indexLowNoiseAccX),
            oc.GetData(indexLowNoiseAccY),
            oc.GetData(indexLowNoiseAccZ),
            oc.GetData(indexWideAccX),
            oc.GetData(indexWideAccY),
            oc.GetData(indexWideAccZ),
            oc.GetData(indexGyroX),
            oc.GetData(indexGyroY),
            oc.GetData(indexGyroZ),
            oc.GetData(indexMagX),
            oc.GetData(indexMagY),
            oc.GetData(indexMagZ),
            oc.GetData(indexBMP180Temperature),
            oc.GetData(indexBMP180Pressure),
            oc.GetData(indexBatteryVoltage),
            oc.GetData(indexExtA6),
            oc.GetData(indexExtA7),
            oc.GetData(indexExtA15)
        );
    }
}
#endif

    }
}
