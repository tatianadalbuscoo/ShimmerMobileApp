#if ANDROID
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Util;
using Java.Util;
using ShimmerAPI;
using ShimmerAPI.Utilities;

namespace XR2Learn_ShimmerAPI.IMU
{
    public class ShimmerLogAndStreamAndroidBluetooth
    {
        static readonly UUID SPP_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
        const string Tag = "ShimmerBT";

        private readonly string _deviceId;
        private readonly string _mac;

        private readonly ShimmerBluetoothAndroid _core;

        public double SamplingRate { get; }
        public string DeviceId => _deviceId;
        public string MacAddress => _mac;

        private bool _connected = false;
        private bool _streaming = false;
        private BluetoothSocket? _socket;
        private Stream? _inputStream;
        private Stream? _outputStream;
        private CancellationTokenSource? _readCts;

        public event EventHandler? UICallback;

        public ShimmerLogAndStreamAndroidBluetooth(
            string devId, string macAddress, double samplingRate,
            int accelRange, int gsrRange, bool enableLowPowerAccel,
            bool enableLowPowerGyro, bool enableLowPowerMag,
            int gyroRange, int magRange, byte[]? exg1, byte[]? exg2,
            bool internalExpPower)
        {
            _deviceId = devId ?? string.Empty;
            _mac = (macAddress ?? string.Empty).Trim();
            SamplingRate = samplingRate;

            Log.Debug(Tag, $"Initialized for device {_deviceId} @ {_mac}");

            _core = new ShimmerBluetoothAndroid(
                _deviceId,
                () => _inputStream,
                () => _outputStream,
                () => _connected
            );

            _core.UICallback += (s, e) => UICallback?.Invoke(s, e);
        }

        // === RITORNA BOOL ===
        public bool Connect()
        {
            try
            {
                ConnectAsync().GetAwaiter().GetResult();
                return _connected;
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Connect() error: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public async Task ConnectAsync()
        {
            Log.Debug(Tag, "Attempting ConnectAsync...");
            try
            {
                var adapter = BluetoothAdapter.DefaultAdapter ?? throw new Exception("BluetoothAdapter non disponibile");
                if (!adapter.IsEnabled) throw new Exception("Bluetooth disabilitato");

                var device = adapter.GetRemoteDevice(_mac);
                _socket = device.CreateRfcommSocketToServiceRecord(SPP_UUID);
                if (adapter.IsDiscovering) adapter.CancelDiscovery();

                await _socket.ConnectAsync();
                _inputStream = _socket.InputStream;
                _outputStream = _socket.OutputStream;
                _connected = true;
                Log.Debug(Tag, "Socket connesso!");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Errore ConnectAsync: {ex.Message}");
                Disconnect();
                throw;
            }
        }

        public void Disconnect()
        {
            Log.Debug(Tag, "Disconnecting...");
            _streaming = false;
            _connected = false;
            _readCts?.Cancel();
            try { _inputStream?.Close(); } catch { }
            try { _outputStream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
            _inputStream = null;
            _outputStream = null;
            _socket = null;
        }

        public bool IsConnected() => _connected;

        public async void StartStreaming()
        {
            Log.Debug(Tag, $"StartStreaming called. Connected? {_connected}");
            if (!_connected || _inputStream == null || _outputStream == null) return;

            await ConfigureSamplingRateAndSensorsAsync();

            await WriteAsync(new[] { (byte)ShimmerBluetooth.PacketTypeShimmer2.START_STREAMING_COMMAND });

            _streaming = true;
            _readCts = new CancellationTokenSource();

            _ = Task.Run(() =>
            {
                try
                {
                    _core.ReadData(); // parsing ShimmerAPI
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Core.ReadData() ended: {ex.Message}");
                }
            });
        }

        public async void StopStreaming()
        {
            Log.Debug(Tag, "StopStreaming called.");
            _streaming = false;
            _readCts?.Cancel();

            if (_connected && _outputStream != null)
                await WriteAsync(new[] { (byte)ShimmerBluetooth.PacketTypeShimmer2.STOP_STREAMING_COMMAND });
        }

        private async Task<int> WriteAsync(byte[] data, int offset = 0, int? count = null)
        {
            if (!_connected || _outputStream == null) return 0;
            int len = count ?? data.Length;
            await _outputStream.WriteAsync(data, offset, len);
            await _outputStream.FlushAsync();
            Log.Debug(Tag, $"WriteAsync: sent {len} bytes");
            return len;
        }

        private Task ConfigureSamplingRateAndSensorsAsync()
        {
            // TODO: inviare comandi configurazione se necessario
            return Task.CompletedTask;
        }
    }

    // ====== Bridge verso ShimmerBluetooth (override completi) ======
    internal class ShimmerBluetoothAndroid : ShimmerBluetooth
    {
        private readonly Func<Stream?> _getIn;
        private readonly Func<Stream?> _getOut;
        private readonly Func<bool> _isOpen;

        public ShimmerBluetoothAndroid(
            string deviceName,
            Func<Stream?> inputStreamGetter,
            Func<Stream?> outputStreamGetter,
            Func<bool> isOpenGetter
        )
            : base(deviceName)
        {
            _getIn = inputStreamGetter;
            _getOut = outputStreamGetter;
            _isOpen = isOpenGetter;
        }

        protected override int ReadByte()
        {
            var s = _getIn();
            if (s == null) throw new IOException("Input stream not available.");
            int b = s.ReadByte();
            if (b < 0) throw new IOException("End of stream.");
            return b & 0xFF;
        }

        protected override void WriteBytes(byte[] buffer, int index, int length)
        {
            var s = _getOut();
            if (s == null) throw new IOException("Output stream not available.");
            s.Write(buffer, index, length);
            s.Flush();
        }

        public override string GetShimmerAddress() => "ANDROID-BT";
        public override void SetShimmerAddress(string address) { /* no-op su Android */ }

        protected override bool IsConnectionOpen() => _isOpen();

        protected override void OpenConnection() { /* gestita fuori (ConnectAsync) */ }
        protected override void CloseConnection() { /* gestita fuori (Disconnect) */ }

        protected override void FlushConnection()
        {
            // no-op: RFCOMM stream non ha flush esplicito separato qui
        }

        protected override void FlushInputConnection()
        {
            var s = _getIn();
            if (s == null || !s.CanRead) return;
            try
            {
                // best effort: svuota buffer se supportato
                while (s.CanRead && s.Position < s.Length)
                {
                    if (s.ReadByte() < 0) break;
                }
            }
            catch { /* ignore */ }
        }
    }
}
#endif
