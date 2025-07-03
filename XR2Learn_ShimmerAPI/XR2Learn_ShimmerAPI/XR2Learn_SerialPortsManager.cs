using System;
using System.IO.Ports;

namespace XR2Learn_ShimmerAPI
{
    public class XR2Learn_SerialPortsManager
    {
        /// <summary>
        /// Returns a list of available serial ports
        /// </summary>
        /// <returns>List of available serial ports</returns>
        public static string[] GetAvailableSerialPortsNames()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Returns if the given port is available
        /// </summary>
        /// <param name="port">Port to check</param>
        /// <returns>True if the given port is available, False otherwise</returns>
        public static bool IsPortAvailable(string port)
        {
            return Array.IndexOf(SerialPort.GetPortNames(), port) > -1;
        }
    }
}
