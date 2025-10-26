using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;

namespace ShimmerSDKTests.IMUTests
{
    public class ShimmerSDK_IMUTests
    {
        // ===== Helpers riflessione di supporto =====
        private static object? GetField(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(o);
        }
        private static void SetField(object o, string name, object? value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(o, value);
        }
        private static MethodInfo? GetPrivMethod(Type t, string name, bool isStatic = false)
        {
            var flags = BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.GetMethod(name, flags);
        }
        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fi);
            var value = fi!.GetValue(instance);
            Assert.NotNull(value);
            return (T)value!;
        }

        private static void SetPrivField(object o, string name, object? value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(o, value);
        }

        private static object? GetPrivField(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(o);
        }

        private static MethodInfo? GetPrivInstanceMethod(Type t, string name)
            => t.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

        // LatestData behavior

        [Fact]
        public void LatestData_Is_Null_By_Default()
        {
            var sut = new ShimmerSDK_IMU();
            Assert.Null(sut.LatestData);
        }

        [Fact]
        public void LatestData_Is_Updated_On_DataPacket_And_Maps_CAL_Values()
        {
            var sut = new ShimmerSDK_IMU();

            // Se il ramo WINDOWS non è compilato (niente HandleEvent), bypass (come fai già in altri test).
            var handleEvent = GetPrivInstanceMethod(typeof(ShimmerSDK_IMU), "HandleEvent");
            if (handleEvent == null) return;

            // Crea fake driver e collega l'handler reale via reflection
            var fake = new ShimmerLogAndStreamSystemSerialPortV2("DevIMU", "COMX");
            var del = Delegate.CreateDelegate(typeof(EventHandler), sut, handleEvent);
            fake.UICallback += (EventHandler)del;

            // inietta il fake nel campo privato 'shimmer' e forza firstDataPacket=true
            SetPrivField(sut, "shimmer", fake);
            SetPrivField(sut, "firstDataPacket", true);

            // Costruisci un ObjectCluster con TUTTI i segnali CAL usati dall'IMU
            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 123);

            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 3);

            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 4);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 5);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 6);

            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL, 7);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL, 8);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL, 9);

            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL, 10);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 11);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 12);

            oc.Add(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL, 20);
            oc.Add(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL, 21);
            oc.Add(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL, 22);

            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL, 30);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL, 31);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL, 32);

            // Genera l'evento come fa il driver reale
            fake.RaiseDataPacket(oc);

            // Verifica: LatestData popolato e con il numero atteso di campi (19)
            Assert.NotNull(sut.LatestData);
            var vals = sut.LatestData!.Values;
            Assert.True(vals.Length >= 19);

            // Helper locale per leggere rapidamente il double da SensorData
            static double D(object? o)
            {
                Assert.NotNull(o);
                var sd = Assert.IsType<SensorData>(o);
                return sd.Data;
            }

            // L'ordine è quello usato nel costruttore di ShimmerSDK_IMUData dentro HandleEvent():
            // ts, LNA(x,y,z), WRA(x,y,z), Gyr(x,y,z), Mag(x,y,z), Temp, Press, Batt, ExtA6, ExtA7, ExtA15
            Assert.Equal(123, D(vals[0]));
            Assert.Equal(1, D(vals[1]));
            Assert.Equal(2, D(vals[2]));
            Assert.Equal(3, D(vals[3]));
            Assert.Equal(4, D(vals[4]));
            Assert.Equal(5, D(vals[5]));
            Assert.Equal(6, D(vals[6]));
            Assert.Equal(7, D(vals[7]));
            Assert.Equal(8, D(vals[8]));
            Assert.Equal(9, D(vals[9]));
            Assert.Equal(10, D(vals[10]));
            Assert.Equal(11, D(vals[11]));
            Assert.Equal(12, D(vals[12]));
            Assert.Equal(20, D(vals[13]));
            Assert.Equal(21, D(vals[14]));
            Assert.Equal(22, D(vals[15]));
            Assert.Equal(30, D(vals[16]));
            Assert.Equal(31, D(vals[17]));
            Assert.Equal(32, D(vals[18]));
        }

        [Fact]
        public void LatestData_Handles_Missing_Signals_With_Nulls()
        {
            var sut = new ShimmerSDK_IMU();

            var handleEvent = GetPrivInstanceMethod(typeof(ShimmerSDK_IMU), "HandleEvent");
            if (handleEvent == null) return;

            var fake = new ShimmerLogAndStreamSystemSerialPortV2("DevIMU", "COMX");
            var del = Delegate.CreateDelegate(typeof(EventHandler), sut, handleEvent);
            fake.UICallback += (EventHandler)del;

            SetPrivField(sut, "shimmer", fake);
            SetPrivField(sut, "firstDataPacket", true);

            // Pacchetto con SOLO timestamp (tutti gli altri segnali assenti)
            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 999);

            fake.RaiseDataPacket(oc);

            Assert.NotNull(sut.LatestData);
            var vals = sut.LatestData!.Values;

            // ts presente, il resto può risultare null (perché gli index mappati restano -1)
            Assert.NotNull(vals[0]); // timestamp
            for (int i = 1; i < Math.Min(vals.Length, 19); i++)
                Assert.Null(vals[i]);
        }


        // ===== Ctor() =====
        [Fact]
        public void Ctor_Sets_Defaults()
        {
            var sut = new ShimmerSDK_IMU();
            Assert.Equal(51.2, sut.SamplingRate, precision: 1);
            Assert.True(GetPrivateField<bool>(sut, "_enableLowNoiseAccelerometer"));
            Assert.True(GetPrivateField<bool>(sut, "_enableWideRangeAccelerometer"));
            Assert.True(GetPrivateField<bool>(sut, "_enableGyroscope"));
            Assert.True(GetPrivateField<bool>(sut, "_enableMagnetometer"));
            Assert.True(GetPrivateField<bool>(sut, "_enablePressureTemperature"));
            Assert.True(GetPrivateField<bool>(sut, "_enableBattery"));
            Assert.True(GetPrivateField<bool>(sut, "_enableExtA6"));
            Assert.True(GetPrivateField<bool>(sut, "_enableExtA7"));
            Assert.True(GetPrivateField<bool>(sut, "_enableExtA15"));
        }

        // ===== SetFirmwareSamplingRateNearest() =====
        [Fact]
        public void SetFirmwareSamplingRateNearest_Quantizes_And_Updates_SR()
        {
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(50.0);
            // 32768 / round(32768/50=655.36) = 32768/655 ≈ 50.02748
            Assert.InRange(applied, 50.02, 50.04);
            Assert.InRange(sut.SamplingRate, 50.02, 50.04);
        }

        [Fact]
        public void SetFirmwareSamplingRateNearest_Is_Locally_Monotonic()
        {
            var sut = new ShimmerSDK_IMU();
            var a1 = sut.SetFirmwareSamplingRateNearest(100.0);
            var a2 = sut.SetFirmwareSamplingRateNearest(101.0);
            Assert.True(a2 >= a1 - 1e-9);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void SetFirmwareSamplingRateNearest_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_IMU();
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.SetFirmwareSamplingRateNearest(requested));
        }

        [Fact]
        public void SetFirmwareSamplingRateNearest_ExactDivisor_IsIdentity()
        {
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(51.2);
            Assert.InRange(applied, 51.1999, 51.2001);
            Assert.InRange(sut.SamplingRate, 51.1999, 51.2001);
        }

        private static double Quantize(double requested)
        {
            const double clock = 32768.0;
            int divider = Math.Max(1, (int)Math.Round(clock / requested, MidpointRounding.AwayFromZero));
            return clock / divider;
        }

        [Theory]
        [InlineData(50.0)]
        [InlineData(75.0)]
        [InlineData(100.0)]
        [InlineData(123.456)]
        public void SetFirmwareSamplingRateNearest_MatchesExpectedQuantization(double requested)
        {
            var sut = new ShimmerSDK_IMU();
            double expected = Quantize(requested);
            double applied = sut.SetFirmwareSamplingRateNearest(requested);
            Assert.InRange(applied, expected - 1e-12, expected + 1e-12);
            Assert.InRange(sut.SamplingRate, expected - 1e-12, expected + 1e-12);
        }

        [Fact]
        public void SetFirmwareSamplingRateNearest_IsIdempotent_ForSameInput()
        {
            var sut = new ShimmerSDK_IMU();
            var a1 = sut.SetFirmwareSamplingRateNearest(200.0);
            var a2 = sut.SetFirmwareSamplingRateNearest(200.0);
            Assert.InRange(a2, a1 - 1e-12, a1 + 1e-12);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(655)]
        public void SetFirmwareSamplingRateNearest_RoundsHalfAwayFromZero_OnDivider(int N)
        {
            const double clock = 32768.0;
            double requested = clock / (N + 0.5);
            double expected = clock / (N + 1);
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(requested);
            Assert.InRange(applied, expected - 1e-12, expected + 1e-12);
        }

        [Fact]
        public void SetFirmwareSamplingRateNearest_ClampsToClock_ForVeryHighRequest()
        {
            const double clock = 32768.0;
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(1e9);
            Assert.InRange(applied, clock - 1e-12, clock + 1e-12);
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(0.01)]
        public void SetFirmwareSamplingRateNearest_ProducesSmallPositive_ForVeryLowRequest(double requested)
        {
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(requested);
            Assert.True(applied > 0);
            Assert.True(applied <= requested * 1.01);
        }

        // ===== ConfigureWindows() & HandleEvent() =====
        [Fact]
        public void ConfigureWindows_IfPresent_BuildsDriverAndSubscribes()
        {
            var sut = new ShimmerSDK_IMU();
            var m = sut.GetType().GetMethod("ConfigureWindows", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return; // ramo WINDOWS non presente

            m.Invoke(sut, new object[] {
                "D1","COM1",
                true,true,   // LowNoiseAcc, WideRangeAcc
                true,true,   // Gyro, Mag
                true,true,   // PressureTemp, Battery
                true,false,true // ExtA6, ExtA7, ExtA15
            });

            var shimmerFi = sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic);
            var shimmer = shimmerFi?.GetValue(sut);
            Assert.NotNull(shimmer);
            var evt = shimmer!.GetType().GetEvent("UICallback");
            Assert.NotNull(evt);
        }

        [Fact]
        public void GetSafe_NegativeIndex_ReturnsNull()
        {
            var mi = typeof(ShimmerSDK_IMU).GetMethod("GetSafe", BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return; // ramo WINDOWS non presente
            var oc = new ObjectCluster();
            oc.Add("ANY", ShimmerConfiguration.SignalFormats.CAL, 42);
            var result = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(result);
        }

        [Fact]
        public void GetSafe_OutOfRangeIndex_ReturnsNull()
        {
            var mi = typeof(ShimmerSDK_IMU).GetMethod("GetSafe", BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;
            var oc = new ObjectCluster();
            oc.Add("X", ShimmerConfiguration.SignalFormats.CAL, 1);
            var result = mi.Invoke(null, new object[] { oc, 999 });
            Assert.Null(result);
        }

        [Fact]
        public void GetSafe_ValidIndex_ReturnsData()
        {
            var mi = typeof(ShimmerSDK_IMU).GetMethod("GetSafe", BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;
            var oc = new ObjectCluster();
            oc.Add("A", ShimmerConfiguration.SignalFormats.CAL, 12.34);
            int idx = oc.GetIndex("A", ShimmerConfiguration.SignalFormats.CAL);
            var result = mi.Invoke(null, new object[] { oc, idx });
            Assert.NotNull(result);
            var sd = Assert.IsType<SensorData>(result);
            Assert.Equal(12.34, sd.Data, precision: 6);
        }

        [Fact]
        public void HandleEvent_Maps_Indices_And_Raises_Sample()
        {
            var sut = new ShimmerSDK_IMU();
            var handleEvent = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handleEvent == null) return; // ramo WINDOWS non presente

            // Sottoscrivi l'evento pubblico
            ShimmerSDK.IMU.ShimmerSDK_IMUData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK.IMU.ShimmerSDK_IMUData)d;

            // Simula un ObjectCluster con tutti i segnali CAL previsti
            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 123);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 3);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 4);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 5);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 6);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL, 7);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL, 8);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL, 9);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL, 10);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 11);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 12);
            oc.Add(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL, 20);
            oc.Add(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL, 21);
            oc.Add(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL, 22);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL, 30);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL, 31);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL, 32);

            var ev = new CustomEventArgs((int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc);
            handleEvent.Invoke(sut, new object?[] { null, ev });

            Assert.NotNull(received);
            Assert.NotNull(received!.Values[0]); // timestamp
            Assert.True(received!.Values.Length >= 18);
        }

        // ===== ConfigureAndroid() & HandleEventAndroid() =====
        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("ZZ:ZZ:ZZ:ZZ:ZZ:ZZ")]
        public void ConfigureAndroid_Throws_On_Invalid_Mac(string mac)
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ConfigureAndroid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            var args = new object[]
            {
                "Dev1", mac,
                true, true,  // LN-Acc, WR-Acc
                true, true,  // Gyro, Mag
                true, true,  // Pressure/Temp, Battery
                true, false, true // ExtA6, ExtA7, ExtA15
            };

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, args));
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Invalid MAC", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigureAndroid_Builds_SensorBitmap_And_Initializes_Driver()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ConfigureAndroid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            mi.Invoke(sut, new object[]
            {
                "Dev1", "00:11:22:33:44:55",
                true,  true,   // LN-Acc, WR-Acc
                true,  true,   // Gyro, Mag
                true,  true,   // Press/Temp, Battery
                true,  false,  true   // ExtA6, ExtA7, ExtA15
            });

            // shimmerAndroid non nullo
            var fShimmerAndroid = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fShimmerAndroid == null) return; // il campo esiste solo se ramo ANDROID compilato
            var shimmerAndroid = fShimmerAndroid!.GetValue(sut);
            Assert.NotNull(shimmerAndroid);

            // bitmap attesa
            int expected =
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6 |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

            var fEnabled = sut.GetType().GetField("_androidEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fEnabled);
            Assert.Equal(expected, (int)fEnabled!.GetValue(sut)!);
        }

        [Fact]
        public void ConfigureAndroid_Resets_Index_Mapping()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ConfigureAndroid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            mi.Invoke(sut, new object[]
            {
                "DevX", "01:23:45:67:89:AB",
                false, true,   // LN-Acc off, WR-Acc on
                false, true,   // Gyro off, Mag on
                false, true,   // Press/Temp off, Battery on
                false, false, false // ExtA6/7/15 off
            });

            bool firstPacket = (bool)(sut.GetType().GetField("firstDataPacketAndroid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sut)!);
            Assert.True(firstPacket);

            string[] indexFields = new[]
            {
                "indexTimeStamp",
                "indexLowNoiseAccX","indexLowNoiseAccY","indexLowNoiseAccZ",
                "indexWideAccX","indexWideAccY","indexWideAccZ",
                "indexGyroX","indexGyroY","indexGyroZ",
                "indexMagX","indexMagY","indexMagZ",
                "indexBMP180Temperature","indexBMP180Pressure",
                "indexBatteryVoltage",
                "indexExtA6","indexExtA7","indexExtA15"
            };

            foreach (var fld in indexFields)
            {
                var fi = sut.GetType().GetField(fld, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(fi);
                Assert.Equal(-1, (int)fi!.GetValue(sut)!);
            }
        }

        // ===== ConnectInternalAndroid() =====
        [Fact]
        public void ConnectInternalAndroid_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ConnectInternalAndroid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, Array.Empty<object>()));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Contains("ConfigureAndroid", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ===== ApplySamplingRateWithSafeRestartAsync() =====
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void ApplySamplingRateWithSafeRestartAsync_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ApplySamplingRateWithSafeRestartAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            var task = (Task<double>)mi.Invoke(sut, new object[] { requested })!;
            var agg = Assert.Throws<AggregateException>(() => task.GetAwaiter().GetResult());
            var inner = agg.InnerException!;
            Assert.IsType<ArgumentOutOfRangeException>(inner);
        }

        [Fact]
        public void ApplySamplingRateWithSafeRestartAsync_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = typeof(ShimmerSDK_IMU).GetMethod("ApplySamplingRateWithSafeRestartAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            var task = (Task<double>)mi.Invoke(sut, new object[] { 100.0 })!;
            var agg = Assert.Throws<AggregateException>(() => task.GetAwaiter().GetResult());
            var inner = agg.InnerException!;
            Assert.IsType<InvalidOperationException>(inner);
            Assert.Contains("ConfigureAndroid", inner.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ===== HandleEventAndroid(): DATA_PACKET =====
        [Fact]
        public void Android_HandleEventAndroid_DataPacket_Maps_And_Raises_Sample()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "HandleEventAndroid");
            if (mi == null) return; // ramo ANDROID non presente

            // Stato: primo pacchetto
            SetField(sut, "firstDataPacketAndroid", true);

            // Ascolta l'evento pubblico
            ShimmerSDK.IMU.ShimmerSDK_IMUData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK.IMU.ShimmerSDK_IMUData)d;

            // Costruisci ObjectCluster con CAL
            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 123);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 3);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL, 4);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 5);
            oc.Add(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 6);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL, 7);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL, 8);
            oc.Add(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL, 9);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL, 10);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL, 11);
            oc.Add(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL, 12);
            oc.Add(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL, 20);
            oc.Add(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL, 21);
            oc.Add(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL, 22);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL, 30);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL, 31);
            oc.Add(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL, 32);

            var ev = new CustomEventArgs((int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc);
            mi.Invoke(sut, new object?[] { null, ev });

            Assert.NotNull(received);
            Assert.True(received!.Values.Length >= 18);
            // secondo pacchetto: riusa gli indici
            received = null;
            mi.Invoke(sut, new object?[] { null, ev });
            Assert.NotNull(received);
        }

        // ===== HandleEventAndroid(): STATE_CHANGE =====
        [Fact]
        public async Task Android_HandleEventAndroid_StateChange_SetsFlags_And_Completes_Tasks()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "HandleEventAndroid");
            if (mi == null) return; // ramo ANDROID non presente

            // Prepara TCS tramite riflessione
            var tcsConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsStreaming = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetField(sut, "_androidConnectedTcs", tcsConnected);
            SetField(sut, "_androidStreamingAckTcs", tcsStreaming);

            // Evento: CONNECTED
            var evConn = new CustomEventArgs(
                (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerBluetooth.SHIMMER_STATE_CONNECTED);
            mi.Invoke(sut, new object?[] { null, evConn });
            Assert.True(await Task.WhenAny(tcsConnected.Task, Task.Delay(200)) == tcsConnected.Task);

            // Evento: STREAMING
            var evStr = new CustomEventArgs(
                (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerBluetooth.SHIMMER_STATE_STREAMING);
            mi.Invoke(sut, new object?[] { null, evStr });
            Assert.True(await Task.WhenAny(tcsStreaming.Task, Task.Delay(200)) == tcsStreaming.Task);

            // Flag privati aggiornati
            Assert.True((bool)GetField(sut, "_androidIsStreaming")!);
            Assert.True((bool)GetField(sut, "firstDataPacketAndroid")!);
        }
    }
}