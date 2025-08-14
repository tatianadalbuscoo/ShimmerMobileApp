#if ANDROID
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Util;
using Java.Util;

namespace XR2Learn_ShimmerAPI.IMU
{
    public class ShimmerLogAndStreamAndroidBluetooth
    {
        static readonly UUID SPP_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
        const string Tag = "ShimmerBT";

        public double SamplingRate { get; } // aggiungi questa proprietà


        public string DeviceId { get; }
        public string MacAddress { get; }
        // ... altri campi come prima ...

        private bool _connected = false;
        private bool _streaming = false;
        private BluetoothSocket? _socket;
        private Stream? _inputStream;
        private CancellationTokenSource? _readCts;

        public event EventHandler? UICallback;

        public ShimmerLogAndStreamAndroidBluetooth(
            string devId, string macAddress, double samplingRate,
            int accelRange, int gsrRange, bool enableLowPowerAccel,
            bool enableLowPowerGyro, bool enableLowPowerMag,
            int gyroRange, int magRange, byte[]? exg1, byte[]? exg2,
            bool internalExpPower)
        {
            DeviceId = devId;
            MacAddress = macAddress;
            SamplingRate = samplingRate;
            AccelRange = accelRange;
            GsrRange = gsrRange;
            EnableLowPowerAccel = enableLowPowerAccel;
            EnableLowPowerGyro = enableLowPowerGyro;
            EnableLowPowerMag = enableLowPowerMag;
            GyroRange = gyroRange;
            MagRange = magRange;
            Exg1 = exg1;
            Exg2 = exg2;
            InternalExpPower = internalExpPower;
            Log.Debug(Tag, $"Initialized for device {DeviceId} @ {MacAddress}");
        }

        public void Connect()
        {
            // Mantiene compatibilità col vecchio codice sincrono
            ConnectAsync().GetAwaiter().GetResult();
        }


        public async Task ConnectAsync()
        {
            Log.Debug(Tag, "Attempting ConnectAsync...");
            try
            {
                var adapter = BluetoothAdapter.DefaultAdapter;
                if (adapter == null)
                    throw new Exception("BluetoothAdapter non disponibile");
                if (!adapter.IsEnabled)
                    throw new Exception("Bluetooth disabilitato");

                Log.Debug(Tag, "Bluetooth adapter OK, resolving device...");
                var device = adapter.GetRemoteDevice(MacAddress);
                _socket = device.CreateRfcommSocketToServiceRecord(SPP_UUID);
                Log.Debug(Tag, "Socket creata, canceling discovery...");
                if (adapter.IsDiscovering) adapter.CancelDiscovery();

                await _socket.ConnectAsync();
                Log.Debug(Tag, "Socket connesso!");

                _inputStream = _socket.InputStream;
                _connected = true;
                Log.Debug(Tag, "Invoco UICallback (connected)");
                UICallback?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Errore ConnectAsync: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            Log.Debug(Tag, "Disconnecting...");
            _streaming = false;
            _connected = false;
            _readCts?.Cancel();
            try { _inputStream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
            _inputStream = null;
            _socket = null;
            Log.Debug(Tag, "Handshake Disconnect done—invoking UICallback");
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public bool IsConnected() => _connected;

        public void StartStreaming()
        {
            Log.Debug(Tag, $"StartStreaming called. Connected? {_connected}");
            if (!_connected || _inputStream == null) return;

            _streaming = true;
            _readCts = new CancellationTokenSource();
            Task.Run(() => ReadLoop(_readCts.Token));

            Log.Debug(Tag, "Streaming started—UICallback");
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public void StopStreaming()
        {
            Log.Debug(Tag, "StopStreaming called.");
            _streaming = false;
            _readCts?.Cancel();
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        private async Task ReadLoop(CancellationToken token)
        {
            Log.Debug(Tag, "ReadLoop started.");
            var buffer = new byte[512];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read = await _inputStream!.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read > 0)
                    {
                        Log.Debug(Tag, $"ReadLoop received {read} bytes: {BitConverter.ToString(buffer, 0, Math.Min(8, read))}...");
                    }
                    else
                    {
                        Log.Warn(Tag, "ReadLoop: no bytes read, breaking.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"ReadLoop exception: {ex.Message}");
                Disconnect();
            }
            Log.Debug(Tag, "ReadLoop terminated.");
        }

        // Proprietà esistenti copiati...
        public int AccelRange { get; }
        public int GsrRange { get; }
        public bool EnableLowPowerAccel { get; }
        public bool EnableLowPowerGyro { get; }
        public bool EnableLowPowerMag { get; }
        public int GyroRange { get; }
        public int MagRange { get; }
        public byte[]? Exg1 { get; }
        public byte[]? Exg2 { get; }
        public bool InternalExpPower { get; }
    }
}
#endif
