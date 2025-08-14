#if ANDROID
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Util;
using Java.Util;
using ShimmerAPI; // per PacketTypeShimmer2

namespace XR2Learn_ShimmerAPI.IMU
{
    public class ShimmerLogAndStreamAndroidBluetooth
    {
        static readonly UUID SPP_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
        const string Tag = "ShimmerBT";
        private const byte CMD_START_STREAMING = 0x07;  // Start streaming

        public double SamplingRate { get; }
        public string DeviceId { get; }
        public string MacAddress { get; }

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

        public void Connect() => ConnectAsync().GetAwaiter().GetResult();

        public async Task ConnectAsync()
        {
            Log.Debug(Tag, "Attempting ConnectAsync...");
            try
            {
                var adapter = BluetoothAdapter.DefaultAdapter ?? throw new Exception("BluetoothAdapter non disponibile");
                if (!adapter.IsEnabled) throw new Exception("Bluetooth disabilitato");

                var device = adapter.GetRemoteDevice(MacAddress);
                _socket = device.CreateRfcommSocketToServiceRecord(SPP_UUID);
                if (adapter.IsDiscovering) adapter.CancelDiscovery();

                await _socket.ConnectAsync();
                _inputStream = _socket.InputStream;
                _outputStream = _socket.OutputStream;
                _connected = true;
                Log.Debug(Tag, "Socket connesso!");
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
            try { _outputStream?.Close(); } catch { }
            try { _socket?.Close(); } catch { }
            _inputStream = null;
            _outputStream = null;
            _socket = null;
            UICallback?.Invoke(this, EventArgs.Empty);
        }

        public bool IsConnected() => _connected;

        // 🔹 UNICA StartStreaming (niente duplicati)
        public async void StartStreaming()
        {
            Log.Debug(Tag, $"StartStreaming called. Connected? {_connected}");
            if (!_connected || _inputStream == null || _outputStream == null) return;

            // TODO: inviare sampling rate + sensor bitmap come su Windows
            await ConfigureSamplingRateAndSensorsAsync();

            // Comando ufficiale di start (uguale a Windows)
            await WriteAsync(new byte[] { CMD_START_STREAMING });


            _streaming = true;
            _readCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoop(_readCts.Token));
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
                        Log.Debug(Tag, $"ReadLoop received {read} bytes: {BitConverter.ToString(buffer, 0, Math.Min(8, read))}...");
                    else
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"ReadLoop exception: {ex.Message}");
                Disconnect();
            }
            Log.Debug(Tag, "ReadLoop terminated.");
        }

        // --- UTIL: scrittura RFCOMM
        private async Task<int> WriteAsync(byte[] data, int offset = 0, int? count = null)
        {
            if (!_connected || _outputStream == null) return 0;
            int len = count ?? data.Length;
            await _outputStream.WriteAsync(data, offset, len);
            await _outputStream.FlushAsync();
            Log.Debug(Tag, $"WriteAsync: sent {len} bytes");
            return len;
        }

        // --- TODO: inviare sampling rate & bitmap sensori (stub per ora)
        private Task ConfigureSamplingRateAndSensorsAsync()
        {
            // In Windows qui si manda: SR + bitmap sensori + range ecc.
            // Per il momento non inviamo niente: molti Shimmer partono comunque.
            // Poi lo implementiamo copiando i comandi ShimmerAPI.
            return Task.CompletedTask;
        }

        // Proprietà extra
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
