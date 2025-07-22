// Simple test class to connect to a Shimmer3 IMU device, start data streaming,
// and print sensor values (Accel, Gyro, Mag) every second.

using System;
using System.Threading;
using XR2Learn_ShimmerAPI.IMU;

namespace XR2Learn_ShimmerAPI
{
    /// <summary>
    /// Test class showcasing a simplified IMU use case
    /// </summary>
    public class TestIMU
    {
        private static readonly XR2Learn_ShimmerIMU api = new XR2Learn_ShimmerIMU();

        private static Timer timer; // keep global or it will be garbage-collected
        private static int t = 60000; // ms
        private static string deviceName = "Shimmer3_IMU";
        private static readonly double DefaultSamplingRate = 51.2;

        public static void Main(string[] args)
        {
            //string[] ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();
            string[] ports = new string[] { "COM15" };


            Console.WriteLine("Available ports: [ " + string.Join(", ", ports) + " ]");
            foreach (string port in ports)
            {
                string comPort = port;
                api.Configure(deviceName, comPort);

                api.EnableLowNoiseAccelerometer = true;
                api.EnableWideRangeAccelerometer = true;
                api.EnableGyroscope = true;
                api.EnableMagnetometer = true;
                api.EnablePressureTemperature = true;
                api.EnableBattery = true;
                api.EnableExtA6 = true;
                api.EnableExtA7 = true;
                api.EnableExtA15 = true;

                api.SamplingRate = DefaultSamplingRate;

                Console.WriteLine("Attempting connection on serial port " + comPort + " ...");
                api.Connect();
            }

            if (api.IsConnected())
            {
                Console.WriteLine("Device connected");

                Console.WriteLine("Sending StartStreaming message");
                api.StartStreaming();
                Console.WriteLine("Receiving data for " + (t / 1000) + "s ...");

                timer = HandleData(1); // 1Hz
                WaitAndDisconnect(t);
            }
            else
            {
                Console.WriteLine("Unable to connect to device");
            }
        }

        private static Timer HandleData(int hz)
        {
            var period = TimeSpan.FromMilliseconds(1000 / hz);

            return new Timer((e) =>
            {
                XR2Learn_ShimmerIMUData data = api.LatestData;
                if (data == null) return;

                Console.WriteLine("[" + data.TimeStamp.Data + "]");

                Console.WriteLine("LowNoise Accel X: " + data.AccelerometerX.Data + " [" + data.AccelerometerX.Unit + "] | " +
                                  "Y: " + data.AccelerometerY.Data + " [" + data.AccelerometerY.Unit + "] | " +
                                  "Z: " + data.AccelerometerZ.Data + " [" + data.AccelerometerZ.Unit + "]");

                Console.WriteLine("WideRange Accel X: " + data.WideRangeAccelerometerX.Data + " [" + data.WideRangeAccelerometerX.Unit + "] | " +
                                  "Y: " + data.WideRangeAccelerometerY.Data + " [" + data.WideRangeAccelerometerY.Unit + "] | " +
                                  "Z: " + data.WideRangeAccelerometerZ.Data + " [" + data.WideRangeAccelerometerZ.Unit + "]");

                Console.WriteLine("Gyro  X: " + data.GyroscopeX.Data + " [" + data.GyroscopeX.Unit + "] | " +
                                  "Y: " + data.GyroscopeY.Data + " [" + data.GyroscopeY.Unit + "] | " +
                                  "Z: " + data.GyroscopeZ.Data + " [" + data.GyroscopeZ.Unit + "]");

                Console.WriteLine("Mag   X: " + data.MagnetometerX.Data + " [" + data.MagnetometerX.Unit + "] | " +
                                  "Y: " + data.MagnetometerY.Data + " [" + data.MagnetometerY.Unit + "] | " +
                                  "Z: " + data.MagnetometerZ.Data + " [" + data.MagnetometerZ.Unit + "]");

                Console.WriteLine("Temperature BMP180: " + data.Temperature_BMP180.Data + " [" + data.Temperature_BMP180.Unit + "]");
                Console.WriteLine("Pressure BMP180: " + data.Pressure_BMP180.Data + " [" + data.Pressure_BMP180.Unit + "]");

                // Converti millivolt in volt
                double voltage = data.BatteryVoltage.Data / 1000.0;

                // Calcolo della percentuale basato su 3.0V - 4.2V
                double percentage = Math.Min(100, Math.Max(0, 100 * (voltage - 3.0) / (4.2 - 3.0)));

                // Stampa in millivolt e percentuale
                Console.WriteLine("Battery Voltage: " + (voltage * 1000).ToString("F0") + " [mV]");
                Console.WriteLine("Battery Percentage (calculated): " + percentage.ToString("F1") + " [%]");

                Console.WriteLine("Ext ADC A6: " + data.ExtADC_A6.Data + " [" + data.ExtADC_A6.Unit + "]");
                Console.WriteLine("Ext ADC A7: " + data.ExtADC_A7.Data + " [" + data.ExtADC_A7.Unit + "]");
                Console.WriteLine("Ext ADC A15: " + data.ExtADC_A15.Data + " [" + data.ExtADC_A15.Unit + "]");




            }, null, TimeSpan.Zero, period);

        }

        private static void WaitAndDisconnect(int t)
        {
            Thread.Sleep(t);
            timer.Dispose();

            Console.WriteLine("Sending StopStreaming message");
            api.StopStreaming();
            Console.WriteLine("Disconnecting");
            api.Disconnect();
            Console.WriteLine("Device disconnected");
        }
    }
}
