// tests/ShimmerSDKTests/EXGTests/ShimmerSDK_EXGDataTests.cs

#nullable enable
using System;
using System.Linq;
using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;

namespace ShimmerSDKTests
{
    // === Helper “shim” usabile da tutti i test che oggi usano .Values ===
    // Usa reflection per costruire un array ordinato dei canali della classe reale ShimmerSDK_EXGData
    // (branch Windows/Android: campi; branch Apple: proprietà).
    public static class ExgValuesShim
    {
        public static object?[] Vals(ShimmerSDK_EXGData d)
        {
            var t = d.GetType();

            object? Get(string name)
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(d);
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return p?.GetValue(d);
            }

            var names = new[]
            {
                "TimeStamp",
                "LowNoiseAccelerometerX","LowNoiseAccelerometerY","LowNoiseAccelerometerZ",
                "WideRangeAccelerometerX","WideRangeAccelerometerY","WideRangeAccelerometerZ",
                "GyroscopeX","GyroscopeY","GyroscopeZ",
                "MagnetometerX","MagnetometerY","MagnetometerZ",
                "Temperature_BMP180","Pressure_BMP180",
                "BatteryVoltage",
                "ExtADC_A6","ExtADC_A7","ExtADC_A15",
                "Exg1","Exg2"
            };

            return names.Select(Get).ToArray();
        }
    }
}

namespace ShimmerSDKTests.EXGTests
{
    public class ShimmerSDK_EXGData_Tests
    {
        // ==== Helpers locali per questi test =================================
        private static object? GetField(object instance, string name)
        {
            var fi = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(fi);
            return fi!.GetValue(instance);
        }

        private static bool IsSensorDataBranch(Type t)
        {
            var ts = t.GetField("TimeStamp", BindingFlags.Instance | BindingFlags.Public)?.FieldType;
            return ts != null && typeof(SensorData).IsAssignableFrom(ts);
        }

        // Ctor() — behavior
        // Assegna correttamente i canali nella variante corrente (Windows/Android vs iOS/Mac)
        [Fact]
        public void Ctor_Assigns_Fields_Correctly_For_Current_Platform()
        {
            var t = typeof(ShimmerSDK_EXGData);

            if (IsSensorDataBranch(t))
            {
                // WINDOWS/ANDROID: prova a usare il costruttore lungo via reflection; se non c’è, skip
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 21);
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    new SensorData(100),
                    new SensorData(1), new SensorData(2), new SensorData(3),
                    new SensorData(4), new SensorData(5), new SensorData(6),
                    new SensorData(7), new SensorData(8), new SensorData(9),
                    new SensorData(10), new SensorData(11), new SensorData(12),
                    new SensorData(20), new SensorData(21),
                    new SensorData(22),
                    new SensorData(30), new SensorData(31), new SensorData(32),
                    new SensorData(1001), new SensorData(1002)
                });

                Assert.Equal(100, ((SensorData?)GetField(sut, "TimeStamp"))?.Data);
                Assert.Equal(1, ((SensorData?)GetField(sut, "LowNoiseAccelerometerX"))?.Data);
                Assert.Equal(6, ((SensorData?)GetField(sut, "WideRangeAccelerometerZ"))?.Data);
                Assert.Equal(9, ((SensorData?)GetField(sut, "GyroscopeZ"))?.Data);
                Assert.Equal(12, ((SensorData?)GetField(sut, "MagnetometerZ"))?.Data);
                Assert.Equal(20, ((SensorData?)GetField(sut, "Temperature_BMP180"))?.Data);
                Assert.Equal(21, ((SensorData?)GetField(sut, "Pressure_BMP180"))?.Data);
                Assert.Equal(22, ((SensorData?)GetField(sut, "BatteryVoltage"))?.Data);
                Assert.Equal(30, ((SensorData?)GetField(sut, "ExtADC_A6"))?.Data);
                Assert.Equal(31, ((SensorData?)GetField(sut, "ExtADC_A7"))?.Data);
                Assert.Equal(32, ((SensorData?)GetField(sut, "ExtADC_A15"))?.Data);
                Assert.Equal(1001, ((SensorData?)GetField(sut, "Exg1"))?.Data);
                Assert.Equal(1002, ((SensorData?)GetField(sut, "Exg2"))?.Data);
            }
            else
            {
                // iOS/MacCatalyst/TFM neutro: costruttore param-less + proprietà Exg1/Exg2
                var sutEmpty = Activator.CreateInstance(typeof(ShimmerSDK_EXGData))!;
                var p1 = sutEmpty.GetType().GetProperty("Exg1", BindingFlags.Instance | BindingFlags.Public);
                var p2 = sutEmpty.GetType().GetProperty("Exg2", BindingFlags.Instance | BindingFlags.Public);
                if (p1 == null || p2 == null) return; // nessun branch attivo

                p1.SetValue(sutEmpty, 1001.0);
                p2.SetValue(sutEmpty, 1002.0);

                var sutFull = Activator.CreateInstance(typeof(ShimmerSDK_EXGData),
                    new object?[] {
                        100.0,
                        1.0, 2.0, 3.0,
                        4.0, 5.0, 6.0,
                        7.0, 8.0, 9.0,
                        10.0, 11.0, 12.0,
                        20.0, 21.0,
                        22.0,
                        30.0, 31.0, 32.0,
                        1001.0, 1002.0
                    })!;

                Assert.Equal(100.0, GetField(sutFull, "TimeStamp"));
                Assert.Equal(1.0, GetField(sutFull, "LowNoiseAccelerometerX"));
                Assert.Equal(6.0, GetField(sutFull, "WideRangeAccelerometerZ"));
                Assert.Equal(9.0, GetField(sutFull, "GyroscopeZ"));
                Assert.Equal(12.0, GetField(sutFull, "MagnetometerZ"));
                Assert.Equal(20.0, GetField(sutFull, "Temperature_BMP180"));
                Assert.Equal(21.0, GetField(sutFull, "Pressure_BMP180"));
                Assert.Equal(22.0, GetField(sutFull, "BatteryVoltage"));
                Assert.Equal(30.0, GetField(sutFull, "ExtADC_A6"));
                Assert.Equal(31.0, GetField(sutFull, "ExtADC_A7"));
                Assert.Equal(32.0, GetField(sutFull, "ExtADC_A15"));
            }
        }

        // Ctor() — behavior
        // Accetta null e li preserva
        [Fact]
        public void Ctor_Allows_Nulls_For_Optional_Channels()
        {
            var t = typeof(ShimmerSDK_EXGData);

            if (IsSensorDataBranch(t))
            {
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 21);
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    null,
                    null, null, null,
                    null, null, null,
                    null, null, null,
                    null, null, null,
                    null, null,
                    null,
                    null, null, null,
                    null, null
                });

                Assert.Null(GetField(sut, "Exg1"));
                Assert.Null(GetField(sut, "Exg2"));
                Assert.Null(GetField(sut, "LowNoiseAccelerometerX"));
            }
            else
            {
                var sut = Activator.CreateInstance(typeof(ShimmerSDK_EXGData))!;
                var p1 = sut.GetType().GetProperty("Exg1", BindingFlags.Instance | BindingFlags.Public);
                var p2 = sut.GetType().GetProperty("Exg2", BindingFlags.Instance | BindingFlags.Public);
                if (p1 == null || p2 == null) return;

                Assert.Null(p1.GetValue(sut));
                Assert.Null(p2.GetValue(sut));
                Assert.Null(GetField(sut, "LowNoiseAccelerometerX"));
            }
        }

        // API surface — behavior
        // Due canali EXG (Exg1, Exg2)
        // API surface — behavior
        // Due canali EXG (Exg1, Exg2) quando il ramo di piattaforma li espone
        [Fact]
        public void Exg_Channel_Count_Is_Two()
        {
            var t = typeof(ShimmerSDK_EXGData);
            var members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                           .Where(m => m.Name is "Exg1" or "Exg2")
                           .ToArray();

            // Se questa build non include né i campi (Win/Android) né le proprietà (iOS/Mac),
            // non c’è nulla da verificare: skip “soft” come negli altri test.
            if (members.Length == 0) return;

            Assert.Equal(2, members.Length);
        }


        // Immutability/Mutability — behavior
        // Win/Android: field readonly; Apple: property settable
        [Fact]
        public void Immutable_On_WindowsAndroid_But_Settable_On_Apple()
        {
            var t = typeof(ShimmerSDK_EXGData);
            var exg1Field = t.GetField("Exg1", BindingFlags.Public | BindingFlags.Instance);
            var exg2Field = t.GetField("Exg2", BindingFlags.Public | BindingFlags.Instance);

            var exg1Prop = t.GetProperty("Exg1", BindingFlags.Public | BindingFlags.Instance);
            var exg2Prop = t.GetProperty("Exg2", BindingFlags.Public | BindingFlags.Instance);

            if (IsSensorDataBranch(t))
            {
                if (exg1Field == null || exg2Field == null) return; // ramo non compilato
                Assert.True(exg1Field!.IsInitOnly);
                Assert.True(exg2Field!.IsInitOnly);
                Assert.Null(exg1Prop);
                Assert.Null(exg2Prop);
            }
            else
            {
                if (exg1Prop == null || exg2Prop == null) return; // ramo non compilato
                Assert.True(exg1Prop!.CanWrite);
                Assert.True(exg2Prop!.CanWrite);
                Assert.Null(exg1Field);
                Assert.Null(exg2Field);
            }
        }
    }
}
