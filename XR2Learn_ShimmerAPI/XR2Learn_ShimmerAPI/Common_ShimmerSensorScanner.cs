#if WINDOWS
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;

namespace XR2Learn_ShimmerAPI
{
    public static partial class ShimmerSensorScanner
    {
        // Solo questi casi: EXG oppure IMU (fallback). Keep Unknown per errori.
        public enum BoardKind { Unknown, EXG, IMU }

        /// Tenta di leggere la daughter-card: ritorna true se riesce a leggere qualcosa.
        /// - kind: EXG oppure IMU (qualsiasi cosa non-EXG viene mappata a IMU)
        /// - rawId: testo o enum risolto (se disponibile)
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

                // Metodi possibili nei vari FW
                foreach (var mName in new[] { "GetExpansionBoardID", "ReadExpansionBoardID", "GetExpansionBoard", "GetDaughterCardID" })
                {
                    var m = t.GetMethod(mName, BindingFlags.Public | BindingFlags.Instance);
                    if (m != null && m.GetParameters().Length == 0)
                    {
                        val = m.Invoke(shim, null);
                        if (val != null) break;
                    }
                }

                // Fallback proprietà
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

                // Se vediamo "EXG" esplicito → EXG
                if (upper.Contains("EXG"))
                {
                    kind = BoardKind.EXG;
                    return true;
                }

                // Default: qualunque altra cosa → IMU
                kind = BoardKind.IMU;

                // Se è numerico, prova a risolvere sugli enum; solo se matcha EXG lo metti EXG
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
                                kind  = name.Contains("EXG") ? BoardKind.EXG : BoardKind.IMU; // non-EXG ⇒ IMU
                                return true;
                            }
                        }
                    }

                    // ID numerico letto ma non mappato: abbiamo letto qualcosa → resta IMU
                    return true;
                }

                // Testo non-EXG: già impostato a IMU
                return true;
            }
            catch
            {
                kind  = BoardKind.Unknown;
                rawId = "";
                return false;
            }
        }

        /// Helper asincrono che apre/chiude e ritorna (ok, kind, rawId)
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

        /// Ritorna i nomi-canale così come li espone il FW (no probe aggressivo).
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
    }
}
#endif
