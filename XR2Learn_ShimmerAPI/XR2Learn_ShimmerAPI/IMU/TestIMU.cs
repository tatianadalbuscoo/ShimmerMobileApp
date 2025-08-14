/*using System;
using System.Threading;

#if WINDOWS
using XR2Learn_ShimmerAPI.IMU;
#endif

namespace XR2Learn_ShimmerAPI
{

    /// <summary>
    /// Standalone test class for verifying communication with the Shimmer3 IMU using the XR2Learn_ShimmerIMU API.
    /// It handles sensor configuration, starts data streaming, and prints real-time sensor values to the console.
    /// Intended for functional testing and validation without any user interface or MAUI front-end integration.
    /// </summary>
    public class TestIMU
    {

#if WINDOWS
        // Instance of the IMU communication API used for configuration, connection, and data retrieval
        private static readonly XR2Learn_ShimmerIMU api = new XR2Learn_ShimmerIMU();

        // Timer used to periodically display sensor values (to avoid garbage collection, it's kept static)
        private static Timer timer;

        // Duration of the test in milliseconds
        private static int t = 60000;

        // Name of the device shown in logs
        private static string deviceName = "Shimmer3_IMU";

        // Default sampling rate in Hz
        private static readonly double DefaultSamplingRate = 51.2;

        /// <summary>
        /// Main entry point of the test. Configures the IMU, connects, streams data, and logs it to the console.
        /// </summary>
        public static void Main(string[] args)
        {

            // Manually specify the COM port of the device
            string comPort = "COM15";

            // Configure the IMU with all desired sensors enabled
            api.Configure(deviceName, comPort,
                enableLowNoiseAcc: true,
                enableWideRangeAcc: true,
                enableGyro: true,
                enableMag: true,
                enablePressureTemp: true,
                enableBattery: true,
                enableExtA6: true,
                enableExtA7: true,
                enableExtA15: true);

            // Set the sampling rate
            api.SamplingRate = DefaultSamplingRate;

            Console.WriteLine("Attempting connection on serial port " + comPort + " ...");

            // Attempt to connect to the IMU
            api.Connect();

            // Check connection success
            if (api.IsConnected())
            {
                Console.WriteLine("Device connected");

                // Start data streaming
                Console.WriteLine("Sending StartStreaming message");
                api.StartStreaming();
                Console.WriteLine("Receiving data for " + (t / 1000) + "s ...");

                // Start a timer that reads data every 1 second
                timer = HandleData(1);

                WaitAndDisconnect(t);
            }
            else
                Console.WriteLine("Unable to connect to device");
        }

        /// <summary>
        /// Periodically reads and prints sensor data to the console.
        /// </summary>
        /// <param name="hz">Frequency (in Hz) at which to print data</param>
        /// <returns>A Timer that periodically prints data</returns>
        private static Timer HandleData(int hz)
        {
            var period = TimeSpan.FromMilliseconds(1000 / hz);

            return new Timer((e) =>
            {
                // Retrieve the latest data frame
                XR2Learn_ShimmerIMUData data = api.LatestData;
                if (data == null) return;

                // Print timestamp
                Console.WriteLine("[" + data.TimeStamp.Data + "]");

                // Print Low-Noise Accelerometer data
                Console.WriteLine("LowNoise Accel X: " + data.LowNoiseAccelerometerX.Data + " [" + data.LowNoiseAccelerometerX.Unit + "] | " +
                                  "Y: " + data.LowNoiseAccelerometerY.Data + " [" + data.LowNoiseAccelerometerY.Unit + "] | " +
                                  "Z: " + data.LowNoiseAccelerometerZ.Data + " [" + data.LowNoiseAccelerometerZ.Unit + "]");

                // Print Wide-Range Accelerometer data
                Console.WriteLine("WideRange Accel X: " + data.WideRangeAccelerometerX.Data + " [" + data.WideRangeAccelerometerX.Unit + "] | " +
                                  "Y: " + data.WideRangeAccelerometerY.Data + " [" + data.WideRangeAccelerometerY.Unit + "] | " +
                                  "Z: " + data.WideRangeAccelerometerZ.Data + " [" + data.WideRangeAccelerometerZ.Unit + "]");

                // Print Gyroscope data
                Console.WriteLine("Gyro  X: " + data.GyroscopeX.Data + " [" + data.GyroscopeX.Unit + "] | " +
                                  "Y: " + data.GyroscopeY.Data + " [" + data.GyroscopeY.Unit + "] | " +
                                  "Z: " + data.GyroscopeZ.Data + " [" + data.GyroscopeZ.Unit + "]");

                // Print Magnetometer data
                Console.WriteLine("Mag   X: " + data.MagnetometerX.Data + " [" + data.MagnetometerX.Unit + "] | " +
                                  "Y: " + data.MagnetometerY.Data + " [" + data.MagnetometerY.Unit + "] | " +
                                  "Z: " + data.MagnetometerZ.Data + " [" + data.MagnetometerZ.Unit + "]");

                // Print Pressure and Temperature sensor data
                Console.WriteLine("Pressure BMP180: " + data.Pressure_BMP180.Data + " [" + data.Pressure_BMP180.Unit + "]");
                Console.WriteLine("Temperature BMP180: " + data.Temperature_BMP180.Data + " [" + data.Temperature_BMP180.Unit + "]");

                // Battery conversion from millivolts to volts
                double voltage = data.BatteryVoltage.Data / 1000.0;

                // Calculate battery level percentage (between 3.0V and 4.2V)
                double percentage = Math.Min(100, Math.Max(0, 100 * (voltage - 3.0) / (4.2 - 3.0)));

                // Print battery status
                Console.WriteLine("Battery Voltage: " + (voltage * 1000).ToString("F0") + " [mV]");
                Console.WriteLine("Battery Percentage (calculated): " + percentage.ToString("F1") + " [%]");

                // Print External ADCs
                Console.WriteLine("Ext ADC A6: " + data.ExtADC_A6.Data + " [" + data.ExtADC_A6.Unit + "]");
                Console.WriteLine("Ext ADC A7: " + data.ExtADC_A7.Data + " [" + data.ExtADC_A7.Unit + "]");
                Console.WriteLine("Ext ADC A15: " + data.ExtADC_A15.Data + " [" + data.ExtADC_A15.Unit + "]");

            }, null, TimeSpan.Zero, period);

        }

        /// <summary>
        /// Waits for the defined duration, then stops streaming and disconnects from the device.
        /// </summary>
        /// <param name="t">Time in milliseconds before shutdown</param>
        private static void WaitAndDisconnect(int t)
        {
            // Wait for test duration
            Thread.Sleep(t);

            // Dispose the timer
            timer.Dispose();

            // Stop the IMU stream
            Console.WriteLine("Sending StopStreaming message");
            api.StopStreaming();

            // Disconnect the IMU device
            Console.WriteLine("Disconnecting");
            api.Disconnect();
            Console.WriteLine("Device disconnected");
        }
#else
        /// <summary>
        /// Main entry point - stub per piattaforme non-Windows
        /// </summary>
        public static void Main(string[] args)
        {
            Console.WriteLine("TestIMU non supportato su questa piattaforma. Funziona solo su Windows.");
            Console.WriteLine("Premi un tasto per uscire...");
            Console.ReadKey();
        }
#endif
    }
}*/