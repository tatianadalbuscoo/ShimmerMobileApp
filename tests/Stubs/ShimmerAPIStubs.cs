/* 
 * ShimmerAPIStubs.cs
 * Purpose: Cross-OS, no-I/O test stubs to drive unit tests without real hardware/SDK.
 */


using System.Reflection;
using System.Runtime.CompilerServices;


namespace ShimmerAPI
{

    /// <summary>
    /// Minimal subset of Shimmer Bluetooth constants and enumerations used by tests.
    /// </summary>
    public static class ShimmerBluetooth
    {

        public const int SHIMMER_STATE_CONNECTED = 1;       // Logical state: device is connected.
        public const int SHIMMER_STATE_STREAMING = 2;       // Logical state: device is streaming.


        /// <summary>
        /// Identifiers that tag SDK/UI callback messages.
        /// </summary>
        public enum ShimmerIdentifier
        {
            MSG_IDENTIFIER_DATA_PACKET = 0x00,
            MSG_IDENTIFIER_STATE_CHANGE = 0x01,
            MSG_IDENTIFIER_NOTIFICATION_MESSAGE = 0x02,
            MSG_IDENTIFIER_PACKET_RECEPTION_RATE = 0x03
        }


        /// <summary>
        /// Bitfield of Shimmer3 sensor flags; combine with bitwise OR.
        /// </summary>
        [Flags]
        public enum SensorBitmapShimmer3
        {
            SENSOR_A_ACCEL = 1 << 0,
            SENSOR_D_ACCEL = 1 << 1,
            SENSOR_MPU9150_GYRO = 1 << 2,
            SENSOR_LSM303DLHC_MAG = 1 << 3,
            SENSOR_BMP180_PRESSURE = 1 << 4,
            SENSOR_VBATT = 1 << 5,
            SENSOR_EXT_A6 = 1 << 6,
            SENSOR_EXT_A7 = 1 << 7,
            SENSOR_EXT_A15 = 1 << 8,
            SENSOR_EXG1_24BIT = 1 << 9,
            SENSOR_EXG2_24BIT = 1 << 10
        }
    }


    /// <summary>
    /// Minimal data wrapper representing a single numeric sample.
    /// </summary>
    public class SensorData
    {

        /// <summary>
        /// The stored numeric value.
        /// </summary>
        public double Data { get; }


        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="d">Value to store.</param>
        public SensorData(double d) => Data = d;
    }


    /// <summary>
    /// Lightweight container that mimics SDK ObjectCluster (name/format -> value rows).
    /// </summary>
    public class ObjectCluster
    {
        private readonly List<(string name, string? fmt, SensorData data)> _rows = new();


        /// <summary>
        /// Adds a row to the cluster.
        /// </summary>
        /// <param name="name">Signal name.</param>
        /// <param name="format">Signal format (e.g., CAL/RAW); optional.</param>
        /// <param name="value">Numeric value.</param>
        public void Add(string name, string? format, double value)
            => _rows.Add((name, format, new SensorData(value)));


        /// <summary>
        /// Finds the index of the first row that matches <paramref name="name"/> and (if provided) <paramref name="format"/>.
        /// </summary>
        /// <returns>Zero-based index or -1 if not found.</returns>
        public int GetIndex(string name, string? format)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].name, name, StringComparison.OrdinalIgnoreCase)
                    && (format == null || string.Equals(_rows[i].fmt, format, StringComparison.OrdinalIgnoreCase)))
                    return i;
            }
            return -1;
        }
    }


    /// <summary>
    /// Common signal names and formats referenced by the code under test.
    /// </summary>
    public static class ShimmerConfiguration
    {

        /// <summary>
        /// Generic signal names.
        /// </summary>
        public static class SignalNames
        {
            public const string SYSTEM_TIMESTAMP = "System Timestamp";
        }


        /// <summary>
        /// Signal format labels.
        /// </summary>
        public static class SignalFormats
        {
            public const string CAL = "CAL";
        }
    }


    /// <summary>
    /// Shimmer3-specific signal names used by tests.
    /// </summary>
    public static class Shimmer3Configuration
    {

        /// <summary>
        /// Shimmer3 signal name strings.
        /// </summary>
        public static class SignalNames
        {
            public const string LOW_NOISE_ACCELEROMETER_X = "Low Noise Accelerometer X";
            public const string LOW_NOISE_ACCELEROMETER_Y = "Low Noise Accelerometer Y";
            public const string LOW_NOISE_ACCELEROMETER_Z = "Low Noise Accelerometer Z";
            public const string WIDE_RANGE_ACCELEROMETER_X = "Wide Range Accelerometer X";
            public const string WIDE_RANGE_ACCELEROMETER_Y = "Wide Range Accelerometer Y";
            public const string WIDE_RANGE_ACCELEROMETER_Z = "Wide Range Accelerometer Z";
            public const string GYROSCOPE_X = "Gyroscope X";
            public const string GYROSCOPE_Y = "Gyroscope Y";
            public const string GYROSCOPE_Z = "Gyroscope Z";
            public const string MAGNETOMETER_X = "Magnetometer X";
            public const string MAGNETOMETER_Y = "Magnetometer Y";
            public const string MAGNETOMETER_Z = "Magnetometer Z";
            public const string TEMPERATURE = "Temperature";
            public const string PRESSURE = "Pressure";
            public const string V_SENSE_BATT = "VSense Batt";
            public const string EXTERNAL_ADC_A6 = "External ADC A6";
            public const string EXTERNAL_ADC_A7 = "External ADC A7";
            public const string EXTERNAL_ADC_A15 = "External ADC A15";

            public const string EXG1_CH1 = "EXG1_CH1";
            public const string EXG2_CH1 = "EXG2_CH1";
        }
    }


    /// <summary>
    /// Event args carrying an indicator and optional payload, mirroring SDK callbacks.
    /// </summary>
    public class CustomEventArgs : EventArgs
    {

        private readonly int _indicator;
        private readonly object? _payload;


        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="indicator">Numeric identifier of the message.</param>
        /// <param name="payload">Optional payload object.</param>
        public CustomEventArgs(int indicator, object? payload)
        {
            _indicator = indicator; _payload = payload;
        }


        /// <summary>
        /// Gets the numeric indicator.
        /// </summary>
        public int getIndicator() => _indicator;


        /// <summary>
        /// Gets the optional payload object.
        /// </summary>
        public object? getObject() => _payload;
    }


    /// <summary>
    /// Fake driver that records lifecycle calls and raises UI-like callbacks for tests.
    /// </summary>
    public class ShimmerLogAndStreamSystemSerialPortV2
    {

        /// <summary>
        /// Logical device label.
        /// </summary>
        public string Device { get; }


        /// <summary>
        /// Port or unique identifier.
        /// </summary>
        public string Port { get; }


        /// <summary>
        /// Last sampling rate set via <see cref="WriteSamplingRate"/>.
        /// </summary>
        public double LastSamplingRateWritten { get; private set; }


        /// <summary>
        /// Enabled sensors bitmask.
        /// </summary>
        public int EnabledSensors { get; set; }


        // ----- Lifecycle state/counters for tests -----


        /// <summary>
        /// Current connection state.
        /// </summary>
        public bool Connected { get; private set; }


        /// <summary>
        /// Convenience wrapper for <see cref="Connected"/>.
        /// </summary>
        public bool IsConnected() => Connected;


        /// <summary>
        /// Total number of Connect() calls.
        /// </summary>
        public int ConnectCalls { get; private set; }


        /// <summary>
        /// Total number of StartStreaming() calls.
        /// </summary>
        public int StartStreamingCalls { get; private set; }


        /// <summary>
        /// Total number of StopStreaming() calls.
        /// </summary>
        public int StopStreamingCalls { get; private set; }


        /// <summary>
        /// Total number of Inquiry() calls.
        /// </summary>
        public int InquiryCalls { get; private set; }


        /// <summary>
        /// Total number of WriteSensors() calls.
        /// </summary>
        public int WriteSensorsCalls { get; private set; }


        /// <summary>
        /// Most recent bitmap passed to WriteSensors().
        /// </summary>
        public int? LastSensorsBitmap { get; private set; }


        // Aliases expected by tests
        public int ConnectCount => ConnectCalls;
        public int StartCount => StartStreamingCalls;
        public int StopCount => StopStreamingCalls;
        public int InquiryCount => InquiryCalls;
        public int WriteSensorsCount => WriteSensorsCalls;


        /// <summary>
        /// Create a new fake driver instance.
        /// </summary>
        /// <param name="device">Device label.</param>
        /// <param name="port">Port or ID.</param>
        public ShimmerLogAndStreamSystemSerialPortV2(string device, string port)
        {
            Device = device; Port = port;
        }


        /// <summary>
        /// Event raised to simulate SDK UI callbacks.
        /// </summary>
        public event EventHandler? UICallback;


        /// <summary>
        /// Records sampling rate configuration.
        /// </summary>
        /// <param name="hz">Sampling rate in Hz.</param>
        public void WriteSamplingRate(double hz) => LastSamplingRateWritten = hz;


        /// <summary>
        /// Raises a data-packet callback with the provided cluster.
        /// </summary>
        /// <param name="oc">Object cluster payload.</param>
        public void RaiseDataPacket(ObjectCluster oc)
            => UICallback?.Invoke(this, new CustomEventArgs(
                (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc));
    }
}



namespace ShimmerSDK.EXG
{
    using ShimmerAPI;


    /// <summary>
    /// Cross-OS registry: associates an EXG SUT with its fake driver without touching private fields.
    /// </summary>
    public static class ShimmerSDK_EXG_TestRegistry
    {

        private static readonly ConditionalWeakTable<ShimmerSDK_EXG, ShimmerLogAndStreamSystemSerialPortV2> _map
            = new();


        /// <summary>
        /// Registers (replace if present) a fake driver for the SUT.
        /// </summary>
        /// <param name="sut">System under test.</param>
        /// <param name="drv">Fake driver instance.</param>
        public static void Register(ShimmerSDK_EXG sut, ShimmerLogAndStreamSystemSerialPortV2 drv)
        {
            try { _map.Remove(sut); } catch { }
            _map.Add(sut, drv);
        }


        /// <summary>
        /// Gets the fake driver for the specified SUT, if any.
        /// </summary>
        public static ShimmerLogAndStreamSystemSerialPortV2? Get(ShimmerSDK_EXG sut)
            => _map.TryGetValue(sut, out var drv) ? drv : null;
    }


    /// <summary>
    /// Test-time helpers to configure EXG SUT flags and wire event handlers.
    /// </summary>
    public static class ShimmerSDK_EXG_TestExtensions
    {

        /// <summary>
        /// Configures private fields on the SUT, computes the sensors bitmap,
        /// injects a fake driver, and binds available private handlers.
        /// </summary>
        /// <param name="sut">System under test.</param>
        /// <param name="deviceName">Logical device name for the fake driver.</param>
        /// <param name="portOrId">Port or unique identifier for the fake driver.</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable pressure/temperature.</param>
        /// <param name="enableBatteryVoltage">Enable battery voltage telemetry.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        /// <param name="enableExg">Enable EXG channels.</param>
        /// <param name="exgMode">EXG operation mode to set.</param>
        public static void TestConfigure(
            this ShimmerSDK_EXG sut,
            string deviceName,
            string portOrId,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBatteryVoltage,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15,
            bool enableExg,
            ExgMode exgMode
        )
        {

            // Set private feature flags
            SetBool(sut, "_enableLowNoiseAccelerometer", enableLowNoiseAcc);
            SetBool(sut, "_enableWideRangeAccelerometer", enableWideRangeAcc);
            SetBool(sut, "_enableGyroscope", enableGyro);
            SetBool(sut, "_enableMagnetometer", enableMag);
            SetBool(sut, "_enablePressureTemperature", enablePressureTemp);
            SetBool(sut, "_enableBatteryVoltage", enableBatteryVoltage);
            SetBool(sut, "_enableExtA6", enableExtA6);
            SetBool(sut, "_enableExtA7", enableExtA7);
            SetBool(sut, "_enableExtA15", enableExtA15);
            SetBool(sut, "_enableExg", enableExg);
            SetField(sut, "_exgMode", exgMode);

            // Build sensor bitmap
            int enabled = 0;
            if (enableLowNoiseAcc) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
            if (enableWideRangeAcc) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
            if (enableGyro) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
            if (enableMag) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
            if (enablePressureTemp) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
            if (enableBatteryVoltage) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
            if (enableExtA6) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
            if (enableExtA7) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
            if (enableExtA15) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;
            if (enableExg)
            {
                enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT;
                enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;
            }

            // Create and configure the fake driver
            var fake = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, portOrId)
            {
                EnabledSensors = enabled
            };

            var h1 = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            var h2 = sut.GetType().GetMethod("HandleEventAndroid", BindingFlags.Instance | BindingFlags.NonPublic);

            if (h1 != null)
            {
                var d1 = Delegate.CreateDelegate(typeof(EventHandler), sut, h1);
                fake.UICallback += (EventHandler)d1;
            }
            if (h2 != null)
            {
                var d2 = Delegate.CreateDelegate(typeof(EventHandler), sut, h2);
                fake.UICallback += (EventHandler)d2;
            }

            // Register the driver
            ShimmerSDK_EXG_TestRegistry.Register(sut, fake);

            // Force "first packet" paths if present
            TrySetField(sut, "firstDataPacket", true);
            TrySetField(sut, "firstDataPacketAndroid", true);

            // Best-effort injection into private 'shimmer' field (no-op if missing)
            TrySetField(sut, "shimmer", fake);
        }


        /// <summary>
        /// Convenience wrapper to set a private <see cref="bool"/> field via reflection.
        /// </summary>
        /// <param name="o">Target instance.</param>
        /// <param name="field">Private instance field name.</param>
        /// <param name="v">Boolean value to assign.</param>
        private static void SetBool(object o, string field, bool v) => SetField(o, field, v);


        /// <summary>
        /// Sets a private instance field via reflection, throwing if the field is not found.
        /// </summary>
        /// <param name="o">The target instance containing the field.</param>
        /// <param name="field">The exact name of the private instance field to set.</param>
        /// <param name="v">The value to assign to the field.</param>
        /// <exception cref="MissingFieldException">
        /// Thrown when the specified <paramref name="field"/> does not exist on the target type.
        /// </exception>
        private static void SetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) throw new MissingFieldException(o.GetType().FullName, field);
            fi.SetValue(o, v);
        }


        /// <summary>
        /// Attempts to set a private instance field via reflection.
        /// </summary>
        /// <param name="o">The target instance containing the field.</param>
        /// <param name="field">The exact name of the private instance field to set.</param>
        /// <param name="v">The value to assign to the field.</param>
        /// <returns>
        /// <c>true</c> if the field was found and set; otherwise <c>false</c>.
        /// </returns>
        private static bool TrySetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) return false;
            fi.SetValue(o, v);
            return true;
        }
    }
}


namespace ShimmerSDK.IMU
{
    using ShimmerAPI;
    using System.Runtime.CompilerServices;
    using System.Reflection;


    /// <summary>
    /// Cross-OS registry that associates an IMU SUT instance with its fake driver
    /// without accessing private fields directly.
    /// </summary>
    public static class ShimmerSDK_IMU_TestRegistry
    {
        private static readonly ConditionalWeakTable<ShimmerSDK_IMU, ShimmerLogAndStreamSystemSerialPortV2> _map
            = new();


        /// <summary>
        /// Registers (replaces if already present) the fake driver for the specified SUT.
        /// </summary>
        /// <param name="sut">System under test.</param>
        /// <param name="drv">Fake driver to associate with <paramref name="sut"/>.</param>
        public static void Register(ShimmerSDK_IMU sut, ShimmerLogAndStreamSystemSerialPortV2 drv)
        {
            try { _map.Remove(sut); } catch { }
            _map.Add(sut, drv);
        }


        /// <summary>
        /// Retrieves the fake driver associated with the given SUT, if any.
        /// </summary>
        /// <param name="sut">System under test.</param>
        /// <returns>
        /// The mapped <see cref="ShimmerLogAndStreamSystemSerialPortV2"/> instance,
        /// or <c>null</c> when no mapping exists.
        /// </returns>
        public static ShimmerLogAndStreamSystemSerialPortV2? Get(ShimmerSDK_IMU sut)
            => _map.TryGetValue(sut, out var drv) ? drv : null;
    }


    /// <summary>
    /// Test-only extension helpers to configure the IMU SUT, compute sensor bitmaps,
    /// and wire event handlers.
    /// </summary>
    public static class ShimmerSDK_IMU_TestExtensions
    {

        /// <summary>
        /// Configures private flags on the SUT, builds the enabled-sensors bitmap,
        /// injects a fake driver, and binds the first available private handler.
        /// </summary>
        /// <param name="sut">System under test.</param>
        /// <param name="deviceName">Logical device name for the fake driver.</param>
        /// <param name="portOrId">Port or unique identifier for the fake driver.</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemperature">Enable pressure/temperature.</param>
        /// <param name="enableBattery">Enable battery telemetry.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        public static void TestConfigure(
            this ShimmerSDK_IMU sut,
            string deviceName,
            string portOrId,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemperature,
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15
        )
        {

            // Set private feature flags
            SetBool(sut, "_enableLowNoiseAccelerometer", enableLowNoiseAcc);
            SetBool(sut, "_enableWideRangeAccelerometer", enableWideRangeAcc);
            SetBool(sut, "_enableGyroscope", enableGyro);
            SetBool(sut, "_enableMagnetometer", enableMag);
            SetBool(sut, "_enablePressureTemperature", enablePressureTemperature); // <- fix
            SetBool(sut, "_enableBattery", enableBattery);
            SetBool(sut, "_enableExtA6", enableExtA6);
            SetBool(sut, "_enableExtA7", enableExtA7);
            SetBool(sut, "_enableExtA15", enableExtA15);

            // Build sensor bitmap
            int enabled = 0;
            if (enableLowNoiseAcc) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
            if (enableWideRangeAcc) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
            if (enableGyro) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
            if (enableMag) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
            if (enablePressureTemperature) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE; // <- fix
            if (enableBattery) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
            if (enableExtA6) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
            if (enableExtA7) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
            if (enableExtA15) enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

            var fake = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, portOrId)
            {
                EnabledSensors = enabled
            };

            var candidateHandlers = new[]
            {
                "HandleEvent",
                "HandleEventWindows",
                "HandleEventAndroid",
                "HandleEventMac",
                "HandleEventMacCatalyst",
                "HandleEventiOS"
            };

            EventHandler? bound = null;
            foreach (var name in candidateHandlers)
            {
                var mi = sut.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) continue;

                var del = Delegate.CreateDelegate(typeof(EventHandler), sut, mi, throwOnBindFailure: false);
                if (del is EventHandler eh)
                {
                    bound = eh;
                    break;
                }
            }
            if (bound != null)
            {
                fake.UICallback += bound;
            }
            else
            {
                fake.UICallback += (sender, e) =>
                {
                    if (e is ShimmerAPI.CustomEventArgs cea &&
                        cea.getIndicator() == (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
                    {
                        var evField = sut.GetType().GetField("SampleReceived", BindingFlags.Instance | BindingFlags.NonPublic);
                        var dlg = evField?.GetValue(sut) as Delegate;
                        if (dlg != null)
                        {
                            dlg.DynamicInvoke(sut, cea.getObject());
                        }
                    }
                };

            }

            // register the fake driver
            ShimmerSDK_IMU_TestRegistry.Register(sut, fake);

            // force mapping to next packet if field exists
            TrySetField(sut, "firstDataPacket", true);
            TrySetField(sut, "firstDataPacketAndroid", true);

            // also inject shimmer into the private field if present (no-op if absent)
            TrySetField(sut, "shimmer", fake);
        }


        /// <summary>
        /// Convenience wrapper to set a private <see cref="bool"/> field via reflection.
        /// </summary>
        /// <param name="o">Target instance.</param>
        /// <param name="field">Name of the private instance field.</param>
        /// <param name="v">Boolean value to assign.</param>
        private static void SetBool(object o, string field, bool v) => SetField(o, field, v);


        /// <summary>
        /// Sets a private instance field via reflection, throwing if the field is not found.
        /// </summary>
        /// <param name="o">The target instance containing the field.</param>
        /// <param name="field">The exact name of the private instance field to set.</param>
        /// <param name="v">The value to assign to the field.</param>
        /// <exception cref="MissingFieldException">
        /// Thrown when the specified <paramref name="field"/> does not exist on the target type.
        /// </exception>
        private static void SetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) throw new MissingFieldException(o.GetType().FullName, field);
            fi.SetValue(o, v);
        }


        /// <summary>
        /// Attempts to set a private instance field via reflection.
        /// </summary>
        /// <param name="o">The target instance containing the field.</param>
        /// <param name="field">The exact name of the private instance field to set.</param>
        /// <param name="v">The value to assign to the field.</param>
        /// <returns>
        /// <c>true</c> if the field was found and set; otherwise <c>false</c>.
        /// </returns>
        private static bool TrySetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) return false;
            fi.SetValue(o, v);
            return true;
        }
    }
}
