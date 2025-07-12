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
            string[] ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();
            Console.WriteLine("Available ports: [ " + string.Join(", ", ports) + " ]");
            foreach (string port in ports)
            {
                string comPort = port;
                api.Configure(deviceName, comPort);

                api.EnableAccelerometer = true;
                api.EnableGyroscope = true;
                api.EnableMagnetometer = true;

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

                Console.WriteLine("Accel X: " + data.AccelerometerX.Data + " [" + data.AccelerometerX.Unit + "] | " +
                                  "Y: " + data.AccelerometerY.Data + " [" + data.AccelerometerY.Unit + "] | " +
                                  "Z: " + data.AccelerometerZ.Data + " [" + data.AccelerometerZ.Unit + "]");

                Console.WriteLine("Gyro  X: " + data.GyroscopeX.Data + " [" + data.GyroscopeX.Unit + "] | " +
                                  "Y: " + data.GyroscopeY.Data + " [" + data.GyroscopeY.Unit + "] | " +
                                  "Z: " + data.GyroscopeZ.Data + " [" + data.GyroscopeZ.Unit + "]");

                Console.WriteLine("Mag   X: " + data.MagnetometerX.Data + " [" + data.MagnetometerX.Unit + "] | " +
                                  "Y: " + data.MagnetometerY.Data + " [" + data.MagnetometerY.Unit + "] | " +
                                  "Z: " + data.MagnetometerZ.Data + " [" + data.MagnetometerZ.Unit + "]");
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
