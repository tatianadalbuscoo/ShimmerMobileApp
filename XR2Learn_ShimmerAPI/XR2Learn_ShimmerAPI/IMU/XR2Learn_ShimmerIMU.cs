// Manages Shimmer IMU device configuration, initialization, and data acquisition.

using ShimmerAPI;
using ShimmerLibrary;
using System;
using XR2Learn_ShimmerAPI.IMU;

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
        private ShimmerLogAndStreamSystemSerialPort shimmer;

        private bool firstDataPacket = true;

        private int indexTimeStamp;
        private int indexAccX;
        private int indexAccY;
        private int indexAccZ;
        private int indexGyroX;
        private int indexGyroY;
        private int indexGyroZ;
        private int indexMagX;
        private int indexMagY;
        private int indexMagZ;

        public XR2Learn_ShimmerIMUData LatestData { get; private set; }

        public XR2Learn_ShimmerIMU()
        {
            // Sampling rate and default sensor config
            _samplingRate = 51.2;
            _enableAccelerometer = true;
            _enableGyroscope = true;
            _enableMagnetometer = true;
        }

        public void Configure(string deviceName, string comPort)
        {
            int enabledSensors = 0;



            if (_enableAccelerometer)
                enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;

            if (_enableGyroscope)
                enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;

            if (_enableMagnetometer)
                enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;



            shimmer = new ShimmerLogAndStreamSystemSerialPort(
                deviceName, comPort, _samplingRate,
                0, 0, enabledSensors,
                false, false, false,
                0, 0,
                Shimmer3Configuration.EXG_EMG_CONFIGURATION_CHIP1,
                Shimmer3Configuration.EXG_EMG_CONFIGURATION_CHIP2,
                true
            );


            shimmer.UICallback += this.HandleEvent;
        }

        private void HandleEvent(object sender, EventArgs args)
        {
            CustomEventArgs eventArgs = (CustomEventArgs)args;
            if (eventArgs.getIndicator() == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
            {
                ObjectCluster oc = (ObjectCluster)eventArgs.getObject();

                if (firstDataPacket)
                {
                    indexTimeStamp = oc.GetIndex(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL);
                    indexAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroX = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroY = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroZ = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagX = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagY = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagZ = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    firstDataPacket = false;
                }

                LatestData = new XR2Learn_ShimmerIMUData(
                    oc.GetData(indexTimeStamp),
                    oc.GetData(indexAccX), oc.GetData(indexAccY), oc.GetData(indexAccZ),
                    oc.GetData(indexGyroX), oc.GetData(indexGyroY), oc.GetData(indexGyroZ),
                    oc.GetData(indexMagX), oc.GetData(indexMagY), oc.GetData(indexMagZ)
                );
            }
        }
    }
}
