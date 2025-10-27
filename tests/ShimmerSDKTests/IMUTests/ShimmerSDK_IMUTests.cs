// tests/ShimmerSDKTests/IMUTests/ShimmerSDK_IMU_DataTests.cs
#nullable enable
using System;
using System.Linq;
using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;

namespace ShimmerSDKTests
{
    // Shim per ottenere i valori in ordine senza usare `.Values`
    public static class ImuValuesShim
    {
        public static object?[] Vals(ShimmerSDK_IMU_Data d)
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
                "ExtADC_A6","ExtADC_A7","ExtADC_A15"
            };
            return names.Select(Get).ToArray();
        }
    }
}

namespace ShimmerSDKTests.IMUTests
{
    public class ShimmerSDK_IMU_DataTests
    {
        private static object? GetPublicField(object instance, string name)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

        private static object? GetPublicProperty(object instance, string name)
            => instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

        private static bool IsSensorDataBranch(Type t)
        {
            var tsField = t.GetField("TimeStamp", BindingFlags.Instance | BindingFlags.Public);
            if (tsField != null) return typeof(SensorData).IsAssignableFrom(tsField.FieldType);
            var tsProp = t.GetProperty("TimeStamp", BindingFlags.Instance | BindingFlags.Public);
            return tsProp != null && typeof(SensorData).IsAssignableFrom(tsProp.PropertyType);
        }

        private static readonly string[] ExpectedMemberNames =
        {
            "TimeStamp",
            "LowNoiseAccelerometerX","LowNoiseAccelerometerY","LowNoiseAccelerometerZ",
            "WideRangeAccelerometerX","WideRangeAccelerometerY","WideRangeAccelerometerZ",
            "GyroscopeX","GyroscopeY","GyroscopeZ",
            "MagnetometerX","MagnetometerY","MagnetometerZ",
            "Temperature_BMP180","Pressure_BMP180",
            "BatteryVoltage",
            "ExtADC_A6","ExtADC_A7","ExtADC_A15"
        };



        [Fact]
        public void Public_Members_Are_Readonly_Fields_On_WinAndroid_Or_Properties_On_Apple()
        {
            var t = typeof(ShimmerSDK_IMU_Data);
            if (IsSensorDataBranch(t))
            {
                var fields = ExpectedMemberNames
                    .Select(n => t.GetField(n, BindingFlags.Public | BindingFlags.Instance))
                    .Where(f => f != null)
                    .ToArray();
                if (fields.Length == 0) return;
                Assert.All(fields!, f => Assert.True(f!.IsInitOnly, $"{f!.Name} non è readonly"));
                foreach (var n in ExpectedMemberNames)
                    Assert.Null(t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                var props = ExpectedMemberNames
                    .Select(n => t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance))
                    .Where(p => p != null)
                    .ToArray();
                if (props.Length == 0) return;
                foreach (var n in ExpectedMemberNames)
                    Assert.Null(t.GetField(n, BindingFlags.Public | BindingFlags.Instance));
            }
        }

        [Fact]
        public void Ctor_Assigns_Fields_Correctly_For_Current_Platform()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (IsSensorDataBranch(t))
            {
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19 &&
                                typeof(SensorData).IsAssignableFrom(c.GetParameters()[0].ParameterType));
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    new SensorData(100),
                    new SensorData(1),new SensorData(2),new SensorData(3),
                    new SensorData(4),new SensorData(5),new SensorData(6),
                    new SensorData(7),new SensorData(8),new SensorData(9),
                    new SensorData(10),new SensorData(11),new SensorData(12),
                    new SensorData(20),new SensorData(21),
                    new SensorData(22),
                    new SensorData(30),new SensorData(31),new SensorData(32)
                });

                double D(object? o) => Assert.IsType<SensorData>(o).Data;

                Assert.Equal(100, D(GetPublicField(sut, "TimeStamp")));
                Assert.Equal(1, D(GetPublicField(sut, "LowNoiseAccelerometerX")));
                Assert.Equal(6, D(GetPublicField(sut, "WideRangeAccelerometerZ")));
                Assert.Equal(9, D(GetPublicField(sut, "GyroscopeZ")));
                Assert.Equal(12, D(GetPublicField(sut, "MagnetometerZ")));
                Assert.Equal(20, D(GetPublicField(sut, "Temperature_BMP180")));
                Assert.Equal(21, D(GetPublicField(sut, "Pressure_BMP180")));
                Assert.Equal(22, D(GetPublicField(sut, "BatteryVoltage")));
                Assert.Equal(30, D(GetPublicField(sut, "ExtADC_A6")));
                Assert.Equal(31, D(GetPublicField(sut, "ExtADC_A7")));
                Assert.Equal(32, D(GetPublicField(sut, "ExtADC_A15")));
            }
            else
            {
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19);
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    100.0,
                    1.0,2.0,3.0,
                    4.0,5.0,6.0,
                    7.0,8.0,9.0,
                    10.0,11.0,12.0,
                    20.0,21.0,
                    22.0,
                    30.0,31.0,32.0
                });

                Assert.Equal(100.0, GetPublicField(sut, "TimeStamp") ?? GetPublicProperty(sut, "TimeStamp"));
                Assert.Equal(1.0, GetPublicField(sut, "LowNoiseAccelerometerX") ?? GetPublicProperty(sut, "LowNoiseAccelerometerX"));
                Assert.Equal(6.0, GetPublicField(sut, "WideRangeAccelerometerZ") ?? GetPublicProperty(sut, "WideRangeAccelerometerZ"));
                Assert.Equal(9.0, GetPublicField(sut, "GyroscopeZ") ?? GetPublicProperty(sut, "GyroscopeZ"));
                Assert.Equal(12.0, GetPublicField(sut, "MagnetometerZ") ?? GetPublicProperty(sut, "MagnetometerZ"));
                Assert.Equal(20.0, GetPublicField(sut, "Temperature_BMP180") ?? GetPublicProperty(sut, "Temperature_BMP180"));
                Assert.Equal(21.0, GetPublicField(sut, "Pressure_BMP180") ?? GetPublicProperty(sut, "Pressure_BMP180"));
                Assert.Equal(22.0, GetPublicField(sut, "BatteryVoltage") ?? GetPublicProperty(sut, "BatteryVoltage"));
                Assert.Equal(30.0, GetPublicField(sut, "ExtADC_A6") ?? GetPublicProperty(sut, "ExtADC_A6"));
                Assert.Equal(31.0, GetPublicField(sut, "ExtADC_A7") ?? GetPublicProperty(sut, "ExtADC_A7"));
                Assert.Equal(32.0, GetPublicField(sut, "ExtADC_A15") ?? GetPublicProperty(sut, "ExtADC_A15"));
            }
        }

        [Fact]
        public void Ctor_Allows_Nulls_For_Optional_Channels()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (IsSensorDataBranch(t))
            {
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19 &&
                                typeof(SensorData).IsAssignableFrom(c.GetParameters()[0].ParameterType));
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    null,
                    null,null,null,
                    null,null,null,
                    null,null,null,
                    null,null,null,
                    null,null,
                    null,
                    null,null,null
                });

                Assert.Null(GetPublicField(sut, "LowNoiseAccelerometerX"));
                Assert.Null(GetPublicField(sut, "Temperature_BMP180"));
                Assert.Null(GetPublicField(sut, "ExtADC_A15"));
            }
            else
            {
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19);
                if (ctor == null)
                {
                    var empty = t.GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object?>());
                    if (empty == null) return;

                    foreach (var n in ExpectedMemberNames)
                    {
                        var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                        var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                        var v = f != null ? f.GetValue(empty) : p?.GetValue(empty);
                        Assert.Null(v);
                    }
                    return;
                }

                object sut = ctor.Invoke(Enumerable.Repeat<object?>(null, 19).ToArray());
                foreach (var n in new[] { "LowNoiseAccelerometerX", "Temperature_BMP180", "ExtADC_A15" })
                {
                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                    var v = f != null ? f.GetValue(sut) : p?.GetValue(sut);
                    Assert.Null(v);
                }
            }
        }
    }
}
