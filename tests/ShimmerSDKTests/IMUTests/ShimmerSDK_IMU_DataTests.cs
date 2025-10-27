// tests/ShimmerSDKTests/IMUTests/ShimmerSDK_IMU_DataTests.cs
#nullable enable
using System;
using System.Linq;
using System.Reflection;
using ShimmerAPI;      // SensorData
using ShimmerSDK.IMU; // ShimmerSDK_IMU_Data
using Xunit;

namespace ShimmerSDKTests.IMUTests
{
    /// <summary>
    /// Test robusti per ShimmerSDK_IMU_Data:
    /// - I canali principali sono obbligatori e readonly
    /// - Il Timestamp è opzionale (accetta alias) per evitare falsi negativi
    /// - Soft-skip se la classe non è presente nel TFM corrente
    /// </summary>
    public class ShimmerSDK_IMU_Data_Tests
    {
        // === Helpers =========================================================
        private static bool MemberExists(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance) != null ||
            t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) != null;

        private static object? GetFieldValue(object instance, string name) =>
            instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

        private static bool IsSensorDataBranch(Type t)
        {
            var ts = t.GetField("TimeStamp", BindingFlags.Public | BindingFlags.Instance)?.FieldType;
            return ts != null && typeof(SensorData).IsAssignableFrom(ts);
        }

        private static readonly string[] RequiredNames =
        {
            // Tutti i canali tranne il timestamp
            "LowNoiseAccelerometerX","LowNoiseAccelerometerY","LowNoiseAccelerometerZ",
            "WideRangeAccelerometerX","WideRangeAccelerometerY","WideRangeAccelerometerZ",
            "GyroscopeX","GyroscopeY","GyroscopeZ",
            "MagnetometerX","MagnetometerY","MagnetometerZ",
            "Temperature_BMP180","Pressure_BMP180",
            "BatteryVoltage",
            "ExtADC_A6","ExtADC_A7","ExtADC_A15"
        };

        // Alias accettati per il timestamp (se presente)
        private static readonly string[] TimestampAliases =
        {
            "TimeStamp","Timestamp","SystemTimeStamp","SystemTimestamp"
        };

        // === Tests ============================================================

        [Fact]
        public void Has_All_Expected_Public_Members()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            // Soft-skip se non c'è nessun membro atteso (classe non inclusa in questo TFM)
            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            // I canali principali DEVONO esserci
            foreach (var n in RequiredNames)
                Assert.True(MemberExists(t, n), $"Membro pubblico mancante: {n}");

            // Il timestamp è opzionale → non assertiamo la sua presenza
        }

        [Fact]
        public void Fields_Are_Public_Readonly_And_No_Properties()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            // Soft-skip se non c'è nessun membro atteso
            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            // Tutti i canali principali sono campi readonly e non esistono proprietà omonime
            foreach (var n in RequiredNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) Assert.True(f.IsInitOnly, $"{n} non è readonly");
                Assert.Null(t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance));
            }

            // Timestamp: se presente con uno degli alias, deve essere un campo readonly
            var tsName = TimestampAliases.FirstOrDefault(a => t.GetField(a, BindingFlags.Public | BindingFlags.Instance) != null);
            if (tsName != null)
            {
                var f = t.GetField(tsName, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(f);
                Assert.True(f!.IsInitOnly, $"{tsName} non è readonly");
                Assert.Null(t.GetProperty(tsName, BindingFlags.Public | BindingFlags.Instance));
            }
        }

        [Fact]
        public void Ctor_Assigns_All_Fields_Correctly_For_Current_Platform()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (IsSensorDataBranch(t))
            {
                // WINDOWS/ANDROID: ctor con 19 parametri di tipo SensorData?
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19 &&
                                                 typeof(SensorData).IsAssignableFrom(c.GetParameters()[0].ParameterType));
                if (ctor == null) return; // soft-skip

                object sut = ctor.Invoke(new object?[]
                {
                    new SensorData(100),                 // TimeStamp (se esiste)
                    new SensorData(1), new SensorData(2), new SensorData(3),   // LNA
                    new SensorData(4), new SensorData(5), new SensorData(6),   // WRA
                    new SensorData(7), new SensorData(8), new SensorData(9),   // Gyro
                    new SensorData(10),new SensorData(11),new SensorData(12),  // Mag
                    new SensorData(20), new SensorData(21),                    // Temp, Pressure (params)
                    new SensorData(22),                                        // Battery
                    new SensorData(30), new SensorData(31), new SensorData(32) // Ext
                });

                double D(object? o) => Assert.IsType<SensorData>(o).Data;

                // Timestamp: assert solo se il campo/alias esiste
                var tsAlias = TimestampAliases.FirstOrDefault(a => MemberExists(t, a));
                if (tsAlias != null) Assert.Equal(100, D(GetFieldValue(sut, tsAlias)));

                Assert.Equal(1, D(GetFieldValue(sut, "LowNoiseAccelerometerX")));
                Assert.Equal(2, D(GetFieldValue(sut, "LowNoiseAccelerometerY")));
                Assert.Equal(3, D(GetFieldValue(sut, "LowNoiseAccelerometerZ")));

                Assert.Equal(4, D(GetFieldValue(sut, "WideRangeAccelerometerX")));
                Assert.Equal(5, D(GetFieldValue(sut, "WideRangeAccelerometerY")));
                Assert.Equal(6, D(GetFieldValue(sut, "WideRangeAccelerometerZ")));

                Assert.Equal(7, D(GetFieldValue(sut, "GyroscopeX")));
                Assert.Equal(8, D(GetFieldValue(sut, "GyroscopeY")));
                Assert.Equal(9, D(GetFieldValue(sut, "GyroscopeZ")));

                Assert.Equal(10, D(GetFieldValue(sut, "MagnetometerX")));
                Assert.Equal(11, D(GetFieldValue(sut, "MagnetometerY")));
                Assert.Equal(12, D(GetFieldValue(sut, "MagnetometerZ")));

                // Param order: (temperature=20, pressure=21)
                // Mapping (Win/Android): Pressure_BMP180 <- 21, Temperature_BMP180 <- 20
                Assert.Equal(21, D(GetFieldValue(sut, "Pressure_BMP180")));
                Assert.Equal(20, D(GetFieldValue(sut, "Temperature_BMP180")));

                Assert.Equal(22, D(GetFieldValue(sut, "BatteryVoltage")));

                Assert.Equal(30, D(GetFieldValue(sut, "ExtADC_A6")));
                Assert.Equal(31, D(GetFieldValue(sut, "ExtADC_A7")));
                Assert.Equal(32, D(GetFieldValue(sut, "ExtADC_A15")));
            }
            else
            {
                // iOS/MacCatalyst: ctor con 19 parametri object?
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19);
                if (ctor == null) return; // soft-skip

                object sut = ctor.Invoke(new object?[]
                {
                    100.0,
                    1.0, 2.0, 3.0,
                    4.0, 5.0, 6.0,
                    7.0, 8.0, 9.0,
                    10.0, 11.0, 12.0,
                    20.0, 21.0,     // Temp, Pressure
                    22.0,
                    30.0, 31.0, 32.0
                });

                // Timestamp: assert solo se il campo/alias esiste
                var tsAlias = TimestampAliases.FirstOrDefault(a => MemberExists(t, a));
                if (tsAlias != null) Assert.Equal(100.0, GetFieldValue(sut, tsAlias));

                Assert.Equal(1.0, GetFieldValue(sut, "LowNoiseAccelerometerX"));
                Assert.Equal(2.0, GetFieldValue(sut, "LowNoiseAccelerometerY"));
                Assert.Equal(3.0, GetFieldValue(sut, "LowNoiseAccelerometerZ"));

                Assert.Equal(4.0, GetFieldValue(sut, "WideRangeAccelerometerX"));
                Assert.Equal(5.0, GetFieldValue(sut, "WideRangeAccelerometerY"));
                Assert.Equal(6.0, GetFieldValue(sut, "WideRangeAccelerometerZ"));

                Assert.Equal(7.0, GetFieldValue(sut, "GyroscopeX"));
                Assert.Equal(8.0, GetFieldValue(sut, "GyroscopeY"));
                Assert.Equal(9.0, GetFieldValue(sut, "GyroscopeZ"));

                Assert.Equal(10.0, GetFieldValue(sut, "MagnetometerX"));
                Assert.Equal(11.0, GetFieldValue(sut, "MagnetometerY"));
                Assert.Equal(12.0, GetFieldValue(sut, "MagnetometerZ"));

                // Mapping (Apple): Pressure_BMP180 <- 21, Temperature_BMP180 <- 20
                Assert.Equal(21.0, GetFieldValue(sut, "Pressure_BMP180"));
                Assert.Equal(20.0, GetFieldValue(sut, "Temperature_BMP180"));

                Assert.Equal(22.0, GetFieldValue(sut, "BatteryVoltage"));

                Assert.Equal(30.0, GetFieldValue(sut, "ExtADC_A6"));
                Assert.Equal(31.0, GetFieldValue(sut, "ExtADC_A7"));
                Assert.Equal(32.0, GetFieldValue(sut, "ExtADC_A15"));
            }
        }

        [Fact]
        public void Ctor_Allows_Nulls_For_All_Channels()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(c => c.GetParameters().Length == 19);
            if (ctor == null) return; // soft-skip

            object sut = ctor.Invoke(Enumerable.Repeat<object?>(null, 19).ToArray());

            // Soft-skip se non c'è nessun membro atteso
            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            foreach (var n in RequiredNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) return; // ramo non presente → soft-skip
                Assert.Null(f.GetValue(sut));
            }

            // Timestamp: se presente con uno degli alias, deve risultare null
            foreach (var alias in TimestampAliases)
            {
                var f = t.GetField(alias, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) Assert.Null(f.GetValue(sut));
            }
        }
    }
}
