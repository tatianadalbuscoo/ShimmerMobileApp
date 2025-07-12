
using System;
using System.Threading;

namespace XR2Learn_ShimmerAPI
{
    /// <summary>
    /// Test class showcasing a simplified use case
    /// </summary>
    public class Test
    {
        private static readonly XR2Learn_ShimmerGSR api = new XR2Learn_ShimmerGSR();

        private static Timer timer; // keep global or it will be garbage-collected after few seconds
        private static int t = 60000; //ms
        private static string deviceName = "Shimmer3";

        public static void Main(string[] args)
        {
            string[] ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();
            Console.WriteLine("Available ports: [ " + string.Join(", ", ports) + " ]");
            foreach (string port in ports)
            {

                api.EnableAccelerator = true;
                api.EnableGSR = true;
                api.EnablePPG = true;

                string comPort = port;
                api.Configure(deviceName, comPort);

                api.NumberOfHeartBeatsToAverage = XR2Learn_ShimmerGSR.DefaultNumberOfHeartBeatsToAverage;
                api.TrainingPeriodPPG = XR2Learn_ShimmerGSR.DefaultTrainingPeriodPPG;
                api.LowPassFilterCutoff = XR2Learn_ShimmerGSR.DefaultLowPassFilterCutoff;
                api.HighPassFilterCutoff = XR2Learn_ShimmerGSR.DefaultHighPassFilterCutoff;
                api.SamplingRate = XR2Learn_ShimmerGSR.DefaultSamplingRate;

                // Modify and put before the api.Configure
                /*api.EnableAccelerator = false;
                api.EnableGSR = true;
                api.EnablePPG = true;*/

                Console.WriteLine("Attempting connection on serial port " + comPort + " ...");
                api.Connect();

                // Stop scanning once connected
                if (api.IsConnected())
                {
                    Console.WriteLine($"Successfully connected on {comPort}");
                    break;
                }
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
                XR2Learn_ShimmerGSRData data = api.LatestData; // get latest dataframe
                if (data == null) return;
                Console.WriteLine("[" + data.TimeStamp.Data + "] " + data.AcceleratorX.Data + " [" + data.AcceleratorX.Unit + "] | " + data.AcceleratorY.Data + " [" + data.AcceleratorY.Unit + "] | " + data.AcceleratorZ.Data + " [" + data.AcceleratorZ.Unit + "]");
                Console.WriteLine(" " + data.GalvanicSkinResponse.Data + " [" + data.GalvanicSkinResponse.Unit + "] | " + data.PhotoPlethysmoGram.Data + " [" + data.PhotoPlethysmoGram.Unit + "] | " + data.HeartRate + " [BPM]");
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
