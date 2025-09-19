/*
 * Cross-platform expansion-board detection for Shimmer devices (Windows & Android).
 * Identifies the board type (EXG/IMU).
 */


#if WINDOWS || ANDROID
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;

#if ANDROID
using Log = global::Android.Util.Log;
using ShimmerDroid = ShimmerSDK.Android;
using System.Collections;
using System.Collections.Generic;
#endif

namespace ShimmerSDK
{

    /// <summary>
    /// Provides helper methods to detect the installed expansion board (EXG/IMU)
    /// on Shimmer devices. Works on Windows with direct ShimmerAPI calls and on
    /// Android with a reflection-based fallback.
    /// </summary>
    public static partial class ShimmerSensorScanner
    {

        // Enumeration of supported expansion board types.
        public enum BoardKind { Unknown, EXG, IMU }


#if WINDOWS
       

        /// <summary>
        /// Detects the expansion board type using the concrete ShimmerAPI calls.
        /// </summary>
        /// <param name="shim">The Shimmer Windows V2 serial-port wrapper.</param>
        /// <param name="kind">Detected board kind (Unknown, EXG, IMU).</param>
        /// <param name="rawId">Raw expansion board identifier string.</param>
        /// <returns><c>true</c> if detection succeeded; otherwise <c>false</c>.</returns>
        public static bool TryDetectBoardKind(
            ShimmerLogAndStreamSystemSerialPortV2 shim,
            out BoardKind kind,
            out string rawId)
        {
            kind  = BoardKind.Unknown;
            rawId = "";

            try
            {
                // Ask the device to refresh the expansion-board ID, then read it back.
                shim.ReadExpID();
                System.Threading.Thread.Sleep(200);

                var board = shim.GetExpansionBoard();
                if (string.IsNullOrWhiteSpace(board))
                    return false;

                rawId = board;

                // Simple mapping: anything containing "EXG" → EXG, otherwise IMU.
                kind = board.IndexOf("EXG", StringComparison.OrdinalIgnoreCase) >= 0
                     ? BoardKind.EXG
                     : BoardKind.IMU;

                return true;
            }
            catch
            {
                kind  = BoardKind.Unknown;
                rawId = "";
                return false;
            }
        }


        /// <summary>
        /// Connects to a Shimmer device via Windows serial port, attempts to detect
        /// the installed expansion board type (IMU/EXG), and disconnects afterwards.
        /// </summary>
        /// <param name="deviceName">Logical device name.</param>
        /// <param name="comPort">Target COM port.</param>
        /// <returns>
        /// A tuple: (ok = true if detection succeeded, kind = detected board type,
        /// rawId = raw identifier string returned by the device).
        /// </returns>
        public static async Task<(bool ok, BoardKind kind, string rawId)> GetExpansionBoardKindWindowsAsync(
            string deviceName, string comPort)
        {
            ShimmerLogAndStreamSystemSerialPortV2? shim = null;
            try
            {

                // Create the serial-port wrapper and connect
                shim = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                shim.Connect();
                await Task.Delay(150);

                // Try to detect the expansion board
                var ok = TryDetectBoardKind(shim, out var kind, out var raw);
                return (ok, kind, raw);
            }
            catch
            {
                return (false, BoardKind.Unknown, "");
            }
            finally
            {

                // Disconnect safely
                try { shim?.Disconnect(); } catch { }
            }
        }


#endif // WINDOWS


#if ANDROID


        /// <summary>
        /// Detects the installed expansion board on Android using reflection-based calls
        /// (forwards ReadInternalExpPower/ReadExpansionBoard and polls GetExpansionBoard).
        /// </summary>
        /// <param name="shim">Android Bluetooth V2 wrapper; must be connected.</param>
        /// <param name="kind">Output: detected board kind (Unknown, EXG, IMU).</param>
        /// <param name="rawId">Output: raw board identifier string returned by the device.</param>
        /// <returns><c>true</c> if a non-empty board string was read and mapped; otherwise <c>false</c>.</returns>
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
                    return false;
                }


                if (!shim.IsConnected())
                {
                    return false;
                }

                // Locate, via reflection, a target object exposing GetExpansionBoard/ReadExpansionBoard.
                var target = FindExpansionTarget(shim, maxDepth: 3);
                if (target == null)
                {
                    return false;
                }

                // Issue the read commands (if present).
                InvokeNoArgIfExists(target, "ReadInternalExpPower");
                InvokeNoArgIfExists(target, "ReadExpansionBoard");
                SafeDelay(120);

                // Poll for a non-empty board string.
                string boardStr;
                var ok = TryWaitExpansionString(target, out boardStr, timeoutMs: 2600);

                // Retry once if empty.
                if (!ok)
                {
                    InvokeNoArgIfExists(target, "ReadExpansionBoard");
                    ok = TryWaitExpansionString(target, out boardStr, timeoutMs: 1400);
                }

                // Map the result.
                if (!ok || string.IsNullOrWhiteSpace(boardStr))
                {
                    kind  = BoardKind.Unknown;  // signal that detection failed
                    rawId = "";
                    return false;
                }

                rawId = boardStr;
                kind  = MapBoardStringToKind(boardStr); // "EXG" -> EXG, else IMU
                return true;
            }
            catch (System.Exception ex)
            {
                kind  = BoardKind.Unknown;
                rawId = "";
                return false;
            }
        }


        /// <summary>
        /// - Connects to a Shimmer device over Android Bluetooth, waits for the link to be ready,
        /// - then detects the installed expansion board (EXG/IMU) and disconnects.
        /// </summary>
        /// <param name="deviceName">Logical device name for logs/SDK.</param>
        /// <param name="mac">Bluetooth MAC address of the target device.</param>
        /// <returns>
        /// (ok = true if detection succeeded, kind = detected board type, rawId = raw board string
        /// or an error hint like "Invalid MAC"/"No connect").
        /// </returns>
        public static async Task<(bool ok, BoardKind kind, string rawId)> GetExpansionBoardKindAndroidAsync(
            string deviceName, string mac)
        {
            ShimmerDroid.ShimmerLogAndStreamAndroidBluetoothV2? shim = null;
            try
            {

                // Validate MAC format early.
                if (!global::Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
                    return (false, BoardKind.Unknown, "Invalid MAC");

                // Create wrapper and start connecting.
                shim = new ShimmerDroid.ShimmerLogAndStreamAndroidBluetoothV2(deviceName, mac);
                shim.Connect();

                // Wait for the connection to become active (max 6s).
                var t0 = DateTime.UtcNow;
                while (!shim.IsConnected() && (DateTime.UtcNow - t0).TotalMilliseconds < 6000)
                    await Task.Delay(50);

                if (!shim.IsConnected())
                    return (false, BoardKind.Unknown, "No connect");

                await Task.Delay(200);

                // Delegate actual detection
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


        // ----- Helper (Android) -----


        /// <summary>
        /// Maps the raw expansion-board string to a <see cref="BoardKind"/>.
        /// </summary>
        /// <param name="boardStr">Raw board identifier returned by the device (e.g., "EXG", "IMU_...").</param>
        /// <returns>
        /// <see cref="BoardKind.EXG"/> if the string contains "EXG" (case-insensitive);
        /// <see cref="BoardKind.IMU"/> if non-empty and not EXG;
        /// otherwise <see cref="BoardKind.Unknown"/>.
        /// </returns>
        private static BoardKind MapBoardStringToKind(string boardStr)
        {
            if (string.IsNullOrWhiteSpace(boardStr))
                return BoardKind.Unknown;

            return boardStr.IndexOf("EXG", StringComparison.OrdinalIgnoreCase) >= 0
                ? BoardKind.EXG
                : BoardKind.IMU;
        }


        /// <summary>
        /// Performs a bounded BFS over the object graph to find an instance that exposes
        /// a parameterless <c>GetExpansionBoard()</c> method (via reflection).
        /// </summary>
        /// <param name="root">Root object to start the search from.</param>
        /// <param name="maxDepth">Maximum traversal depth (0 = root only).</param>
        /// <returns>
        /// The first object that has a parameterless <c>GetExpansionBoard()</c> method; otherwise <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Uses reference-based visited tracking to avoid cycles and ignores primitives/enums/strings
        /// to keep the search cheap. Scans both fields and non-indexed properties (public and non-public).
        /// </remarks>
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

                // If this object exposes GetExpansionBoard(), we are done.
                if (HasMethod(obj, "GetExpansionBoard"))
                    return obj;

                // Stop expanding beyond the depth limit.
                if (depth >= maxDepth) continue;

                // Scan fields and non-indexed properties (public and non-public).
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

            // Local helper: enqueue only non-primitive, non-enum, non-string objects
            // and only if not seen before (reference equality).
            void EnqueueIfNew(object o, int d)
            {
                if (o == null) return;
                var tt = o.GetType();
                if (tt.IsPrimitive || tt.IsEnum || tt == typeof(string)) return;
                if (visited.Add(o)) q.Enqueue((o, d));
            }
        }


        /// <summary>
        /// Checks via reflection whether the given instance exposes a parameterless instance method
        /// with the specified name (public or non-public).
        /// </summary>
        /// <param name="instance">Object to inspect.</param>
        /// <param name="methodName">Target method name.</param>
        /// <returns><c>true</c> if such a method exists; otherwise <c>false</c>.</returns>
                private static bool HasMethod(object instance, string methodName)
        {
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return m != null && m.GetParameters().Length == 0;
        }


        /// <summary>
        /// Polls (with timeout) for a non-empty expansion-board string via reflection.
        /// Calls <c>GetExpansionBoard()</c> repeatedly; halfway through the timeout it retries
        /// by invoking <c>ReadExpansionBoard()</c> once to refresh the value.
        /// </summary>
        /// <param name="target">Object exposing <c>GetExpansionBoard()</c> (and optionally <c>ReadExpansionBoard()</c>).</param>
        /// <param name="boardStr">Output: the retrieved board string, or empty if none was obtained.</param>
        /// <param name="timeoutMs">Maximum time to wait, in milliseconds.</param>
        /// <returns><c>true</c> if a non-empty board string was obtained within the timeout; otherwise <c>false</c>.</returns>
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

                // One mid-timeout refresh attempt to trigger the device to provide the value.
                if (!retried && waited >= timeoutMs / 2)
                {
                    retried = true;
                    InvokeNoArgIfExists(target, "ReadExpansionBoard");
                }
            }

            boardStr = GetStringNoArgIfExists(target, "GetExpansionBoard") ?? "";
            return !string.IsNullOrWhiteSpace(boardStr);
        }


        /// <summary>
        /// Sleeps for the specified number of milliseconds, swallowing any exceptions
        /// (best-effort delay guard for background/polling code).
        /// </summary>
        /// <param name="ms">Delay in milliseconds.</param>
        private static void SafeDelay(int ms)
        {
            try { System.Threading.Thread.Sleep(ms); } catch { }
        }


        /// <summary>
        /// Invokes, via reflection, a parameterless instance method if present
        /// (public or non-public). Returns the method result or <c>null</c> on failure.
        /// </summary>
        /// <param name="instance">Object to invoke the method on.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <returns>The invocation result, or <c>null</c> if the method is missing or throws.</returns>
        private static object? InvokeNoArgIfExists(object instance, string methodName)
        {
            var t = instance.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null && m.GetParameters().Length == 0)
            {
                try
                {
                    var res = m.Invoke(instance, null);
                    return res;
                }
                catch (Exception ex)
                {
                    Log.Debug("Shimmer", $"[Detect/Android] {methodName} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
            return null;
        }


        /// <summary>
        /// Invokes, via reflection, a parameterless instance method expected to return a string
        /// (public or non-public). Converts the result to <see cref="string"/> if present.
        /// </summary>
        /// <param name="instance">Object to invoke the method on.</param>
        /// <param name="methodName">Name of the parameterless method to call.</param>
        /// <returns>The returned string, or <c>null</c> if the method is missing, throws, or returns null.</returns>
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


        /// <summary>
        /// Reference-based equality comparer used to track visited objects in BFS traversal.
        /// Compares by object identity (<see cref="ReferenceEquals"/>) and uses
        /// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)"/> for hashing.
        /// </summary>
        private sealed class RefEqComparer : IEqualityComparer<object>
        {
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }


#endif // ANDROID


    }
}


#endif //  WINDOWS || ANDROID
