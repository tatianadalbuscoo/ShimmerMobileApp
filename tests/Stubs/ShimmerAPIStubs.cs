// tests/Stubs/ShimmerAPIStubs.cs
// Stub minimi per eseguire i test cross-OS senza I/O reale.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ShimmerAPI
{
    public static class ShimmerBluetooth
    {
        public const int SHIMMER_STATE_CONNECTED = 1;
        public const int SHIMMER_STATE_STREAMING = 2;

        public enum ShimmerIdentifier
        {
            MSG_IDENTIFIER_DATA_PACKET = 0x00,
            MSG_IDENTIFIER_STATE_CHANGE = 0x01,
            MSG_IDENTIFIER_NOTIFICATION_MESSAGE = 0x02,
            MSG_IDENTIFIER_PACKET_RECEPTION_RATE = 0x03
        }

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

    public class SensorData
    {
        public double Data { get; }
        public SensorData(double d) => Data = d;
    }

    public class ObjectCluster
    {
        private readonly List<(string name, string? fmt, SensorData data)> _rows = new();

        public void Add(string name, string? format, double value)
            => _rows.Add((name, format, new SensorData(value)));

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

        public SensorData? GetData(int idx)
            => (idx >= 0 && idx < _rows.Count) ? _rows[idx].data : null;
    }

    public static class ShimmerConfiguration
    {
        public static class SignalNames
        {
            public const string SYSTEM_TIMESTAMP = "System Timestamp";
        }
        public static class SignalFormats
        {
            public const string CAL = "CAL";
        }
    }

    public static class Shimmer3Configuration
    {
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

    public class CustomEventArgs : EventArgs
    {
        private readonly int _indicator;
        private readonly object? _payload;
        public CustomEventArgs(int indicator, object? payload)
        {
            _indicator = indicator; _payload = payload;
        }
        public int getIndicator() => _indicator;
        public object? getObject() => _payload;
    }

    public class ShimmerLogAndStreamSystemSerialPortV2
    {
        public string Device { get; }
        public string Port { get; }
        public double LastSamplingRateWritten { get; private set; }
        public int EnabledSensors { get; set; }

        // --- Stato/contatori per i test del lifecycle ---
        public bool Connected { get; private set; }
        public bool IsConnected() => Connected;

        // Contatori "interni"
        public int ConnectCalls { get; private set; }
        public int StartStreamingCalls { get; private set; }
        public int StopStreamingCalls { get; private set; }
        public int InquiryCalls { get; private set; }
        public int WriteSensorsCalls { get; private set; }
        public int? LastSensorsBitmap { get; private set; }

        // Alias con i nomi attesi dai test
        public int ConnectCount => ConnectCalls;
        public int StartCount => StartStreamingCalls;
        public int StopCount => StopStreamingCalls;
        public int InquiryCount => InquiryCalls;
        public int WriteSensorsCount => WriteSensorsCalls;

        public ShimmerLogAndStreamSystemSerialPortV2(string device, string port)
        {
            Device = device; Port = port;
        }

        public event EventHandler? UICallback;

        public void Connect()
        {
            ConnectCalls++;
            Connected = true;
        }

        public void WriteSamplingRate(double hz) => LastSamplingRateWritten = hz;

        public void WriteSensors(int bitmap)
        {
            LastSensorsBitmap = bitmap;
            WriteSensorsCalls++;
        }

        public void Inquiry() => InquiryCalls++;

        public void StartStreaming() { StartStreamingCalls++; }

        public void StopStreaming() { StopStreamingCalls++; }

        public void Disconnect() { Connected = false; }

        public void RaiseDataPacket(ObjectCluster oc)
            => UICallback?.Invoke(this, new CustomEventArgs(
                (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc));
    }
}

namespace ShimmerSDK.EXG
{
    using ShimmerAPI;

    // Registro cross-OS: collega un "fake driver" al SUT senza accedere a campi privati.
    public static class ShimmerSDK_EXG_TestRegistry
    {
        private static readonly ConditionalWeakTable<ShimmerSDK_EXG, ShimmerLogAndStreamSystemSerialPortV2> _map
            = new();

        public static void Register(ShimmerSDK_EXG sut, ShimmerLogAndStreamSystemSerialPortV2 drv)
        {
            // se esiste già, rimuovi e riaggiungi
            try { _map.Remove(sut); } catch { }
            _map.Add(sut, drv);
        }

        public static ShimmerLogAndStreamSystemSerialPortV2? Get(ShimmerSDK_EXG sut)
            => _map.TryGetValue(sut, out var drv) ? drv : null;
    }

    public static class ShimmerSDK_EXG_TestExtensions
    {
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

            var fake = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, portOrId)
            {
                EnabledSensors = enabled
            };

            // collega gli handler eventi reali se esistono (Windows e/o Android)
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

            // registra il driver nel registro cross-OS
            ShimmerSDK_EXG_TestRegistry.Register(sut, fake);

            // forza "primo pacchetto" se i campi esistono
            TrySetField(sut, "firstDataPacket", true);
            TrySetField(sut, "firstDataPacketAndroid", true);

            // inietta anche nel campo privato shimmer se presente (no-op se mancante)
            TrySetField(sut, "shimmer", fake);
        }

        private static void SetBool(object o, string field, bool v) => SetField(o, field, v);
        private static void SetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) throw new MissingFieldException(o.GetType().FullName, field);
            fi.SetValue(o, v);
        }
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

    // Registro cross-OS per IMU (stesso pattern dell’EXG)
    public static class ShimmerSDK_IMU_TestRegistry
    {
        private static readonly ConditionalWeakTable<ShimmerSDK_IMU, ShimmerLogAndStreamSystemSerialPortV2> _map
            = new();

        public static void Register(ShimmerSDK_IMU sut, ShimmerLogAndStreamSystemSerialPortV2 drv)
        {
            try { _map.Remove(sut); } catch { }
            _map.Add(sut, drv);
        }

        public static ShimmerLogAndStreamSystemSerialPortV2? Get(ShimmerSDK_IMU sut)
            => _map.TryGetValue(sut, out var drv) ? drv : null;
    }

    public static class ShimmerSDK_IMU_TestExtensions
    {
        public static void TestConfigure(
            this ShimmerSDK_IMU sut,
            string deviceName,
            string portOrId,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemperature, // <- nome coerente con la firma
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15
        )
        {
            // set dei flag privati
            SetBool(sut, "_enableLowNoiseAccelerometer", enableLowNoiseAcc);
            SetBool(sut, "_enableWideRangeAccelerometer", enableWideRangeAcc);
            SetBool(sut, "_enableGyroscope", enableGyro);
            SetBool(sut, "_enableMagnetometer", enableMag);
            SetBool(sut, "_enablePressureTemperature", enablePressureTemperature); // <- fix
            SetBool(sut, "_enableBattery", enableBattery);
            SetBool(sut, "_enableExtA6", enableExtA6);
            SetBool(sut, "_enableExtA7", enableExtA7);
            SetBool(sut, "_enableExtA15", enableExtA15);

            // bitmap sensori (niente EXG qui)
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

            // collega gli handler eventi reali se esistono (Windows/Android/Mac…)
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
                // Fallback: se non c'è un handler privato, rilancia l'evento pubblico SampleReceived con uno snapshot minimale
                fake.UICallback += (sender, e) =>
                {
                    if (e is ShimmerAPI.CustomEventArgs cea &&
                        cea.getIndicator() == (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
                    {
                        var evField = sut.GetType().GetField("SampleReceived", BindingFlags.Instance | BindingFlags.NonPublic);
                        var dlg = evField?.GetValue(sut) as Delegate;
                        if (dlg != null)
                        {
                            var payload = new ShimmerSDK_IMUData(cea.getObject());
                            dlg.DynamicInvoke(sut, payload);
                        }
                    }
                };
            }

            // registra il fake driver
            ShimmerSDK_IMU_TestRegistry.Register(sut, fake);

            // forza mappatura al prossimo pacchetto se il campo esiste
            TrySetField(sut, "firstDataPacket", true);
            TrySetField(sut, "firstDataPacketAndroid", true);

            // inietta anche nel campo privato shimmer se presente (no-op se assente)
            TrySetField(sut, "shimmer", fake);
        }

        private static void SetBool(object o, string field, bool v) => SetField(o, field, v);
        private static void SetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) throw new MissingFieldException(o.GetType().FullName, field);
            fi.SetValue(o, v);
        }
        private static bool TrySetField(object o, string field, object? v)
        {
            var fi = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null) return false;
            fi.SetValue(o, v);
            return true;
        }
    }
}
