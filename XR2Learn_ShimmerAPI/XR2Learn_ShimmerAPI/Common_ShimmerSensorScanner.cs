#if WINDOWS || ANDROID
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;

#if ANDROID
using Log = global::Android.Util.Log;
using ShimmerDroid = XR2Learn_ShimmerAPI.Android; // alias del tuo namespace Android
using System.Collections;
using System.Collections.Generic;
#endif

namespace XR2Learn_ShimmerAPI
{
    public static partial class ShimmerSensorScanner
    {
        // Unica definizione valida su entrambe le piattaforme
        public enum BoardKind { Unknown, EXG, IMU }

        // =========================
        // WINDOWS (NON TOCCARE)
        // =========================
#if WINDOWS
        /// Tenta di leggere la daughter-card (reflection su API Windows).
        public static bool TryDetectBoardKind(
            ShimmerLogAndStreamSystemSerialPortV2 shim,
            out BoardKind kind,
            out string rawId)
        {
            kind  = BoardKind.Unknown;
            rawId = "";

            try
            {
                object? val = null;
                var t = shim.GetType();

                foreach (var mName in new[] { "GetExpansionBoardID", "ReadExpansionBoardID", "GetExpansionBoard", "GetDaughterCardID" })
                {
                    var m = t.GetMethod(mName, BindingFlags.Public | BindingFlags.Instance);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        val = m.Invoke(shim, null);
                        if (val != null) break;
                    }
                }

                if (val == null)
                {
                    foreach (var pName in new[] { "ExpansionBoard", "ExpansionBoardID", "DaughterCardID" })
                    {
                        var p = t.GetProperty(pName, BindingFlags.Public | BindingFlags.Instance);
                        if (p != null)
                        {
                            val = p.GetValue(shim);
                            if (val != null) break;
                        }
                    }
                }

                if (val == null) return false;

                rawId = val.ToString() ?? "";
                var upper = rawId.ToUpperInvariant();

                if (upper.Contains("EXG"))
                {
                    kind = BoardKind.EXG;
                    return true;
                }

                kind = BoardKind.IMU;

                if (int.TryParse(rawId, out var numId))
                {
                    var sb = typeof(ShimmerBluetooth);
                    var enumTypes = sb.GetNestedTypes(BindingFlags.Public)
                                      .Where(nt => nt.IsEnum && nt.Name.IndexOf("Expansion", StringComparison.OrdinalIgnoreCase) >= 0)
                                      .ToArray();

                    foreach (var et in enumTypes)
                    {
                        foreach (var f in et.GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            var value = Convert.ToInt32(f.GetValue(null));
                            if (value == numId)
                            {
                                var name = f.Name.ToUpperInvariant();
                                rawId = $"{et.Name}.{f.Name}";
                                kind  = name.Contains("EXG") ? BoardKind.EXG : BoardKind.IMU;
                                return true;
                            }
                        }
                    }
                    return true; // numerico letto ma non mappato: resta IMU
                }

                return true; // testo non-EXG → IMU
            }
            catch
            {
                kind  = BoardKind.Unknown;
                rawId = "";
                return false;
            }
        }

        public static async Task<(bool ok, BoardKind kind, string rawId)> GetExpansionBoardKindWindowsAsync(
            string deviceName, string comPort)
        {
            ShimmerLogAndStreamSystemSerialPortV2? shim = null;
            try
            {
                shim = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                shim.Connect();
                await Task.Delay(150);

                var ok = TryDetectBoardKind(shim, out var kind, out var raw);
                return (ok, kind, raw);
            }
            catch
            {
                return (false, BoardKind.Unknown, "");
            }
            finally
            {
                try { shim?.Disconnect(); } catch { }
            }
        }

        public static async Task<string[]> GetSignalNamesWindowsAsync(string deviceName, string comPort)
        {
            ShimmerLogAndStreamSystemSerialPortV2? shim = null;
            try
            {
                shim = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                shim.Connect();
                await Task.Delay(150);

                try { shim.Inquiry(); } catch { }
                System.Threading.Thread.Sleep(150);

                var t  = shim.GetType();
                var mi = t.GetMethod("GetSignalNameArray") ?? t.GetMethod("GetSignalNameList");
                if (mi == null) return Array.Empty<string>();

                var raw = mi.Invoke(shim, null);
                if (raw is string[] arr) return arr.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                if (raw is System.Collections.IEnumerable en)
                    return en.Cast<object?>()
                             .Select(o => o?.ToString() ?? "")
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .ToArray();

                return Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
            finally
            {
                try { shim?.Disconnect(); } catch { }
            }
        }
#endif // WINDOWS

        // =========================
        // ANDROID (SOLO QUESTA PARTE CAMBIA)
        // =========================
#if ANDROID

        //
        // Rilevazione Expansion Card su Android:
        // - trova riflessivamente l’oggetto che espone {ReadInternalExpPower, ReadExpansionBoard, GetExpansionBoard}
        // - invia ReadInternalExpPower + ReadExpansionBoard
        // - fa polling su GetExpansionBoard() finché arriva la risposta
        // - mappa la stringa a BoardKind (EXG vs IMU)
        //
        public static bool TryDetectBoardKind(
            ShimmerDroid.ShimmerLogAndStreamAndroidBluetoothV2 shim,
            out BoardKind kind,
            out string rawId)
        {
            kind  = BoardKind.Unknown;
            rawId = "";

            try
            {
                if (shim == null)
                {
                    Log.Debug("Shimmer", "[Detect/Android] shim NULL");
                    return false;
                }

                Log.Debug("Shimmer", $"[Detect/Android] shimType={shim.GetType().FullName}, connected={shim.IsConnected()}");

                if (!shim.IsConnected())
                {
                    Log.Debug("Shimmer", "[Detect/Android] not connected");
                    return false;
                }

                // 1) Cerco il "target" che espone GetExpansionBoard/ReadExpansionBoard (può essere il shim o un campo interno).
                var target = FindExpansionTarget(shim, maxDepth: 3);
                if (target == null)
                {
                    Log.Debug("Shimmer", "[Detect/Android] expansion target not found");
                    return false;
                }

                // 2) Forza i comandi di lettura (se esistono)
                InvokeNoArgIfExists(target, "ReadInternalExpPower");
                InvokeNoArgIfExists(target, "ReadExpansionBoard");
                SafeDelay(120);

                // 3) Attendi la risposta su GetExpansionBoard()
                string boardStr;
                var ok = TryWaitExpansionString(target, out boardStr, timeoutMs: 2600);

                // 4) Ritenta una volta se vuoto
                if (!ok)
                {
                    Log.Debug("Shimmer", "[Detect/Android] retry ReadExpansionBoard");
                    InvokeNoArgIfExists(target, "ReadExpansionBoard");
                    ok = TryWaitExpansionString(target, out boardStr, timeoutMs: 1400);
                }

                Log.Debug("Shimmer", $"[Detect/Android] GetExpansionBoard() -> '{boardStr ?? "<null>"}'");

                if (!ok || string.IsNullOrWhiteSpace(boardStr))
                {
                    // Non arrivato nulla: lasciamo Unknown per segnalare che la lettura non è riuscita
                    kind  = BoardKind.Unknown;
                    rawId = "";
                    return false;
                }

                rawId = boardStr;
                kind  = MapBoardStringToKind(boardStr);

                Log.Debug("Shimmer", $"[Detect/Android] classified kind={kind}");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Debug("Shimmer", $"[Detect/Android][ERR] {ex.GetType().Name}: {ex.Message}");
                kind  = BoardKind.Unknown;
                rawId = "";
                return false;
            }
        }

        /// <summary>
        /// Wrapper asincrono: connette → detect → disconnette.
        /// </summary>
        public static async Task<(bool ok, BoardKind kind, string rawId)> GetExpansionBoardKindAndroidAsync(
            string deviceName, string mac)
        {
            ShimmerDroid.ShimmerLogAndStreamAndroidBluetoothV2? shim = null;
            try
            {
                if (!global::Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
                    return (false, BoardKind.Unknown, "Invalid MAC");

                shim = new ShimmerDroid.ShimmerLogAndStreamAndroidBluetoothV2(deviceName, mac);
                shim.Connect();

                // attendo la connessione
                var t0 = DateTime.UtcNow;
                while (!shim.IsConnected() && (DateTime.UtcNow - t0).TotalMilliseconds < 6000)
                    await Task.Delay(50);

                if (!shim.IsConnected())
                    return (false, BoardKind.Unknown, "No connect");

                // piccolo margine per inizializzare il thread di lettura
                await Task.Delay(200);

                var ok = TryDetectBoardKind(shim, out var kind, out var raw);
                return (ok, kind, raw);
            }
            catch (System.Exception ex)
            {
                return (false, BoardKind.Unknown, ex.Message);
            }
            finally
            {
                try { shim?.Disconnect(); } catch { }
            }
        }

        // ----------------- Helper (Android) -----------------

        private static BoardKind MapBoardStringToKind(string boardStr)
        {
            if (string.IsNullOrWhiteSpace(boardStr))
                return BoardKind.Unknown;

            return boardStr.IndexOf("EXG", StringComparison.OrdinalIgnoreCase) >= 0
                ? BoardKind.EXG
                : BoardKind.IMU;
        }

        /// <summary>
        /// Trova nel grafo dell’oggetto (profondità limitata) un istanza che espone <c>GetExpansionBoard()</c>.
        /// </summary>
        private static object? FindExpansionTarget(object root, int maxDepth)
        {
            if (root == null || maxDepth < 0) return null;

            var visited = new HashSet<object>(new RefEqComparer());
            var q = new Queue<(object obj, int depth)>();

            EnqueueIfNew(root, 0);

            while (q.Count > 0)
            {
                var (obj, depth) = q.Dequeue();
                if (obj == null) continue;

                // se questo oggetto ha GetExpansionBoard(), lo prendo
                if (HasMethod(obj, "GetExpansionBoard"))
                    return obj;

                if (depth >= maxDepth) continue;

                // scandisco fields e properties non indicizzate
                var t = obj.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                foreach (var f in t.GetFields(flags))
                {
                    object? val = null;
                    try { val = f.GetValue(obj); } catch { }
                    if (val == null) continue;
                    EnqueueIfNew(val, depth + 1);
                }

                foreach (var p in t.GetProperties(flags))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    object? val = null;
                    try { val = p.GetValue(obj); } catch { }
                    if (val == null) continue;
                    EnqueueIfNew(val, depth + 1);
                }
            }

            return null;

            void EnqueueIfNew(object o, int d)
            {
                if (o == null) return;
                // evito di inserire tipi "semplici" inutili
                var tt = o.GetType();
                if (tt.IsPrimitive || tt.IsEnum || tt == typeof(string)) return;
                if (visited.Add(o)) q.Enqueue((o, d));
            }
        }

        private static bool HasMethod(object instance, string methodName)
        {
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return m != null && m.GetParameters().Length == 0;
        }

        /// <summary>Attende che <c>GetExpansionBoard()</c> torni una stringa non vuota. Ritenta a metà timeout.</summary>
        private static bool TryWaitExpansionString(object target, out string boardStr, int timeoutMs)
        {
            boardStr = GetStringNoArgIfExists(target, "GetExpansionBoard") ?? "";
            if (!string.IsNullOrWhiteSpace(boardStr)) return true;

            var waited = 0;
            const int step = 100;
            var retried = false;

            while (waited < timeoutMs)
            {
                SafeDelay(step);
                waited += step;

                boardStr = GetStringNoArgIfExists(target, "GetExpansionBoard") ?? "";
                if (!string.IsNullOrWhiteSpace(boardStr)) return true;

                if (!retried && waited >= timeoutMs / 2)
                {
                    retried = true;
                    InvokeNoArgIfExists(target, "ReadExpansionBoard");
                }
            }

            boardStr = GetStringNoArgIfExists(target, "GetExpansionBoard") ?? "";
            return !string.IsNullOrWhiteSpace(boardStr);
        }

        private static void SafeDelay(int ms)
        {
            try { System.Threading.Thread.Sleep(ms); } catch { }
        }

        /// <summary>Invoca via reflection un metodo senza argomenti se esiste (public o non-public).</summary>
        private static object? InvokeNoArgIfExists(object instance, string methodName)
        {
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null && m.GetParameters().Length == 0)
            {
                try
                {
                    var res = m.Invoke(instance, null);
                    Log.Debug("Shimmer", $"[Detect/Android] {methodName} invoked on {t.Name} -> {(res == null ? "-" : res.ToString())}");
                    return res;
                }
                catch (Exception ex)
                {
                    Log.Debug("Shimmer", $"[Detect/Android] {methodName} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
            else
            {
                Log.Debug("Shimmer", $"[Detect/Android] {methodName} not found on {t.FullName}");
            }
            return null;
        }

        /// <summary>Invoca via reflection un getter-stringa senza argomenti se esiste.</summary>
        private static string? GetStringNoArgIfExists(object instance, string methodName)
        {
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null && m.GetParameters().Length == 0)
            {
                try
                {
                    var v = m.Invoke(instance, null);
                    return v?.ToString();
                }
                catch (Exception ex)
                {
                    Log.Debug("Shimmer", $"[Detect/Android] {methodName} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
            return null;
        }

        // Comparer reference-based per "visited" nella BFS
        private sealed class RefEqComparer : IEqualityComparer<object>
        {
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

#endif // ANDROID
    }
}
#endif
