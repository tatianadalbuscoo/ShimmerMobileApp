/* 
 * Windows-only helper that lists available COM ports and checks their availability. 
 * It acts as a thin wrapper over System.IO.Ports to help the app discover Shimmer devices. 
 */

#if WINDOWS
using System;
using System.IO.Ports;

namespace ShimmerSDK
{
    public class ShimmerSDK_SerialPortsManager
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
#endif
