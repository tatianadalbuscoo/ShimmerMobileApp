#if IOS || MACCATALYST
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using UIKit;       // per invocare sul main thread (senza MAUI)
using Foundation;  // NSThread

namespace XR2Learn_ShimmerAPI.GSR
{
    // Bridge iOS/MacCatalyst per EXG: instrada IMU/env (EXG1/EXG2 ignorati per ora)
    public partial class XR2Learn_ShimmerEXG
    {
        // ======== API pubblica stile IMU ========

        // Config WS/Bridge
        public string BridgeHost { get; set; } = "192.168.43.1";
        public int    BridgePort { get; set; } = 8787;
        public string BridgePath { get; set; } = "/";
        public string BridgeTargetMac { get; set; } = "";

        // Ultimo pacchetto instradato
        public XR2Learn_ShimmerEXGData? LatestData { get; private set; }
// --- modalità EXG letta dal bridge (normalizzata) ---
private string _currentExgMode = "";

// 🔔 Evento che la UI può ascoltare per “stampare” la modalità
public event EventHandler<string>? ExgModeChanged;

// Titolo user-friendly per la label/console
public string CurrentExgModeTitle => _currentExgMode switch
{
    "ecg" => "ECG",
    "emg" => "EMG",
    "test" => "EXG Test",
    "resp" or "respiration" => "Respiration",
    _ => ""
};

// Comodo per nascondere la label quando non c’è una modalità valida
public bool HasExgMode => !string.IsNullOrEmpty(CurrentExgModeTitle);

// Setter che normalizza e notifica la UI sul main thread
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
        if (!string.IsNullOrEmpty(title)) D($"[UI] EXG Mode: {title}");

    }
}




        // ======== Helper main-thread ========
        private static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            if (NSThread.IsMain) action();
            else UIApplication.SharedApplication.BeginInvokeOnMainThread(action);
        }

        // ======== Stato WS ========
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _wsCts;
        private Task? _rxLoop;
        private volatile bool _bridgeConnected;
        private volatile bool _isStreaming;

        // subscribe & rx
        private volatile bool _subscribed;
        private volatile bool _gotAnySample;

        // ======== ACK TCS ========
        private TaskCompletionSource<bool>? _tcsHello, _tcsOpen, _tcsConfig, _tcsStart;
        private TaskCompletionSource<double>? _tcsSetSR;

        // ======== DEBUG ========
        private bool _debug = false;
        private int _dbgSamplePrintEvery = 1;
        private int _dbgSampleCounter = 0;
        private bool _loggedFirstMode = false;


        private void D(string msg)
        {
            if (_debug) Console.WriteLine($"[EXG iOS Bridge] {msg}");
        }

        private static string HexPreview(byte[] data, int max = 16)
        {
            int n = Math.Min(max, data?.Length ?? 0);
            var sb = new StringBuilder(n * 3);
            for (int i = 0; i < n; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i + 1 < n) sb.Append(' ');
            }
            if ((data?.Length ?? 0) > n) sb.Append(" …");
            return sb.ToString();
        }

        // ======== Wrapper .Data per compat UI ========
        public sealed class NumericPayload
        {
            public double Data { get; set; }
            public NumericPayload(double data) => Data = data;
        }

        private static bool IsNum(object? v) =>
            v is sbyte or byte or short or ushort or int or uint or long or ulong
             or float or double or decimal;

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

        private static string Fmt(object? v)
        {
            if (v is null) return "-";
            if (v is NumericPayload np) v = np.Data;
            else
            {
                var prop = v.GetType().GetProperty("Data");
                if (prop != null) v = prop.GetValue(v);
                if (v is null) return "-";
            }
            return v switch
            {
                double d  => d.ToString("F3", CultureInfo.InvariantCulture),
                float f   => ((double)f).ToString("F3", CultureInfo.InvariantCulture),
                decimal m => ((double)m).ToString("F3", CultureInfo.InvariantCulture),
                sbyte sb  => ((double)sb).ToString("F3", CultureInfo.InvariantCulture),
                byte b    => ((double)b).ToString("F3", CultureInfo.InvariantCulture),
                short s   => ((double)s).ToString("F3", CultureInfo.InvariantCulture),
                ushort us => ((double)us).ToString("F3", CultureInfo.InvariantCulture),
                int i     => ((double)i).ToString("F3", CultureInfo.InvariantCulture),
                uint ui   => ((double)ui).ToString("F3", CultureInfo.InvariantCulture),
                long l    => ((double)l).ToString("F3", CultureInfo.InvariantCulture),
                ulong ul  => ((double)ul).ToString("F3", CultureInfo.InvariantCulture),
                _         => v.ToString() ?? "-"
            };
        }

private static bool HasAnyValue(XR2Learn_ShimmerEXGData d) =>
    d != null &&
    (IsNumLike(d.LowNoiseAccelerometerX) || IsNumLike(d.LowNoiseAccelerometerY) || IsNumLike(d.LowNoiseAccelerometerZ) ||
     IsNumLike(d.WideRangeAccelerometerX) || IsNumLike(d.WideRangeAccelerometerY) || IsNumLike(d.WideRangeAccelerometerZ) ||
     IsNumLike(d.GyroscopeX) || IsNumLike(d.GyroscopeY) || IsNumLike(d.GyroscopeZ) ||
     IsNumLike(d.MagnetometerX) || IsNumLike(d.MagnetometerY) || IsNumLike(d.MagnetometerZ) ||
     IsNumLike(d.Temperature_BMP180) || IsNumLike(d.Pressure_BMP180) ||
     IsNumLike(d.BatteryVoltage) ||
     IsNumLike(d.ExtADC_A6) || IsNumLike(d.ExtADC_A7) || IsNumLike(d.ExtADC_A15) ||
     // EXG
     IsNumLike(d.Exg1Ch1) || IsNumLike(d.Exg2Ch1) || IsNumLike(d.ExgRespiration));


        // ======== Helper attese ACK ========
        private static async Task WaitOrThrow(TaskCompletionSource<bool> tcs, string what, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            if (done != tcs.Task || !tcs.Task.Result)
                throw new InvalidOperationException($"{what} timeout/failed");
        }
        private static async Task<bool> WaitSoft(TaskCompletionSource<bool> tcs, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            return done == tcs.Task && tcs.Task.Result;
        }

        // ======== Subscribe robusto ========
        private async Task EnsureSubscribedAsync()
        {
            if (_subscribed) return;
            for (int i = 0; i < 8 && !_subscribed; i++)
            {
                _tcsOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                D($"CMD → open {BridgeTargetMac} (retry {i + 1})");
                await SendJsonAsync(new { type = "open", mac = BridgeTargetMac }).ConfigureAwait(false);
                await Task.WhenAny(_tcsOpen.Task, Task.Delay(600)).ConfigureAwait(false);
                if (_subscribed) break;
            }
        }

        // ======== WS helpers ========
        private async Task EnsureWebSocketAsync()
        {
            if (_ws != null && _ws.State == WebSocketState.Open) return;

            await CloseWebSocketAsync().ConfigureAwait(false);

            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            _wsCts = new CancellationTokenSource();

            var uri = new UriBuilder("ws", BridgeHost, BridgePort, string.IsNullOrWhiteSpace(BridgePath) ? "/" : BridgePath).Uri;
            D($"Connecting to {uri} …");
            await _ws.ConnectAsync(uri, _wsCts.Token).ConfigureAwait(false);
            _bridgeConnected = true;
            D("WS connected");

            _rxLoop = Task.Run(() => ReceiveLoopAsync(_wsCts!.Token));
        }

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
            D("WS closed");
        }

        // ======== Connect / Start / Stop ========
        private async Task ConnectMacAsync()
        {
            if (string.IsNullOrWhiteSpace(BridgeHost)) throw new InvalidOperationException("BridgeHost non impostato");
            if (BridgePort <= 0) throw new InvalidOperationException("BridgePort non valido");
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac non impostato");

            await EnsureWebSocketAsync().ConfigureAwait(false);

            _tcsHello = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "hello" }).ConfigureAwait(false);
            await WaitOrThrow(_tcsHello, "hello_ack", 3000).ConfigureAwait(false);

            _subscribed = false;
            await EnsureSubscribedAsync().ConfigureAwait(false);
            // chiedi la config al bridge per avere subito exg_mode
            await SendJsonAsync(new { type = "get_config", mac = BridgeTargetMac }).ConfigureAwait(false);


            // Config IMU/env (EXG disatteso)
            _tcsConfig = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cfg = new
            {
                type = "set_config",
                // IMU/env: fissa a true per evitare dipendenze da proprietà mancanti
                EnableLowNoiseAccelerometer  = true,
                EnableWideRangeAccelerometer = true,
                EnableGyroscope              = true,
                EnableMagnetometer           = true,
                EnablePressureTemperature    = true,

                // opzionali ext ADC
                EnableExtA6  = true,
                EnableExtA7  = true,
                EnableExtA15 = true,

                // sampling rate dalla partial comune (già esistente nel tuo progetto)
                SamplingRate = this.SamplingRate
            };
            await SendJsonAsync(cfg).ConfigureAwait(false);
            _ = await WaitSoft(_tcsConfig, 6000).ConfigureAwait(false);
        }

        private async Task StartStreamingMacAsync()
        {
            if (!_bridgeConnected) await ConnectMacAsync().ConfigureAwait(false);
            await EnsureSubscribedAsync().ConfigureAwait(false);

            _tcsStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            D("CMD → start");
            await SendJsonAsync(new { type = "start", mac = BridgeTargetMac }).ConfigureAwait(false);
            await WaitOrThrow(_tcsStart, "start_ack", 12000).ConfigureAwait(false);

            _isStreaming = true;
        }

        private async Task StopStreamingMacAsync()
        {
            if (_ws == null) return;
            try { D("CMD → stop"); await SendJsonAsync(new { type = "stop" }).ConfigureAwait(false); } catch { }
            _isStreaming = false;
        }

        private async Task DisconnectMacAsync()
        {
            await StopStreamingMacAsync().ConfigureAwait(false);
            if (_ws != null) { try { D("CMD → close"); await SendJsonAsync(new { type = "close" }).ConfigureAwait(false); } catch { } }
            await CloseWebSocketAsync().ConfigureAwait(false);
        }

        private bool IsConnectedMac() => _bridgeConnected && _ws != null && _ws.State == WebSocketState.Open;

        private async Task<double> SetFirmwareSamplingRateNearestImpl(double desiredHz)
        {
            if (desiredHz <= 0) throw new ArgumentOutOfRangeException(nameof(desiredHz));
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac non impostato");

            await EnsureWebSocketAsync().ConfigureAwait(false);
            await EnsureSubscribedAsync().ConfigureAwait(false);

            _tcsSetSR = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "set_sampling_rate", mac = BridgeTargetMac, sr = desiredHz }).ConfigureAwait(false);

            var done = await Task.WhenAny(_tcsSetSR.Task, Task.Delay(6000)).ConfigureAwait(false);
            if (done != _tcsSetSR.Task) throw new TimeoutException("set_sampling_rate timeout");

            double applied = _tcsSetSR.Task.Result;
            if (applied <= 0) throw new InvalidOperationException("set_sampling_rate failed");

            SamplingRate = applied; // proprietà esistente nella partial comune
            return applied;
        }

        // ======== Send JSON ========
        private async Task SendJsonAsync(object payload)
        {
            if (_ws == null) throw new InvalidOperationException("WS non connesso");
            var json  = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            D("WS → " + json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _wsCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }

        // ======== Gestione messaggi ========
        private void HandleControlMessage(string txt)
        {
            try
            {
                using var doc = JsonDocument.Parse(txt);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var t)) return;
                var type = t.GetString();

                switch (type)
                {
                    case "hello_ack":
                        _tcsHello?.TrySetResult(root.TryGetProperty("ok", out var ok1) && ok1.GetBoolean());
                        break;

                    case "open_ack":
                    {
                        bool ok = root.TryGetProperty("ok", out var ok2) && ok2.GetBoolean();
                        _subscribed = ok;
                        _tcsOpen?.TrySetResult(ok);
                        break;
                    }

case "config":
case "config_changed":
{
    // es: { type:"config"|"config_changed", cfg:{ "exg_mode":"ecg" } }
    if (type == "config" && root.TryGetProperty("ok", out var okEl) && !okEl.GetBoolean())
        break; // config non ok: esci

    if (root.TryGetProperty("cfg", out var cfg) &&
        cfg.ValueKind == JsonValueKind.Object &&
        cfg.TryGetProperty("exg_mode", out var m) &&
        m.ValueKind == JsonValueKind.String)
    {
        var raw = m.GetString();
        var normalized = (raw ?? "").Trim().ToLowerInvariant();
        CurrentExgMode = normalized;

        D($"[EXG] exg_mode ({type}) raw='{raw}' normalized='{normalized}'");

        if (normalized != "ecg" && normalized != "emg" &&
            normalized != "test" && normalized != "resp" &&
            normalized != "respiration")
        {
            D("[EXG] ⚠️ exg_mode non riconosciuto. Attesi: ecg | emg | test | resp(respiration)");
        }
    }
    break;
}





                    case "config_ack":
                    {
                        bool ok3 = root.TryGetProperty("ok", out var okCfg) && okCfg.GetBoolean();
                        _tcsConfig?.TrySetResult(ok3);
                        break;
                    }

                    case "set_sampling_rate_ack":
                    {
                        bool ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
                        double applied = root.TryGetProperty("applied", out var aEl) && aEl.ValueKind == JsonValueKind.Number
                                         ? aEl.GetDouble() : 0.0;
                        _tcsSetSR?.TrySetResult(ok ? applied : -1.0);
                        break;
                    }



                    case "start_ack":
                        _tcsStart?.TrySetResult(root.TryGetProperty("ok", out var ok4) && ok4.GetBoolean());
                        break;

                    case "sample":
                    {
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

                                                // EXG (due canali generici dal bridge)
                        double? exg1 = null, exg2 = null;

                        // nuovi nomi
                        if (root.TryGetProperty("exg1", out var exg1El) && exg1El.ValueKind == JsonValueKind.Number)
                            exg1 = exg1El.GetDouble();
                        if (root.TryGetProperty("exg2", out var exg2El) && exg2El.ValueKind == JsonValueKind.Number)
                            exg2 = exg2El.GetDouble();

                            // DEBUG: stampa cosa arriva per EXG e la modalità corrente
if (_debug)
{
    var s1 = exg1.HasValue ? exg1.Value.ToString("F3", CultureInfo.InvariantCulture) : "-";
    var s2 = exg2.HasValue ? exg2.Value.ToString("F3", CultureInfo.InvariantCulture) : "-";
    D($"[EXG] sample mode='{CurrentExgMode}' exg1={s1} exg2={s2}");
}


                        // fallback alias legacy
                        if (!exg1.HasValue && root.TryGetProperty("ExgCh1", out var exgA) && exgA.ValueKind == JsonValueKind.Number)
                            exg1 = exgA.GetDouble();
                        if (!exg2.HasValue && root.TryGetProperty("ExgCh2", out var exgB) && exgB.ValueKind == JsonValueKind.Number)
                            exg2 = exgB.GetDouble();

                        LatestData = new XR2Learn_ShimmerEXGData(
                            // DOPO (no saturazione a 2147483647)
                            timeStamp: ts.HasValue && ts.Value >= 0
                                ? (uint)Math.Min(ts.Value, uint.MaxValue)
                                : 0u,


                            // IMU/env
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

                            // EXG: abbiamo solo due canali (EXG1, EXG2); mappiamo su ch1 di ciascuna coppia
                            exg1Ch1:      exg1.HasValue ? new NumericPayload(exg1.Value) : null,
                            exg1Ch2:      null,
                            exg2Ch1:      exg2.HasValue ? new NumericPayload(exg2.Value) : null,
                            exg2Ch2:      null,

                            // Respiration: duplichiamo uno dei due canali se la modalità è "resp" o "respiration"
                            exgRespiration: (CurrentExgMode == "resp" || CurrentExgMode == "respiration")
                                ? new NumericPayload((exg1 ?? exg2) ?? 0.0)
                                : null
                        );


                        try
                        {
                            var data = LatestData;
                            if (data != null && HasAnyValue(data))
                            {
                                if (_debug && (_dbgSampleCounter++ % _dbgSamplePrintEvery == 0))
                                {
                                    D($"Parsed ts={data.TimeStamp} " +
                                      $"LNA=({Fmt(data.LowNoiseAccelerometerX)}, {Fmt(data.LowNoiseAccelerometerY)}, {Fmt(data.LowNoiseAccelerometerZ)})");
                                }
var snapshot = data;   // o LatestData
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
            catch { /* ignora json malformati */ }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];

            long t0 = Environment.TickCount64;
            int bytesThisSecond = 0;

            while (!ct.IsCancellationRequested && _ws != null)
            {
                WebSocketReceiveResult result;
                int offset = 0;

                try
                {
                    do
                    {
                        var seg = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
                        result = await _ws.ReceiveAsync(seg, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        offset += result.Count;
                    }
                    while (!result.EndOfMessage && offset < buffer.Length);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { D("RX error: " + ex.Message); break; }

                if (result.MessageType == WebSocketMessageType.Close) { D("WS ← Close"); break; }
                if (offset <= 0) continue;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var txt = Encoding.UTF8.GetString(buffer, 0, offset);
                    // if (_debug) D("WS ← sample"); // niente JSON completo
                    HandleControlMessage(txt);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    bytesThisSecond += offset;
                    long now = Environment.TickCount64;
                    if (now - t0 >= 1000) { D($"WS RX ≈ {bytesThisSecond} B/s"); bytesThisSecond = 0; t0 = now; }

                    var chunk = new byte[offset];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, offset);

                    if (chunk.Length > 0 && (chunk[0] == (byte)'{' || chunk[0] == (byte)'['))
                    {
                        var txt = Encoding.UTF8.GetString(chunk, 0, chunk.Length);
                        D("WS (BIN-as-TXT) ← " + txt);
                        HandleControlMessage(txt);
                        continue;
                    }

                    D($"WS ← BIN {offset} B | {HexPreview(chunk, 16)}");
                    // nessun parser binario per EXG nel bridge attuale
                }
            }

            _bridgeConnected = false;
            D("RX loop end");
        }
    }
}
#endif
