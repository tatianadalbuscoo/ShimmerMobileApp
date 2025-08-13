#if WINDOWS
using ShimmerAPI;
#endif
using System;
using System.Threading;

namespace XR2Learn_ShimmerAPI.IMU
{

    /// <summary>
    /// Partial class that handles Shimmer IMU configuration, data acquisition,
    /// and real-time data parsing from a serial port connection.
    /// Uses ShimmerLogAndStreamSystemSerialPortV2 for enhanced serial communication.
    /// </summary>
    public partial class XR2Learn_ShimmerIMU
    {

#if WINDOWS
        // Instance of the enhanced Shimmer serial port communication class.
        private ShimmerLogAndStreamSystemSerialPortV2 shimmer;

        private bool firstDataPacket = true;

        // Indexes for sensor signal mapping
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

        /// <summary>
        /// Latest IMU data sample received from the Shimmer device.
        /// </summary>
        public XR2Learn_ShimmerIMUData LatestData { get; private set; }

        /// <summary>
        /// Initializes default sensor settings and sampling rate.
        /// </summary>
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


        /// <summary>
        /// Configures the Shimmer device connection and enabled sensors.
        /// </summary>
        /// <param name="deviceName">Device identifier string.</param>
        /// <param name="comPort">Serial COM port name.</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable BMP180 temperature/pressure.</param>
        /// <param name="enableBattery">Enable battery voltage monitoring.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        public void Configure(
            string deviceName, string comPort, bool enableLowNoiseAcc,
            bool enableWideRangeAcc, bool enableGyro, bool enableMag,
            bool enablePressureTemp, bool enableBattery,
            bool enableExtA6, bool enableExtA7, bool enableExtA15
        )
        {
#if WINDOWS
            int enabledSensors = 0;

            // Set sensor enable flags
            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBattery = enableBattery;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;

            // Build sensor bitmap
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

            // Short delay before initializing communication
            Thread.Sleep(500);

            // Initialize the enhanced shimmer communication
            shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);

            // Register the data callback handler
            shimmer.UICallback += this.HandleEvent;

#elif ANDROID
    // --- ANDROID: memorizza i flag sensori e l'endpoint MAC ---
    _enableLowNoiseAccelerometer = enableLowNoiseAcc;
    _enableWideRangeAccelerometer = enableWideRangeAcc;
    _enableGyroscope = enableGyro;
    _enableMagnetometer = enableMag;
    _enablePressureTemperature = enablePressureTemp;
    _enableBattery = enableBattery;
    _enableExtA6 = enableExtA6;
    _enableExtA7 = enableExtA7;
    _enableExtA15 = enableExtA15;

    // deviceName logico e MAC endpoint (comPort contiene il MAC su Android)
    _deviceId = deviceName;
    _endpointMac = (comPort ?? string.Empty).Trim();

    // Non creiamo qui l'istanza: la Connect() Android userà _endpointMac per aprire RFCOMM
    Android.Util.Log.Info("Shimmer", $"Configure ANDROID: device={_deviceId}, mac={_endpointMac}");

#elif MACCATALYST
    // memorizza i flag sensori come su Windows
    _enableLowNoiseAccelerometer = enableLowNoiseAcc;
    _enableWideRangeAccelerometer = enableWideRangeAcc;
    _enableGyroscope = enableGyro;
    _enableMagnetometer = enableMag;
    _enablePressureTemperature = enablePressureTemp;
    _enableBattery = enableBattery;
    _enableExtA6 = enableExtA6;
    _enableExtA7 = enableExtA7;
    _enableExtA15 = enableExtA15;

    // su Mac usiamo comPort come hint del nome BLE (es. "Shimmer3")
    if (!string.IsNullOrWhiteSpace(comPort))
        _bleDeviceName = comPort;
#else
    Console.WriteLine("Shimmer IMU non supportato su questa piattaforma.");
#endif
        }

#if WINDOWS
        /// <summary>
        /// Handles incoming data packets from the Shimmer device and extracts sensor values.
        /// </summary>
        private void HandleEvent(object sender, EventArgs args)
        {
            CustomEventArgs eventArgs = (CustomEventArgs)args;

            // Verify that the received message is a data packet
            if (eventArgs.getIndicator() == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
            {

                // Gets the raw data collected from the device into an object called ObjectCluster
                ObjectCluster oc = (ObjectCluster)eventArgs.getObject();

                // When receiving for the first time, save the signal indexes for quick access later
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

                // Creates a new XR2Learn_ShimmerIMUData object containing all the sensor data and saves it in LatestData
                LatestData = new XR2Learn_ShimmerIMUData(

                    // Use the saved indices to extract the values of each sensor from the received packet
                    oc.GetData(indexTimeStamp),

                    oc.GetData(indexLowNoiseAccX), oc.GetData(indexLowNoiseAccY), oc.GetData(indexLowNoiseAccZ),

                    oc.GetData(indexWideAccX), oc.GetData(indexWideAccY), oc.GetData(indexWideAccZ),

                    oc.GetData(indexGyroX), oc.GetData(indexGyroY), oc.GetData(indexGyroZ),

                    oc.GetData(indexMagX), oc.GetData(indexMagY), oc.GetData(indexMagZ),

                    oc.GetData(indexBMP180Temperature), oc.GetData(indexBMP180Pressure),
                    
                    oc.GetData(indexBatteryVoltage),

                    oc.GetData(indexExtA6), oc.GetData(indexExtA7), oc.GetData(indexExtA15)
                );
            }
        }
#endif
    }
}