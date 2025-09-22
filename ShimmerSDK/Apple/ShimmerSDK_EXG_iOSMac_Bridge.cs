// Shimmer EXG iOS/Mac client
// Connects to the Android WebSocket bridge, performs the handshake, subscribes to a target MAC,
// syncs EXG configuration, starts/stops streaming,
// and parses samples into ShimmerSDK_EXGData.
// Configure via BridgeHost / BridgePort / BridgePath and BridgeTargetMac.
// Raises SampleReceived on the main (UI) thread when new data arrives, and ExgModeChanged when the EXG mode changes.


#if IOS || MACCATALYST


using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using UIKit;       
using Foundation;


namespace ShimmerSDK.EXG
{

    /// <summary>
    /// iOS/Mac Catalyst client for reading EXG data from a Shimmer device via an Android WebSocket bridge.
    /// Manages the WS connection/handshake, subscribes to a target MAC, syncs configuration,
    /// starts/stops streaming, and parses incoming samples into <see cref="ShimmerSDK_EXGData"/> (raising <c>SampleReceived</c> on the UI thread).
    /// </summary>
    public partial class ShimmerSDK_EXG
    {

        // IP/hostname of the WebSocket bridge (The Android device relaying Shimmer data)
        public string BridgeHost { get; set; } = "192.168.43.1";

        // WebSocket bridge port
        public int    BridgePort { get; set; } = 8787;

        // URL path for the bridge
        public string BridgePath { get; set; } = "/";

        // Target Shimmer device MAC address to open via the bridge
        public string BridgeTargetMac { get; set; } = "";

        // Active WebSocket to the bridge
        private ClientWebSocket? _ws;

        // Cancellation token source for RX loop / pending ops
        private CancellationTokenSource? _wsCts;

        // Background task running the receive loop
        private Task? _rxLoop;

        // True if the bridge connection is open
        private volatile bool _bridgeConnected;

        // True after start_ack and data streaming is active
        private volatile bool _isStreaming;

        // True once the bridge confirmed "open" for the target MAC
        private volatile bool _subscribed;

        // True after at least one valid sample was received
        private volatile bool _gotAnySample;

        // Awaiters for control ACKs (hello/open/config/start)
        private TaskCompletionSource<bool>? _tcsHello, _tcsOpen, _tcsConfig, _tcsStart;

        // Awaiter for set_sampling_rate ACK carrying the applied value
        private TaskCompletionSource<double>? _tcsSetSR;

        // Normalized current EXG mode id
        private string _currentExgMode = "";

        // Fired when the EXG mode changes; argument is the UI-friendly title, raised on the main thread.
        public event EventHandler<string>? ExgModeChanged;

        /// <summary>
        /// Indicates whether a valid EXG mode is currently selected.
        /// </summary>
        /// <returns>
        /// <c>true</c> if <see cref="CurrentExgModeTitle"/> is not empty; otherwise, <c>false</c>.
        /// </returns>
        public bool HasExgMode 
            => !string.IsNullOrEmpty(CurrentExgModeTitle);

        
        /// <summary>
        /// Checks whether the given object is a built-in numeric primitive type
        /// (signed/unsigned integers or floating-point types).
        /// </summary>
        /// <param name="v">Object to test; may be <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="v"/> is one of:
        /// sbyte, byte, short, ushort, int, uint, long, ulong, float, double, or decimal;
        /// otherwise <c>false</c>.
        /// </returns>
        private static bool IsNum(object? v) =>
            v is sbyte or byte or short or ushort or int or uint or long or ulong
             or float or double or decimal;


        /// <summary>
        /// Indicates whether the bridge WebSocket is currently connected and open.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the bridge reports connected and the underlying WebSocket exists
        /// and is in the <see cref="WebSocketState.Open"/> state; otherwise <c>false</c>.
        /// </returns>
        private bool IsConnectedMac() => 
            _bridgeConnected && _ws != null && _ws.State == WebSocketState.Open;

          
        /// <summary>
        /// Returns whether <paramref name="d"/> contains at least one populated numeric field.
        /// </summary>
        /// <param name="d">
        /// The latest <see cref="ShimmerSDK_EXGData"/> sample to inspect; may be <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if any accelerometer (LNA/WRA), gyroscope, magnetometer, temperature,
        /// pressure, battery voltage, external ADC (A6/A7/A15), or EXG channel is present and numeric;
        /// otherwise <c>false</c>.
        /// </returns>
        private static bool HasAnyValue(ShimmerSDK_EXGData d) =>
            d != null &&
            (IsNumLike(d.LowNoiseAccelerometerX) || IsNumLike(d.LowNoiseAccelerometerY) || IsNumLike(d.LowNoiseAccelerometerZ) ||
             IsNumLike(d.WideRangeAccelerometerX) || IsNumLike(d.WideRangeAccelerometerY) || IsNumLike(d.WideRangeAccelerometerZ) ||
             IsNumLike(d.GyroscopeX) || IsNumLike(d.GyroscopeY) || IsNumLike(d.GyroscopeZ) ||
             IsNumLike(d.MagnetometerX) || IsNumLike(d.MagnetometerY) || IsNumLike(d.MagnetometerZ) ||
             IsNumLike(d.Temperature_BMP180) || IsNumLike(d.Pressure_BMP180) ||
             IsNumLike(d.BatteryVoltage) ||
             IsNumLike(d.ExtADC_A6) || IsNumLike(d.ExtADC_A7) || IsNumLike(d.ExtADC_A15) ||
             IsNumLike(d.Exg1) || IsNumLike(d.Exg2));


        /// <summary>
        /// Gets a user-friendly title for the current EXG mode (e.g., “ECG”, “EMG”, “EXG Test”, “Respiration”); 
        /// returns an empty string if unknown.
        /// </summary>
        /// <value>A human-readable mode title derived from <c>_currentExgMode</c>.</value>
        public string CurrentExgModeTitle => _currentExgMode switch
        {
            "ecg" => "ECG",
            "emg" => "EMG",
            "test" => "EXG Test",
            "resp" or "respiration" => "Respiration",
            _ => ""
        };


        /// <summary>
        /// Gets the most recent EXG sample received from the bridge.
        /// </summary>
        /// <value>
        /// A <see cref="ShimmerSDK_EXGData"/> instance with the latest values, or <c>null</c>
        /// if no sample has been received yet.
        /// </value>
        public ShimmerSDK_EXGData? LatestData 
        {
            get; 
            private set; 
        }


        /// <summary>
        /// Normalizes and sets the EXG mode key; when it changes, raises <see cref="ExgModeChanged"/>
        /// on the UI thread with <see cref="CurrentExgModeTitle"/>.
        /// </summary>
        /// <returns>The normalized (lowercased/trimmed) EXG mode key.</returns>
        public string CurrentExgMode
        {
            get => _currentExgMode;
            private set
            {
                var norm = (value ?? "").Trim().ToLowerInvariant();
                if (_currentExgMode == norm) return;
                _currentExgMode = norm;

                var title = CurrentExgModeTitle;
                RunOnMainThread(() => ExgModeChanged?.Invoke(this, title));

            }
        }


        /// <summary>
        /// Executes the given action on the iOS main (UI) thread.
        /// </summary>
        /// <param name="action">The delegate to run; ignored if <c>null</c>.</param>
        private static void RunOnMainThread(Action action)
        {
            if (action == null) 
                return;
            if (NSThread.IsMain) 
                action();
            else 
                UIApplication.SharedApplication.BeginInvokeOnMainThread(action);
        }


        /// <summary>
        /// Determines whether an object can be treated as a numeric value.
        /// Checks primitive numeric types, the <see cref="NumericPayload"/> wrapper,
        /// and any object exposing a numeric <c>Data</c> property.
        /// </summary>
        /// <param name="v">The object to inspect; may be <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="v"/> is a primitive number, a
        /// <see cref="NumericPayload"/>, or an object whose <c>Data</c> property
        /// is numeric; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsNumLike(object? v)
        {
            if (IsNum(v)) return true;
            if (v is NumericPayload) return true;

            var p = v?.GetType().GetProperty("Data");
            if (p != null)
            {
                var inner = p.GetValue(v);
                return IsNum(inner);
            }
            return false;
        }

       
        /// <summary>
        /// Awaits an ACK TaskCompletionSource with a hard timeout and throws on failure.
        /// </summary>
        /// <param name="tcs">The ACK <see cref="TaskCompletionSource{Boolean}"/> to await.</param>
        /// <param name="what">Short label of the awaited operation (used in the exception message).</param>
        /// <param name="ms">Timeout in milliseconds.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the timeout elapses or if the ACK resolves to <c>false</c>.
        /// </exception>
        /// <returns>A task that completes when the ACK is received or throws on timeout/failure.</returns>
        private static async Task WaitOrThrow(TaskCompletionSource<bool> tcs, string what, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            if (done != tcs.Task || !tcs.Task.Result)
                throw new InvalidOperationException($"{what} timeout/failed");
        }


        /// <summary>
        /// Awaits an ACK TaskCompletionSource with a soft timeout, returning success/failure.
        /// </summary>
        /// <param name="tcs">The ACK <see cref="TaskCompletionSource{Boolean}"/> to await.</param>
        /// <param name="ms">Timeout in milliseconds.</param>
        /// <returns>
        /// <c>true</c> if the ACK completes within the timeout and its result is <c>true</c>;
        /// otherwise <c>false</c>.
        /// </returns>
        private static async Task<bool> WaitSoft(TaskCompletionSource<bool> tcs, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            return done == tcs.Task && tcs.Task.Result;
        }


        /// <summary>
        /// Ensures the bridge is subscribed (opened) to the target MAC, retrying a few times.
        /// </summary>
        /// <returns>A task that completes when subscription is confirmed or retries are exhausted.</returns>
        private async Task EnsureSubscribedAsync()
        {
            if (_subscribed) return;
            for (int i = 0; i < 8 && !_subscribed; i++)
            {
                _tcsOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await SendJsonAsync(new { type = "open", mac = BridgeTargetMac }).ConfigureAwait(false);
                await Task.WhenAny(_tcsOpen.Task, Task.Delay(600)).ConfigureAwait(false);
                if (_subscribed) break;
            }
        }


        /// <summary>
        /// Ensures there is an open WebSocket connection to the bridge.
        /// </summary>
        /// <returns>A task that completes when the socket is connected and the RX loop is started.</returns>
        /// <exception cref="WebSocketException">Thrown if the connection attempt fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the connect operation is canceled.</exception>
        private async Task EnsureWebSocketAsync()
        {
            if (_ws != null && _ws.State == WebSocketState.Open) return;

            await CloseWebSocketAsync().ConfigureAwait(false);

            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            _wsCts = new CancellationTokenSource();

            var uri = new UriBuilder("ws", BridgeHost, BridgePort, string.IsNullOrWhiteSpace(BridgePath) ? "/" : BridgePath).Uri;
            await _ws.ConnectAsync(uri, _wsCts.Token).ConfigureAwait(false);
            _bridgeConnected = true;

            _rxLoop = Task.Run(() => ReceiveLoopAsync(_wsCts!.Token));
        }


        /// <summary>
        /// Closes and disposes the current WebSocket session and related resources,
        /// cancelling the receive loop if running.
        /// </summary>
        /// <returns>A task that completes when the socket has been closed and resources disposed.</returns>
        private async Task CloseWebSocketAsync()
        {
            _bridgeConnected = false;
            _isStreaming = false;

            var ws = _ws; _ws = null;
            try { _wsCts?.Cancel(); } catch { }
            try { if (_rxLoop != null) await _rxLoop.ConfigureAwait(false); } catch { }

            if (ws != null)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false); } catch { }
                ws.Dispose();
            }
            _wsCts?.Dispose(); _wsCts = null; _rxLoop = null;
        }


        /// <summary>
        /// Opens (or reuses) the WebSocket to the Android bridge, completes the initial handshake,
        /// subscribes to the target device (MAC), and requests/apply current sensor configuration.
        /// </summary>
        /// <returns>
        /// A task that completes after the hello handshake, subscription, and config request/push
        /// have been attempted.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if required properties are missing/invalid, or if the hello ACK times out/fails.
        /// </exception>
        private async Task ConnectMacAsync()
        {

            // Validate mandatory bridge settings
            if (string.IsNullOrWhiteSpace(BridgeHost)) throw new InvalidOperationException("BridgeHost not set");
            if (BridgePort <= 0) throw new InvalidOperationException("Invalid BridgePort");
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac not set");

            // Ensure the WebSocket is connected
            await EnsureWebSocketAsync().ConfigureAwait(false);

            // Handshake: send "hello" and wait for a hard ACK
            _tcsHello = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "hello" }).ConfigureAwait(false);
            await WaitOrThrow(_tcsHello, "hello_ack", 3000).ConfigureAwait(false);

            // Subscribe to the target MAC 
            _subscribed = false;
            await EnsureSubscribedAsync().ConfigureAwait(false);

            // Ask the bridge for the current config 
            await SendJsonAsync(new { type = "get_config", mac = BridgeTargetMac }).ConfigureAwait(false);
        }


        /// <summary>
        /// Starts data streaming from the Shimmer device through the bridge.
        /// Ensures there is an active WebSocket connection and a valid subscription,
        /// then sends a <c>start</c> command and waits for <c>start_ack</c>.
        /// </summary>
        /// <returns>A task that completes when streaming has successfully been started.</returns>
        private async Task StartStreamingMacAsync()
        {
            if (!_bridgeConnected) await ConnectMacAsync().ConfigureAwait(false);

            // Make sure we are subscribed to the target MAC before starting the stream.
            await EnsureSubscribedAsync().ConfigureAwait(false);

            // Send 'start' (including MAC so the server knows which stream to start) and await ack.
            _tcsStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "start", mac = BridgeTargetMac }).ConfigureAwait(false);
            await WaitOrThrow(_tcsStart, "start_ack", 12000).ConfigureAwait(false);

            _isStreaming = true;
        }


        /// <summary>
        /// Stops data streaming from the Shimmer device through the bridge.
        /// Sends a best-effort <c>stop</c> command and clears the streaming flag.
        /// </summary>
        /// <returns>A task that completes after the stop command has been sent (best effort).</returns>
        private async Task StopStreamingMacAsync()
        {
            if (_ws == null) return;
            try 
            { 
                await SendJsonAsync(new { type = "stop" }).ConfigureAwait(false); 
            } 
            catch {}
            _isStreaming = false;
        }


        /// <summary>
        /// Gracefully disconnects from the bridge: stops streaming, asks the bridge to close
        /// the current session, and tears down the WebSocket connection.
        /// </summary>
        /// <returns>A task that completes when the disconnection sequence has finished.</returns>
        /// <exception cref="ObjectDisposedException">
        /// May be thrown internally by the WebSocket if it is disposed while closing.
        /// </exception>
        private async Task DisconnectMacAsync()
        {
            await StopStreamingMacAsync().ConfigureAwait(false);
            if (_ws != null)
            {
                try { await SendJsonAsync(new { type = "close" }).ConfigureAwait(false); } catch { }
            }
            await CloseWebSocketAsync().ConfigureAwait(false);
        }


        /// <summary>
        /// Requests the firmware to apply the sampling rate closest to <paramref name="desiredHz"/>
        /// via the WebSocket bridge, waits for the ACK carrying the applied value, and updates
        /// <c>SamplingRate</c> with the result.
        /// </summary>
        /// <param name="desiredHz">Desired sampling rate in Hertz. Must be greater than zero.</param>
        /// <returns>
        /// The sampling rate in Hertz that the firmware actually applied (nearest available).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="desiredHz"/> is less than or equal to zero.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <c>BridgeTargetMac</c> is not set or when the bridge reports a failure
        /// to apply the sampling rate.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the bridge does not acknowledge the rate change within the timeout.
        /// </exception>
        private async Task<double> SetFirmwareSamplingRateNearestImpl(double desiredHz)
        {
            if (desiredHz <= 0) throw new ArgumentOutOfRangeException(nameof(desiredHz));
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac not set");

            // Ensure transport/session are ready before sending the request.
            await EnsureWebSocketAsync().ConfigureAwait(false);
            await EnsureSubscribedAsync().ConfigureAwait(false);

            // Send request and await ACK that returns the applied value.
            _tcsSetSR = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "set_sampling_rate", mac = BridgeTargetMac, sr = desiredHz }).ConfigureAwait(false);

            var done = await Task.WhenAny(_tcsSetSR.Task, Task.Delay(6000)).ConfigureAwait(false);
            if (done != _tcsSetSR.Task) throw new TimeoutException("set_sampling_rate timeout");

            double applied = _tcsSetSR.Task.Result;
            if (applied <= 0) throw new InvalidOperationException("set_sampling_rate failed");

            SamplingRate = applied;
            return applied;
        }


        /// <summary>
        /// Serializes <paramref name="payload"/> to JSON and sends it over the WebSocket as a text frame.
        /// </summary>
        /// <param name="payload">The object to serialize and send.</param>
        /// <returns>A task that completes when the frame has been sent.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the WebSocket is not connected.
        /// </exception>
        private async Task SendJsonAsync(object payload)
        {
            if (_ws == null) throw new InvalidOperationException("WebSocket not connected");
            string json = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: _wsCts?.Token ?? CancellationToken.None
            ).ConfigureAwait(false);
        }


        /// <summary>
        /// Parses and handles a single control/message frame coming from the bridge,
        /// updating ACK TaskCompletionSources, local configuration state, and the latest sample.
        /// </summary>
        /// <param name="txt">The JSON text payload received over the WebSocket.</param>
        private void HandleControlMessage(string txt)
        {
            try
            {
                using var doc = JsonDocument.Parse(txt);
                var root = doc.RootElement;

                // Every control message must carry a "type"
                if (!root.TryGetProperty("type", out var t)) return;
                var type = t.GetString();

                switch (type)
                {
                    case "hello_ack":

                        // Resolve the 'hello' handshake TCS with the boolean result.
                        _tcsHello?.TrySetResult(root.TryGetProperty("ok", out var ok1) && ok1.GetBoolean());
                        break;

                    case "open_ack":
                    {

                        
                        bool ok = root.TryGetProperty("ok", out var ok2) && ok2.GetBoolean();
                        _subscribed = ok;
                        _tcsOpen?.TrySetResult(ok);
                        break;
                    }

                    case "config_changed":
                    {
                        
                        // If we ever receive a plain "config" with ok=false, bail out early.
                        if (type == "config" && root.TryGetProperty("ok", out var okEl) && !okEl.GetBoolean())
                            break;

                        // Parse the "cfg" object that carries the current device configuration.
                        if (root.TryGetProperty("cfg", out var cfg) && cfg.ValueKind == JsonValueKind.Object)
                        {
                            
                            // Update current EXG mode
                            if (cfg.TryGetProperty("exg_mode", out var m) && m.ValueKind == JsonValueKind.String)
                            {
                                var raw = m.GetString();
                                var normalized = (raw ?? "").Trim().ToLowerInvariant();
                                CurrentExgMode = normalized;
                            }

                            bool Get(string name) => cfg.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
                            
                            // Sync sensor enablement flags
                            EnableLowNoiseAccelerometer  = Get("EnableLowNoiseAccelerometer");
                            EnableWideRangeAccelerometer = Get("EnableWideRangeAccelerometer");
                            EnableGyroscope              = Get("EnableGyroscope");
                            EnableMagnetometer           = Get("EnableMagnetometer");
                            EnablePressureTemperature    = Get("EnablePressureTemperature");
                            EnableBatteryVoltage                = Get("EnableBattery");
                            EnableExtA6                  = Get("EnableExtA6");
                            EnableExtA7                  = Get("EnableExtA7");
                            EnableExtA15                 = Get("EnableExtA15");

                            // Keep local SR in sync if the bridge reports it
                            if (cfg.TryGetProperty("SamplingRate", out var srEl) && srEl.ValueKind == JsonValueKind.Number)
                                SamplingRate = srEl.GetDouble();

                            // Dispatch the latest sample to subscribers on the main (UI) thread
                            RunOnMainThread(() => SampleReceived?.Invoke(this, LatestData));
                        }

                        break;

                      }

                    case "config_ack":
                    {

                        // Mark whether subscription/open succeeded for this MAC.
                        bool ok3 = root.TryGetProperty("ok", out var okCfg) && okCfg.GetBoolean();
                        _tcsConfig?.TrySetResult(ok3);
                        break;
                    }

                    case "set_sampling_rate_ack":
                    {

                        // Complete SR TCS with applied value (or -1.0 on failure).
                        bool ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
                        double applied = root.TryGetProperty("applied", out var aEl) && aEl.ValueKind == JsonValueKind.Number
                                         ? aEl.GetDouble() : 0.0;
                        _tcsSetSR?.TrySetResult(ok ? applied : -1.0);
                        break;
                    }

                    case "start_ack":

                        // Unblock start: true if streaming started, false otherwise.
                        _tcsStart?.TrySetResult(root.TryGetProperty("ok", out var ok4) && ok4.GetBoolean());
                        break;

                    case "sample":
                    {

                        // Parse and publish a new sample (UI callback on main thread).
                        _gotAnySample = true;

                        double? ts = root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number ? tsEl.GetDouble() : (double?)null;

                        static double? N(JsonElement parent, string name)
                        {
                            if (parent.ValueKind != JsonValueKind.Object) return null;
                            if (!parent.TryGetProperty(name, out var el)) return null;
                            return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : (double?)null;
                        }

                        JsonElement lna = default, wra = default, gyro = default, mag = default, ext = default;
                        root.TryGetProperty("lna", out lna);
                        root.TryGetProperty("wra", out wra);
                        root.TryGetProperty("gyro", out gyro);
                        root.TryGetProperty("mag", out mag);
                        root.TryGetProperty("ext", out ext);

                        var lnaX = N(lna, "x"); var lnaY = N(lna, "y"); var lnaZ = N(lna, "z");
                        var wraX = N(wra, "x"); var wraY = N(wra, "y"); var wraZ = N(wra, "z");
                        var gx   = N(gyro, "x"); var gy   = N(gyro, "y"); var gz   = N(gyro, "z");
                        var mx   = N(mag,  "x"); var my   = N(mag,  "y"); var mz   = N(mag,  "z");

                        double? temp  = root.TryGetProperty("temp",  out var tEl) && tEl.ValueKind == JsonValueKind.Number ? tEl.GetDouble() : (double?)null;
                        double? press = root.TryGetProperty("press", out var pEl) && pEl.ValueKind == JsonValueKind.Number ? pEl.GetDouble() : (double?)null;
                        double? vbatt = root.TryGetProperty("vbatt", out var vEl) && vEl.ValueKind == JsonValueKind.Number ? vEl.GetDouble() : (double?)null;

                        double? a6   = N(ext, "a6");
                        double? a7   = N(ext, "a7");
                        double? a15  = N(ext, "a15");

                        double? exg1 = null, exg2 = null;

                        if (root.TryGetProperty("exg1", out var exg1El) && exg1El.ValueKind == JsonValueKind.Number)
                            exg1 = exg1El.GetDouble();
                        if (root.TryGetProperty("exg2", out var exg2El) && exg2El.ValueKind == JsonValueKind.Number)
                            exg2 = exg2El.GetDouble();

                        if (!exg1.HasValue && root.TryGetProperty("Exg1", out var exgA) && exgA.ValueKind == JsonValueKind.Number)
                            exg1 = exgA.GetDouble();
                        if (!exg2.HasValue && root.TryGetProperty("Exg2", out var exgB) && exgB.ValueKind == JsonValueKind.Number)
                            exg2 = exgB.GetDouble();

                        if (!exg1.HasValue && root.TryGetProperty("Exg1", out var exg1Legacy) && exg1Legacy.ValueKind == JsonValueKind.Number)
                            exg1 = exg1Legacy.GetDouble();
                        if (!exg2.HasValue && root.TryGetProperty("Exg2", out var exg2Legacy) && exg2Legacy.ValueKind == JsonValueKind.Number)
                            exg2 = exg2Legacy.GetDouble();

                        LatestData = new ShimmerSDK_EXGData(
                            timeStamp: ts.HasValue && ts.Value >= 0
                                ? (uint)Math.Min(ts.Value, uint.MaxValue)
                                : 0u,

                            accelerometerX:      lnaX.HasValue ? new NumericPayload(lnaX.Value) : null,
                            accelerometerY:      lnaY.HasValue ? new NumericPayload(lnaY.Value) : null,
                            accelerometerZ:      lnaZ.HasValue ? new NumericPayload(lnaZ.Value) : null,
                            wideAccelerometerX:  wraX.HasValue ? new NumericPayload(wraX.Value) : null,
                            wideAccelerometerY:  wraY.HasValue ? new NumericPayload(wraY.Value) : null,
                            wideAccelerometerZ:  wraZ.HasValue ? new NumericPayload(wraZ.Value) : null,
                            gyroscopeX:          gx.HasValue ? new NumericPayload(gx.Value) : null,
                            gyroscopeY:          gy.HasValue ? new NumericPayload(gy.Value) : null,
                            gyroscopeZ:          gz.HasValue ? new NumericPayload(gz.Value) : null,
                            magnetometerX:       mx.HasValue ? new NumericPayload(mx.Value) : null,
                            magnetometerY:       my.HasValue ? new NumericPayload(my.Value) : null,
                            magnetometerZ:       mz.HasValue ? new NumericPayload(mz.Value) : null,
                            temperatureBMP180:   temp.HasValue  ? new NumericPayload(temp.Value)  : null,
                            pressureBMP180:      press.HasValue ? new NumericPayload(press.Value) : null,
                            batteryVoltage:      vbatt.HasValue ? new NumericPayload(vbatt.Value) : null,
                            extADC_A6:           a6.HasValue ? new NumericPayload(a6.Value) : null,
                            extADC_A7:           a7.HasValue ? new NumericPayload(a7.Value) : null,
                            extADC_A15:          a15.HasValue ? new NumericPayload(a15.Value) : null,

                            exg1: exg1.HasValue ? new NumericPayload(exg1.Value) : null,
                            exg2: exg2.HasValue ? new NumericPayload(exg2.Value) : null
                        );
                        LatestData.Exg1 = exg1.HasValue ? new NumericPayload(exg1.Value) : null;
                        LatestData.Exg2 = exg2.HasValue ? new NumericPayload(exg2.Value) : null;

                        try
                        {

                            // Notify listeners only if at least one channel is present
                            var data = LatestData;
                            if (data != null && HasAnyValue(data))
                            {

                                var snapshot = data;
                                RunOnMainThread(() => SampleReceived?.Invoke(this, snapshot));
                            }
                        }
                        catch { }
                        break;
                    }

                    case "error":
                        _tcsOpen?.TrySetResult(false);
                        _tcsConfig?.TrySetResult(false);
                        _tcsStart?.TrySetResult(false);
                        break;
                }
            }
            catch {}
        }


        /// <summary>
        /// Continuously reads WebSocket messages and routes text (JSON) frames to the control handler
        /// until cancellation or socket closure.
        /// </summary>
        /// <param name="ct">Cancellation token used to stop the receive loop.</param>
        /// <returns>A task that completes when the loop ends due to cancellation, error, or close.</returns>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];

            while (!ct.IsCancellationRequested && _ws != null)
            {
                WebSocketReceiveResult result;
                int offset = 0;

                try
                {
                    
                    // Read a complete WebSocket message-
                    do
                    {
                        var seg = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                        result = await _ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        offset += result.Count;     // Accumulate bytes read
                    }
                    while (!result.EndOfMessage && offset < buffer.Length);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;
                if (offset <= 0) continue;

                if (result.MessageType != WebSocketMessageType.Text) continue;

                // Text frames carry control JSON (acks, config, etc.).
                var txt = Encoding.UTF8.GetString(buffer, 0, offset);
                HandleControlMessage(txt);
            }
            _bridgeConnected = false;
        }


        /// <summary>
        /// Lightweight wrapper for a numeric reading used to uniformly carry sensor values
        /// (e.g., IMU channels) through the pipeline and UI bindings.
        /// </summary>
        public sealed class NumericPayload
        {
            public double Data { get; set; }
            public NumericPayload(double data) => Data = data;
        }
    }
}

#endif
