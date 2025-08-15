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

        // Ranges e flag passati dal chiamante (teniamo le firme ma su ANDROID evitiamo MAG/SR)
        private readonly int _accelRange, _gsrRange, _gyroRange;
        private readonly bool _lpAccel, _lpGyro;
        private readonly byte[]? _exg1, _exg2;
        private readonly bool _internalExpPower;
        private readonly int _sensorBitmap;

        private readonly ShimmerBluetoothAndroid _core;

        public double SamplingRate { get; } // non impostiamo su Android (NRE MAG), ma teniamo la proprietà
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
            bool enableLowPowerGyro, bool enableLowPowerMag, // ignorato lato ANDROID
            int gyroRange, int magRange,                    // ignorato lato ANDROID
            byte[]? exg1, byte[]? exg2,
            bool internalExpPower, int sensorBitmap)
        {
            _deviceId = devId ?? string.Empty;
            _mac = (macAddress ?? string.Empty).Trim();
            SamplingRate = samplingRate;

            _accelRange = accelRange; _gsrRange = gsrRange;
            _gyroRange = gyroRange;
            _lpAccel = enableLowPowerAccel; _lpGyro = enableLowPowerGyro;
            _exg1 = exg1; _exg2 = exg2;
            _internalExpPower = internalExpPower;
            _sensorBitmap = sensorBitmap;

            Log.Debug(Tag, $"Initialized for device {_deviceId} @ {_mac}");

            _core = new ShimmerBluetoothAndroid(
                _deviceId,
                () => _inputStream,
                () => _outputStream,
                () => _connected
            );
            _core.UICallback += (s, e) => UICallback?.Invoke(s, e);
        }

        // === API sincrona di comodo ===
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

                // *** AVVIA SUBITO IL READER ***
                _readCts = new CancellationTokenSource();
                _ = Task.Run(() =>
                {
                    try
                    {
                        _core.ReadData(); // gestisce inquiry/risposte e pacchetti streaming
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Error(Tag, "Core.ReadData() ended with exception:");
                        Android.Util.Log.Error(Tag, ex.ToString());
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                });
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
            try { _readCts?.Cancel(); } catch { }
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

            // Il reader è già attivo dalla Connect(); ora configuriamo in modo safe per Android.
            await ConfigureSamplingRateAndSensorsAsync();

            Log.Debug(Tag, "Sending START_STREAMING...");
            _core.StartStreaming();
            _streaming = true;
        }

        public async void StopStreaming()
        {
            Log.Debug(Tag, "StopStreaming called.");
            _streaming = false;

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

        /// <summary>
        /// Configurazione "safe" per Android: NIENTE MAG, NIENTE WriteSamplingRate (causa NRE nella lib).
        /// Il loop di lettura è già attivo, quindi Inquiry/ReadCal possono ricevere risposte.
        /// </summary>
       private async Task ConfigureSamplingRateAndSensorsAsync()
{
    // 0) STOP "duro" (due volte) + pausa
    await WriteAsync(new[] { (byte)ShimmerBluetooth.PacketTypeShimmer2.STOP_STREAMING_COMMAND });
    await Task.Delay(120);
    await WriteAsync(new[] { (byte)ShimmerBluetooth.PacketTypeShimmer2.STOP_STREAMING_COMMAND });
    await Task.Delay(180);

    // 0bis) Drena l’input per ~150ms (evita di partire a metà pacchetto)
    var start = DateTime.UtcNow;
    try
    {
        var s = _inputStream;
        var buf = new byte[256];
        while ((DateTime.UtcNow - start).TotalMilliseconds < 150)
        {
            if (s == null) break;
            // Read con timeout ridotto: se non hai un Timeout sullo stream, usa ReadAsync con piccolo delay
            if (s.CanRead && s is { })
            {
                // Non blocchiamoci: se non ci sono dati, interrompiamo subito il loop
                if (s is Android.Runtime.InputStreamInvoker inv && inv.BaseInputStream is Java.IO.InputStream jis && jis.Available() == 0)
                    break;

                // Leggi e scarta; se non ci sono dati ritorna 0/negativo e usciamo
                int n = await s.ReadAsync(buf, 0, buf.Length);
                if (n <= 0) break;
            }
            else break;
        }
    }
    catch { /* best-effort */ }

    // 1) Inquiry/Calib (reader già attivo)
    try
    {
        _core.Inquiry();
        await Task.Delay(150);
        _core.ReadCalibrationParameters("All");
    }
    catch (Exception ex)
    {
        Log.Warn(Tag, "Inquiry/ReadCalibrationParameters failed (continuo comunque): " + ex.Message);
    }

    string fw = _core.GetFirmwareVersionFullName() ?? string.Empty;
    Log.Debug(Tag, $"Firmware rilevato: {fw}");

    // 2) Scrivi sensori richiesti (BMP180 escluso se in prova)
    _core.WriteSensors(_sensorBitmap);
    await Task.Delay(80);

    // 3) Ranges sicuri
    try { _core.WriteAccelRange(_accelRange); } catch (Exception ex) { Log.Warn(Tag, "WriteAccelRange failed: " + ex.Message); }
    try { _core.WriteGSRRange(_gsrRange); }   catch (Exception ex) { Log.Warn(Tag, "WriteGSRRange failed: " + ex.Message); }
    try { _core.WriteGyroRange(_gyroRange); } catch (Exception ex) { Log.Warn(Tag, "WriteGyroRange failed: " + ex.Message); }

    // 4) Low power (no Mag su Android)
    try { _core.SetLowPowerAccel(_lpAccel); } catch { }
    try { _core.SetLowPowerGyro(_lpGyro); }   catch { }
    try { _core.WriteInternalExpPower(_internalExpPower ? 1 : 0); } catch { }

    // 5) EXG opzionale
    try
    {
        if (_exg1 != null && _exg2 != null)
            _core.WriteEXGConfigurations(_exg1, _exg2);
    }
    catch (Exception ex) { Log.Warn(Tag, "WriteEXGConfigurations failed: " + ex.Message); }

    // 6) **Sampling rate**: lasciamo quello presente sul device (su Android evitare path MAG/NRE)
    Log.Debug(Tag, $"Configured (Android) sensors=0x{_sensorBitmap:X}; SR skipped on Android to avoid MAG NRE");

    // 7) Piccolo respiro prima dello START
    await Task.Delay(120);
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

            public string GetFirmwareVersionFullName()
            {
                return FirmwareVersionFullName;
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
                // no-op
            }

            protected override void FlushInputConnection()
            {
                // Non c'è un modo portabile per svuotare l'RFCOMM stream qui; best-effort no-op.
            }
        }
    }
}
#endif
