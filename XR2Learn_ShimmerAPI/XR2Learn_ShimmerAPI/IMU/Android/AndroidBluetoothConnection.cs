#if ANDROID
using System;
using System.IO;
using Android.Bluetooth;
using Java.Util;

namespace XR2Learn_ShimmerAPI.IMU.Android
{
    /// <summary>Connessione RFCOMM SPP verso Shimmer.</summary>
    internal sealed class AndroidBluetoothConnection : IShimmerConnection
    {
        private static readonly UUID SPP_UUID =
            UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

        private readonly string _mac;
        private BluetoothSocket? _socket;
        private Stream? _in;
        private Stream? _out;

        public AndroidBluetoothConnection(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) throw new ArgumentException("MAC vuoto", nameof(mac));
            _mac = mac.Trim();
        }

        public bool IsOpen => _socket?.IsConnected == true && _in != null && _out != null;

        public void Open()
        {
            if (IsOpen) return;

            var adapter = BluetoothAdapter.DefaultAdapter ?? throw new InvalidOperationException("BluetoothAdapter non disponibile");
            if (!adapter.IsEnabled) throw new InvalidOperationException("Bluetooth disabilitato");

            var device = adapter.GetRemoteDevice(_mac);
            var sock = device.CreateRfcommSocketToServiceRecord(SPP_UUID);
            if (adapter.IsDiscovering) adapter.CancelDiscovery();
            sock.Connect();

            _socket = sock;
            _in = _socket.InputStream;
            _out = _socket.OutputStream;
        }

        public void Close()
        {
            try { _in?.Close(); } catch { }
            try { _out?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
            _in = null; _out = null; _socket = null;
        }

        public int ReadByte()
        {
            var s = _in ?? throw new IOException("Input stream non disponibile");
            int b = s.ReadByte();
            if (b < 0) throw new IOException("End of stream");

            //Console.WriteLine($"Byte letto: {b & 0xFF}");
            return b & 0xFF;
        }

        public void WriteBytes(byte[] buffer, int index, int length)
        {
            var s = _out ?? throw new IOException("Output stream non disponibile");
            s.Write(buffer, index, length);
            s.Flush();
        }

        public void Flush() { /* no-op */ }
        public void FlushInput() { /* no-op */ }
    }
}
#endif
