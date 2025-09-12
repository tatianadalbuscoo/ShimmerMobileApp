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
    // Bridge iOS/MacCatalyst per EXG: WebSocket verso il tuo WsBridge, con parsing JSON
    public partial class XR2Learn_ShimmerEXG
    {
        // ===== Helper: invoca sul main thread (iOS/macOS Catalyst) =====
        private static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            if (NSThread.IsMain) action();
            else UIApplication.SharedApplication.BeginInvokeOnMainThread(action);
        }

        // ===== Config bridge =====
        public string BridgeHost { get; set; } = "192.168.43.1";
        public int    BridgePort { get; set; } = 8787;
        public string BridgePath { get; set; } = "/";
        public string BridgeTargetMac { get; set; } = "";

        // ===== Stato =====
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _wsCts;
        private Task? _rxLoop;
        private volatile bool _bridgeConnected;
        private volatile bool _isStreaming;

        // subscribe & rx
        private volatile bool _subscribed;
        private volatile bool _gotAnySample;

        // ===== ACK TCS =====
        private TaskCompletionSource<bool>? _tcsHello, _tcsOpen, _tcsConfig, _tcsStart;
        private TaskCompletionSource<double>? _tcsSetSR;

        // ===== DEBUG =====
        private bool _debug = true;
        private int _dbgSamplePrintEvery = 1;
        private int _dbgSampleCounter = 0;

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

        // ===== Wrapper con .Data per compat UI =====
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

        // === Ultimo pacchetto EXG + evento ===
        public XR2Learn_ShimmerEXGData? LatestData { get; private set; }


        private static bool HasAnyExgValue(XR2Learn_ShimmerEXGData d) =>
            d != null && (
                IsNumLike(d.Exg1Ch1) || IsNumLike(d.Exg1Ch2) ||
                IsNumLike(d.Exg2Ch1) || IsNumLike(d.Exg2Ch2) ||
                IsNumLike(d.ExgRespiration) || IsNumLike(d.BatteryVoltage)
            );

        // ==== Subscribe robusto: ritenta più volte ====
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

        // ---- API usate nel ramo streaming ----
        private async Task ConnectMacAsync()
        {
            if (string.IsNullOrWhiteSpace(BridgeHost)) throw new InvalidOperationException("BridgeHost non impostato");
            if (BridgePort <= 0) throw new InvalidOperationException("BridgePort non valido");
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac non impostato");

            await EnsureWebSocketAsync().ConfigureAwait(false);

            // hello (hard)
            _tcsHello = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "hello" }).ConfigureAwait(false);
            await WaitOrThrow(_tcsHello, "hello_ack", 3000).ConfigureAwait(false);

            // subscribe robusto
            _subscribed = false;
            await EnsureSubscribedAsync().ConfigureAwait(false);

            // set_config (soft): ok anche se server_managed
            _tcsConfig = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cfg = new
            {
                type = "set_config",
                // Se/quando il server accetterà config lato client
                EnableExg1  = true,
                EnableExg2  = true,
                ExgUse16Bit = false,
                SamplingRate = this.SamplingRate
            };
            await SendJsonAsync(cfg).ConfigureAwait(false);
            _ = await WaitSoft(_tcsConfig, 6000).ConfigureAwait(false);
        }

        private async Task StartStreamingMacAsync()
        {
            if (!_bridgeConnected) await ConnectMacAsync().ConfigureAwait(false);

            // assicurati di essere sottoscritto
            await EnsureSubscribedAsync().ConfigureAwait(false);

            // invia start con il MAC
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

        public async Task<double> SetFirmwareSamplingRateNearestAsync(double desiredHz)
        {
            if (desiredHz <= 0) throw new ArgumentOutOfRangeException(nameof(desiredHz));
            if (string.IsNullOrWhiteSpace(BridgeTargetMac)) throw new InvalidOperationException("BridgeTargetMac non impostato");

            // assicurati che il WS sia connesso e la sessione sia "open/subscribed"
            await EnsureWebSocketAsync().ConfigureAwait(false);
            await EnsureSubscribedAsync().ConfigureAwait(false);

            _tcsSetSR = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);

            // invia il comando al bridge
            await SendJsonAsync(new { type = "set_sampling_rate", mac = BridgeTargetMac, sr = desiredHz }).ConfigureAwait(false);

            // attende l'ACK (timeout 6s)
            var done = await Task.WhenAny(_tcsSetSR.Task, Task.Delay(6000)).ConfigureAwait(false);
            if (done != _tcsSetSR.Task) throw new TimeoutException("set_sampling_rate timeout");

            double applied = _tcsSetSR.Task.Result;
            if (applied <= 0) throw new InvalidOperationException("set_sampling_rate failed");

            // sincronizza il modello locale e restituisci lo "snap" realmente applicato
            SamplingRate = applied;
            return applied;
        }

        private async Task DisconnectMacAsync()
        {
            await StopStreamingMacAsync().ConfigureAwait(false);
            if (_ws != null) { try { D("CMD → close"); await SendJsonAsync(new { type = "close" }).ConfigureAwait(false); } catch { } }
            await CloseWebSocketAsync().ConfigureAwait(false);
        }

        private bool IsConnectedMac() => _bridgeConnected && _ws != null && _ws.State == WebSocketState.Open;

        // ====== WS helpers ======
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

        private async Task SendJsonAsync(object payload)
        {
            if (_ws == null) throw new InvalidOperationException("WS non connesso");
            var json  = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            D("WS → " + json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _wsCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }

        // === Helper attese ACK ===
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

        // ========= Gestione messaggi =========
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

                    case "config_ack":
                    {
                        bool ok3 = root.TryGetProperty("ok", out var okCfg) && okCfg.GetBoolean();
                        if (!ok3 && root.TryGetProperty("error", out var errCfg))
                            D("config_ack error: " + errCfg.GetString());
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

                        JsonElement exg1 = default, exg2 = default, ext = default;
                        root.TryGetProperty("exg1", out exg1);
                        root.TryGetProperty("exg2", out exg2);
                        root.TryGetProperty("ext", out ext);

                        double? e11 = N(exg1, "ch1");
                        double? e12 = N(exg1, "ch2");
                        double? e21 = N(exg2, "ch1");
                        double? e22 = N(exg2, "ch2");

                        if (!e11.HasValue && root.TryGetProperty("ExgCh1", out var flat1) && flat1.ValueKind==JsonValueKind.Number)
                            e11 = flat1.GetDouble();
                        if (!e12.HasValue && root.TryGetProperty("ExgCh2", out var flat2) && flat2.ValueKind==JsonValueKind.Number)
                            e12 = flat2.GetDouble();

                        double? resp  = root.TryGetProperty("resp", out var rEl) && rEl.ValueKind==JsonValueKind.Number ? rEl.GetDouble() : (double?)null;
                        double? vbatt = root.TryGetProperty("vbatt", out var vEl) && vEl.ValueKind == JsonValueKind.Number ? vEl.GetDouble() : (double?)null;

                        double? a6 = N(ext, "a6");
                        double? a7 = N(ext, "a7");
                        double? a15 = N(ext, "a15");

                        bool hasAnyExg = e11.HasValue || e12.HasValue || e21.HasValue || e22.HasValue || resp.HasValue;
                        if (hasAnyExg)
                        {
                            LatestData = new XR2Learn_ShimmerEXGData(
                                timeStamp: (uint)Math.Max(0, (int)(ts ?? 0)),
                                // opzionali IMU-in-EXG (se il bridge li inserisce comunque)
                                accelerometerX: null, accelerometerY: null, accelerometerZ: null,
                                wideAccelerometerX: null, wideAccelerometerY: null, wideAccelerometerZ: null,
                                gyroscopeX: null, gyroscopeY: null, gyroscopeZ: null,
                                magnetometerX: null, magnetometerY: null, magnetometerZ: null,
                                temperatureBMP180: null, pressureBMP180: null,
                                batteryVoltage: vbatt,
                                extADC_A6: a6, extADC_A7: a7, extADC_A15: a15,
                                // EXG
                                exg1Ch1: e11, exg1Ch2: e12, exg2Ch1: e21, exg2Ch2: e22,
                                exgRespiration: resp
                            );

                            try
                            {
                                if (HasAnyExgValue(LatestData))
                                {
                                    RunOnMainThread(() =>
                                        SampleReceived?.Invoke(this, LatestData));
                                }
                            }
                            catch { }
                        }

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
                    D("WS ← " + txt);
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
                    // Nessun parser binario previsto per EXG nel bridge attuale
                }
            }

            _bridgeConnected = false;
            D("RX loop end");
        }
    }
}
#endif
