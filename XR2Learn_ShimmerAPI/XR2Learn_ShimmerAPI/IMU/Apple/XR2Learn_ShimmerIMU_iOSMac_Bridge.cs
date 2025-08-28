#if IOS || MACCATALYST
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace XR2Learn_ShimmerAPI.IMU
{
    public partial class XR2Learn_ShimmerIMU
    {
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

        // ===== ACK TCS =====
        private TaskCompletionSource<bool>? _tcsHello, _tcsOpen, _tcsConfig, _tcsStart;

        // ===== DEBUG =====
        private bool _debug = true;            // abilita/disabilita stampe
        private int _dbgSamplePrintEvery = 1;  // logga OGNI frame (puoi rimettere 25)
        private int _dbgSampleCounter = 0;

        private void D(string msg)
        {
            if (_debug) Console.WriteLine($"[iOS Bridge] {msg}");
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

        // ===== Wrapper opzionale con .Data per compat con pipeline UI =====
        public sealed class NumericPayload
        {
            public double Data { get; set; }
            public NumericPayload(double data) => Data = data;
        }

        // === Helper su object ===
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

            // estrai eventuale .Data
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

        private static bool HasAnyValue(XR2Learn_ShimmerIMUData d) =>
            d != null &&
            (IsNumLike(d.LowNoiseAccelerometerX) || IsNumLike(d.LowNoiseAccelerometerY) || IsNumLike(d.LowNoiseAccelerometerZ) ||
             IsNumLike(d.WideRangeAccelerometerX) || IsNumLike(d.WideRangeAccelerometerY) || IsNumLike(d.WideRangeAccelerometerZ) ||
             IsNumLike(d.GyroscopeX) || IsNumLike(d.GyroscopeY) || IsNumLike(d.GyroscopeZ) ||
             IsNumLike(d.MagnetometerX) || IsNumLike(d.MagnetometerY) || IsNumLike(d.MagnetometerZ) ||
             IsNumLike(d.Temperature_BMP180) || IsNumLike(d.Pressure_BMP180) ||
             IsNumLike(d.BatteryVoltage) ||
             IsNumLike(d.ExtADC_A6) || IsNumLike(d.ExtADC_A7) || IsNumLike(d.ExtADC_A15));

        // === Helper attese ACK ===

        // Hard: lancia se fallisce
        private static async Task WaitOrThrow(TaskCompletionSource<bool> tcs, string what, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            if (done != tcs.Task || !tcs.Task.Result)
                throw new InvalidOperationException($"{what} timeout/failed");
        }

        // Soft: NON lancia; ritorna true/false
        private static async Task<bool> WaitSoft(TaskCompletionSource<bool> tcs, int ms)
        {
            var done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            return done == tcs.Task && tcs.Task.Result;
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

            // open (hard)
            _tcsOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendJsonAsync(new { type = "open", mac = BridgeTargetMac }).ConfigureAwait(false);
            await WaitOrThrow(_tcsOpen, "open_ack", 8000).ConfigureAwait(false);

            // piccola pausa per permettere l’apertura SPP completa
            await Task.Delay(200).ConfigureAwait(false);

            // set_config (soft)
            _tcsConfig = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cfg = new
            {
                type = "set_config",
                EnableLowNoiseAccelerometer = this.EnableLowNoiseAccelerometer,
                EnableWideRangeAccelerometer = this.EnableWideRangeAccelerometer,
                EnableGyroscope = this.EnableGyroscope,
                EnableMagnetometer = this.EnableMagnetometer,
                EnablePressureTemperature = this.EnablePressureTemperature,
                EnableBattery = this.EnableBattery,
                EnableExtA6 = this.EnableExtA6,
                EnableExtA7 = this.EnableExtA7,
                EnableExtA15 = this.EnableExtA15,
                SamplingRate = this.SamplingRate
            };
            await SendJsonAsync(cfg).ConfigureAwait(false);

            var cfgOk = await WaitSoft(_tcsConfig, 6000).ConfigureAwait(false);
            if (!cfgOk)
                D("config_ack non ricevuto / ok=false → continuo con i default del bridge");
        }

        private async Task StartStreamingMacAsync()
        {
            if (!_bridgeConnected) await ConnectMacAsync().ConfigureAwait(false);

            _tcsStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            D("CMD → start");
            await SendJsonAsync(new { type = "start" }).ConfigureAwait(false);
            await WaitOrThrow(_tcsStart, "start_ack", 4000).ConfigureAwait(false);

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

        // Centralizza la gestione degli ACK + SAMPLE
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
                        _tcsOpen?.TrySetResult(root.TryGetProperty("ok", out var ok2) && ok2.GetBoolean());
                        break;

                    case "config_ack":
                    {
                        bool ok3 = root.TryGetProperty("ok", out var okCfg) && okCfg.GetBoolean();
                        if (!ok3 && root.TryGetProperty("error", out var errCfg))
                            D("config_ack error: " + errCfg.GetString());
                        _tcsConfig?.TrySetResult(ok3);
                        break;
                    }

                    case "start_ack":
                        _tcsStart?.TrySetResult(root.TryGetProperty("ok", out var ok4) && ok4.GetBoolean());
                        break;

                    case "sample":
                    {
                        // stampa “Gyroscope X: …” ecc. e aggiorna LatestData
                        double? ts = root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number ? tsEl.GetDouble() : (double?)null;

                        // helper lettura triple {x,y,z}
                        static double? N(JsonElement parent, string name)
                        {
                            if (!parent.TryGetProperty(name, out var el)) return null;
                            if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
                            return null;
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

                        double? a6 = N(ext, "a6");
                        double? a7 = N(ext, "a7");
                        double? a15 = N(ext, "a15");

                        // LOG leggibile
                        D($"SAMPLE ts={ts?.ToString("F0", CultureInfo.InvariantCulture) ?? "-"} " +
                          $"LNA=({Fmt(new NumericPayload(lnaX ?? 0))}, {Fmt(new NumericPayload(lnaY ?? 0))}, {Fmt(new NumericPayload(lnaZ ?? 0))}) " +
                          $"WRA=({Fmt(new NumericPayload(wraX ?? 0))}, {Fmt(new NumericPayload(wraY ?? 0))}, {Fmt(new NumericPayload(wraZ ?? 0))}) " +
                          $"GYRO=({Fmt(new NumericPayload(gx ?? 0))}, {Fmt(new NumericPayload(gy ?? 0))}, {Fmt(new NumericPayload(gz ?? 0))}) " +
                          $"MAG=({Fmt(new NumericPayload(mx ?? 0))}, {Fmt(new NumericPayload(my ?? 0))}, {Fmt(new NumericPayload(mz ?? 0))}) " +
                          $"TEMP={(temp?.ToString("F3", CultureInfo.InvariantCulture) ?? "-")} " +
                          $"PRESS={(press?.ToString("F3", CultureInfo.InvariantCulture) ?? "-")} " +
                          $"VBATT={(vbatt?.ToString("F3", CultureInfo.InvariantCulture) ?? "-")} " +
                          $"EXT=({Fmt(new NumericPayload(a6 ?? 0))}, {Fmt(new NumericPayload(a7 ?? 0))}, {Fmt(new NumericPayload(a15 ?? 0))})");

                        // Aggiorna LatestData in modo coerente con la pipeline UI
                        LatestData = new XR2Learn_ShimmerIMUData(
                            timeStamp: (uint)Math.Max(0, (int)(ts ?? 0)),
                            accelerometerX: lnaX.HasValue ? new NumericPayload(lnaX.Value) : null,
                            accelerometerY: lnaY.HasValue ? new NumericPayload(lnaY.Value) : null,
                            accelerometerZ: lnaZ.HasValue ? new NumericPayload(lnaZ.Value) : null,
                            wideAccelerometerX: wraX.HasValue ? new NumericPayload(wraX.Value) : null,
                            wideAccelerometerY: wraY.HasValue ? new NumericPayload(wraY.Value) : null,
                            wideAccelerometerZ: wraZ.HasValue ? new NumericPayload(wraZ.Value) : null,
                            gyroscopeX: gx.HasValue ? new NumericPayload(gx.Value) : null,
                            gyroscopeY: gy.HasValue ? new NumericPayload(gy.Value) : null,
                            gyroscopeZ: gz.HasValue ? new NumericPayload(gz.Value) : null,
                            magnetometerX: mx.HasValue ? new NumericPayload(mx.Value) : null,
                            magnetometerY: my.HasValue ? new NumericPayload(my.Value) : null,
                            magnetometerZ: mz.HasValue ? new NumericPayload(mz.Value) : null,
                            temperatureBMP180: temp.HasValue ? new NumericPayload(temp.Value) : null,
                            pressureBMP180:    press.HasValue ? new NumericPayload(press.Value) : null,
                            batteryVoltage:    vbatt.HasValue ? new NumericPayload(vbatt.Value) : null,
                            extADC_A6: a6.HasValue ? new NumericPayload(a6.Value) : null,
                            extADC_A7: a7.HasValue ? new NumericPayload(a7.Value) : null,
                            extADC_A15: a15.HasValue ? new NumericPayload(a15.Value) : null
                        );

                        // invoca callback UI se serve
                        try
                        {
                            if (HasAnyValue(LatestData))
                            {
                                if (_debug && (_dbgSampleCounter++ % _dbgSamplePrintEvery == 0))
                                {
                                    // già stampato sopra
                                }
                                SampleReceived?.Invoke(this, LatestData);
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

                    // alcuni server inviano ACK come "binario" ma è JSON
                    if (chunk.Length > 0 && (chunk[0] == (byte)'{' || chunk[0] == (byte)'['))
                    {
                        var txt = Encoding.UTF8.GetString(chunk, 0, chunk.Length);
                        D("WS (BIN-as-TXT) ← " + txt);
                        HandleControlMessage(txt);
                        continue;
                    }

                    D($"WS ← BIN {offset} B | {HexPreview(chunk, 16)}");

                    try
                    {
                        OnPacketReceived(chunk);

                        var data = LatestData;
                        if (HasAnyValue(data))
                        {
                            if (_debug && (_dbgSampleCounter++ % _dbgSamplePrintEvery == 0))
                            {
                                D($"Parsed ts={data.TimeStamp} " +
                                  $"LNA=({Fmt(data.LowNoiseAccelerometerX)}, {Fmt(data.LowNoiseAccelerometerY)}, {Fmt(data.LowNoiseAccelerometerZ)})");
                            }

                            SampleReceived?.Invoke(this, data);
                        }
                    }
                    catch (Exception ex)
                    {
                        D("Parse error: " + ex.Message);
                    }
                }
            }

            _bridgeConnected = false;
            D("RX loop end");
        }

        // Parser provvisorio RAW 10B — lo lasciamo (se mai servisse binario)
        private void OnPacketReceived(byte[] payload)
        {
            try
            {
                if (payload == null || payload.Length < 10)
                    return;

                uint ts = BitConverter.ToUInt32(payload, 0);
                short ax = BitConverter.ToInt16(payload, 4);
                short ay = BitConverter.ToInt16(payload, 6);
                short az = BitConverter.ToInt16(payload, 8);

                const double scale = 16384.0;
                var ax_g = new NumericPayload(ax / scale);
                var ay_g = new NumericPayload(ay / scale);
                var az_g = new NumericPayload(az / scale);

                LatestData = new XR2Learn_ShimmerIMUData(
                    timeStamp: ts,
                    accelerometerX: ax_g, accelerometerY: ay_g, accelerometerZ: az_g,
                    wideAccelerometerX: null, wideAccelerometerY: null, wideAccelerometerZ: null,
                    gyroscopeX: null, gyroscopeY: null, gyroscopeZ: null,
                    magnetometerX: null, magnetometerY: null, magnetometerZ: null,
                    temperatureBMP180: null, pressureBMP180: null,
                    batteryVoltage: null,
                    extADC_A6: null, extADC_A7: null, extADC_A15: null
                );
            }
            catch { }
        }
    }
}
#endif
