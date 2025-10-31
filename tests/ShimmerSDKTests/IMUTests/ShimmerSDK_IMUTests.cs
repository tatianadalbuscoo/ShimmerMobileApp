/*
 * ShimmerSDK_IMUTests.cs
 * Purpose: Unit tests for ShimmerSDK_IMU file.
 */


using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;


namespace ShimmerSDKTests
{

    /// <summary>
    /// Utility shim to read <see cref="ShimmerSDK_IMU_Data"/> values in a consistent order
    /// without relying on <c>.Values</c>.
    /// </summary>
    public static class ImuValuesShim
    {

        /// <summary>
        /// Returns the values of <see cref="ShimmerSDK_IMU_Data"/> in a canonical order.
        /// </summary>
        /// <param name="d">The IMU data instance.</param>
        /// <returns>Ordered array of field/property values.</returns>
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

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_IMU_Data"/>:
    /// verifies public surface shape across platform branches (fields vs properties),
    /// constructor value mapping, and nullability of optional channels.
    /// </summary
    public class ShimmerSDK_IMU_DataTests
    {

        /// <summary>
        /// Gets the value of a public instance <c>field</c> (not a property) from <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">The object that exposes the public field.</param>
        /// <param name="name">The exact name of the public instance field to read.</param>
        /// <returns>
        /// The field value if a matching public field exists; otherwise <c>null</c>.
        /// </returns>
        private static object? GetPublicField(object instance, string name)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);


        /// <summary>
        /// Gets the value of a public instance <c>property</c> (not a field) from <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">The object that exposes the public property.</param>
        /// <param name="name">The exact name of the public instance property to read.</param>
        /// <returns>
        /// The property value if a matching public property exists; otherwise <c>null</c>.
        /// </returns>
        private static object? GetPublicProperty(object instance, string name)
            => instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);


        /// <summary>
        /// Detects which API surface is compiled for <see cref="ShimmerSDK.IMU.ShimmerSDK_IMU_Data"/>:
        /// returns <c>true</c> when the type uses <see cref="SensorData"/> fields (Windows/Android branch),
        /// or <c>false</c> when it uses primitive properties (Apple branch).
        /// </summary>
        /// <param name="t">The <see cref="Type"/> to inspect (usually <c>typeof(ShimmerSDK_IMU_Data)</c>).</param>
        /// <returns>
        /// <c>true</c> if the public <c>TimeStamp</c> member is a field of type <see cref="SensorData"/>; otherwise <c>false</c>.
        /// </returns>
        private static bool IsSensorDataBranch(Type t)
        {
            var tsField = t.GetField("TimeStamp", BindingFlags.Instance | BindingFlags.Public);
            if (tsField != null) return typeof(SensorData).IsAssignableFrom(tsField.FieldType);
            var tsProp = t.GetProperty("TimeStamp", BindingFlags.Instance | BindingFlags.Public);
            return tsProp != null && typeof(SensorData).IsAssignableFrom(tsProp.PropertyType);
        }


        /// <summary>
        /// Canonical ordering of public members expected on <c>ShimmerSDK_IMU_Data</c>
        /// (timestamp, accelerometers, gyros, magnetometers, temperature/pressure,
        /// battery voltage, and external ADC channels).
        /// </summary>
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


        // ----- Public surface shape -----


        /// <summary>
        /// On Win/Android builds the members should be readonly fields; on Apple builds they should be properties.
        /// Expected: 
        /// - Win/Android: all <c>ExpectedMemberNames</c> are public readonly fields (no matching public properties).
        /// - Apple: all <c>ExpectedMemberNames</c> are public properties (no matching public fields).
        /// </summary>
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


        // ----- LatestData behavior -----


        /// <summary>
        /// Checks the default state right after construction.
        /// Expected: on a fresh instance, <c>LatestData</c> is <c>null</c>.
        /// </summary>
        [Fact]
        public void LatestData_Is_Null_By_Default()
        {
            var sut = new ShimmerSDK_IMU();
            Assert.Null(sut.LatestData);
        }


        /// <summary>
        /// Verifies encapsulation of the property.
        /// Expected: the <c>LatestData</c> setter is non-public (private/protected).
        /// </summary>
        [Fact]
        public void LatestData_Setter_Is_NonPublic()
        {
            var pi = typeof(ShimmerSDK_IMU).GetProperty("LatestData",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(pi);

            var set = pi!.SetMethod ?? pi.GetSetMethod(nonPublic: true);

            Assert.True(set == null || !set.IsPublic,
                "LatestData setter should be non-public.");
        }


        /// <summary>
        /// Smoke test that the backing property can actually hold a value when set via reflection.
        /// (Keeps things platform-agnostic; skips if we cannot construct the data type or access a setter.)
        /// Expected: after setting via reflection, <c>LatestData</c> becomes non-null and references the assigned instance.
        /// </summary>
        [Fact]
        public void LatestData_Set_Via_Reflection_When_Possible()
        {
            var sut = new ShimmerSDK_IMU();

            var pi = typeof(ShimmerSDK_IMU).GetProperty("LatestData",
                BindingFlags.Instance | BindingFlags.Public);
            var set = pi?.SetMethod ?? pi?.GetSetMethod(nonPublic: true);
            if (set == null)
                return;

            var imuDataType = typeof(ShimmerSDK_IMU).Assembly.GetType("ShimmerSDK.IMU.ShimmerSDK_IMU_Data")
                              ?? typeof(ShimmerSDK_IMU).Assembly.GetType("ShimmerSDK.IMU.ShimmerSDK_IMUData");
            if (imuDataType == null)
                return;

            object? dataInstance = null;
            try
            {
                dataInstance = Activator.CreateInstance(imuDataType, nonPublic: true);
            }
            catch
            {
                return;
            }

            if (dataInstance == null)
                return;

            set.Invoke(sut, new[] { dataInstance });
            Assert.NotNull(sut.LatestData);
            Assert.Same(dataInstance, sut.LatestData);
        }


        // ----- Constructor behavior ----- 


        /// <summary>
        /// Verifies the constructor wiring for the current platform shape (fields-with-SensorData vs properties-with-double).
        /// Expected: Each public member receives the exact value passed to the constructor
        /// (either from <see cref="SensorData.Data"/> on Windows/Android or raw doubles on Apple).
        /// </summary>
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


        /// <summary>
        /// Ensures nullable channels are accepted by the constructor for the current platform shape.
        /// Expected: Passing <c>null</c> for optional members keeps those members <c>null</c>.
        /// If only a parameterless constructor exists, a new instance has all members <c>null</c>.
        /// </summary>
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


        // ----- SetFirmwareSamplingRateNearest behavior -----


        /// <summary>
        /// Sanity check for quantization and property update.
        /// Expected: returns ≈ 50.027 Hz for request=50, and <c>SamplingRate</c> matches.
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_Quantizes_And_Updates_SR()
        {
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(50.0);
            Assert.InRange(applied, 50.02, 50.04);
            Assert.InRange(sut.SamplingRate, 50.02, 50.04);
        }


        /// <summary>
        /// Guard rails for invalid inputs.
        /// Expected:</b> throws <see cref="ArgumentOutOfRangeException"/> for non-positive values.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void SetFirmwareSamplingRateNearest_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_IMU();
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.SetFirmwareSamplingRateNearest(requested));
        }


        /// <summary>
        /// Identity when the requested is exactly representable (e.g., 51.2 Hz).
        /// Expected:</b> applied and <c>SamplingRate</c> are ≈ 51.2.
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_ExactDivisor_IsIdentity()
        {
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(51.2);
            Assert.InRange(applied, 51.1999, 51.2001);
            Assert.InRange(sut.SamplingRate, 51.1999, 51.2001);
        }


        /// <summary>
        /// Midpoint rounding rule on divider (AwayFromZero).
        /// Expected:</b> for requested=clock/(N+0.5) → applied=clock/(N+1).
        /// </summary>
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


        /// <summary>
        /// Upper clamp.
        /// Expected:</b> very large request clamps to ≈ 32768 Hz (divider=1).
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_ClampsToClock_ForVeryHighRequest()
        {
            const double clock = 32768.0;
            var sut = new ShimmerSDK_IMU();
            double applied = sut.SetFirmwareSamplingRateNearest(1e9);
            Assert.InRange(applied, clock - 1e-12, clock + 1e-12);
        }


        // ----- ConfigureWindows behavior -----


        /// <summary>
        /// Smoke test that ConfigureWindows exists and wires a driver with an event.
        /// Expected: private <c>shimmer</c> is initialized and exposes <c>UICallback</c> event.
        /// </summary>
        [Fact]
        public void ConfigureWindows_IfPresent_BuildsDriverAndSubscribes()
        {
            var sut = new ShimmerSDK_IMU();
            var m = sut.GetType().GetMethod("ConfigureWindows",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (m == null) return; // skip on non-Windows builds

            m.Invoke(sut, new object[] {
                "DevW","COM1",
                true,true,           // LN-Acc, WR-Acc
                true,true,           // Gyro, Mag
                true,true,           // Pressure/Temp, Battery
                true,false,true      // ExtA6, ExtA7, ExtA15
            });

            var shimmerFi = sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic);
            var shimmer = shimmerFi?.GetValue(sut);
            Assert.NotNull(shimmer);

            var evt = shimmer!.GetType().GetEvent("UICallback");
            Assert.NotNull(evt);
        }


        // ----- GetSafe behavior -----


        /// <summary>
        /// Helper: retrieves a private method by name.
        /// <b>Expected:</b> returns MethodInfo when found, otherwise null.
        /// </summary>
        /// <param name="t">Declaring type.</param>
        /// <param name="name">Method name.</param>
        /// <param name="isStatic">True for static methods.</param>
        private static MethodInfo? GetPrivMethod(Type t, string name, bool isStatic = false)
        {
            var flags = BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.GetMethod(name, flags);
        }


        /// <summary>
        /// GetSafe returns null when index is negative.
        /// Expected: result is null for idx &lt; 0.
        /// </summary>
        [Fact]
        public void GetSafe_NegativeIndex_ReturnsNull()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "GetSafe", isStatic: true);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("ANY", ShimmerConfiguration.SignalFormats.CAL, 42);

            var result = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(result);
        }


        /// <summary>
        /// GetSafe returns null when index is out of range.
        /// Expected: result is null for a very large index.
        /// </summary>
        [Fact]
        public void GetSafe_OutOfRangeIndex_ReturnsNull()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "GetSafe", isStatic: true);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("X", ShimmerConfiguration.SignalFormats.CAL, 1);

            var result = mi.Invoke(null, new object[] { oc, 9999 });
            Assert.Null(result);
        }


        /// <summary>
        /// GetSafe returns SensorData for a valid index.
        /// Expected: returned <see cref="SensorData"/> matches the inserted value.
        /// </summary>
        [Fact]
        public void GetSafe_ValidIndex_ReturnsData()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "GetSafe", isStatic: true);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("A", ShimmerConfiguration.SignalFormats.CAL, 12.34);
            int idx = oc.GetIndex("A", ShimmerConfiguration.SignalFormats.CAL);

            var result = mi.Invoke(null, new object[] { oc, idx });
            Assert.NotNull(result);
            var sd = Assert.IsType<SensorData>(result);
            Assert.Equal(12.34, sd.Data, precision: 6);
        }


        // ----- HandleEvent behavior -----


        /// <summary>
        /// Helper: gets the value of a private instance field by name.
        /// </summary>
        /// <param name="o">Target object instance.</param>
        /// <param name="name">Exact private field name to read.</param>
        /// <returns>
        /// The current value of the private field if found; otherwise <c>null</c>.
        /// </returns>
        private static object? GetField(object o, string name)
            => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(o);


        /// <summary>
        /// Reflection helper: sets the value of a private instance field by name.
        /// Fails the test if the field cannot be found.
        /// </summary>
        /// <param name="o">Target object instance.</param>
        /// <param name="name">Exact private field name to write.</param>
        /// <param name="value">Value to assign to the field (may be <c>null</c>).</param>
        private static void SetField(object o, string name, object? value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(o, value);
        }


        /// <summary>
        /// Builds a synthetic <see cref="ShimmerAPI.ObjectCluster"/> populated with
        /// CAL-format IMU signals suitable for unit tests.
        /// </summary>
        /// <returns>
        /// A fully-populated <see cref="ShimmerAPI.ObjectCluster"/> where each signal
        /// is added with format <c>ShimmerConfiguration.SignalFormats.CAL</c>.
        /// </returns>
        private static ShimmerAPI.ObjectCluster MakeFullImuCluster()
        {
            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add(ShimmerAPI.ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 123);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 3);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 4);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 5);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 6);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 7);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 8);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 9);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 10);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 11);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 12);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 20);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.PRESSURE, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 21);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 22);

            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 30);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 31);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 32);

            return oc;
        }


        /// <summary>
        /// Smoke test that simulates a single DATA_PACKET to ensure LatestData is set
        /// and the SampleReceived event fires once.
        /// Expected: LatestData != null and raised == 1.
        /// </summary>
        [Fact]
        public void Win_HandleEvent_DataPacket_Populates_LatestData_And_Raises_Event()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK.IMU.ShimmerSDK_IMU), "HandleEvent");
            if (mi == null) return;

            var sut = new ShimmerSDK.IMU.ShimmerSDK_IMU();

            int raised = 0;
            sut.SampleReceived += (_, __) => raised++;

            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET,
                MakeFullImuCluster());

            mi.Invoke(sut, new object?[] { null, ev });

            Assert.NotNull(sut.LatestData);
            Assert.Equal(1, raised);
        }


        /// <summary>
        /// First packet should build the index mapping (firstDataPacket -> false),
        /// next packet should still raise an event without remapping.
        /// Expected: firstDataPacket flips to false after first call; raised == 2 after two packets.
        /// </summary>
        [Fact]
        public void Win_HandleEvent_Maps_On_First_Packet_Then_Continues_Raising()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK.IMU.ShimmerSDK_IMU), "HandleEvent");
            if (mi == null) return;

            var sut = new ShimmerSDK.IMU.ShimmerSDK_IMU();

            SetField(sut, "firstDataPacket", true);

            int raised = 0;
            sut.SampleReceived += (_, __) => raised++;

            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET,
                MakeFullImuCluster());

            // First package -> index mapping
            mi.Invoke(sut, new object?[] { null, ev });
            Assert.False((bool)GetField(sut, "firstDataPacket")!);
            Assert.Equal(1, raised);

            // Second packet -> no remap, but event still raised
            mi.Invoke(sut, new object?[] { null, ev });
            Assert.Equal(2, raised);
            Assert.NotNull(sut.LatestData);
        }


        /// <summary>
        /// When _reconfigInProgress is true, HandleEvent should ignore incoming packets.
        /// Expected: raised == 0 and LatestData remains null.
        /// </summary>
        [Fact]
        public void Win_HandleEvent_Ignores_Packets_During_Reconfig()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK.IMU.ShimmerSDK_IMU), "HandleEvent");
            if (mi == null) return;

            var sut = new ShimmerSDK.IMU.ShimmerSDK_IMU();

            SetField(sut, "_reconfigInProgress", true);

            int raised = 0;
            sut.SampleReceived += (_, __) => raised++;

            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET,
                MakeFullImuCluster());

            mi.Invoke(sut, new object?[] { null, ev });

            Assert.Equal(0, raised);
            Assert.Null(sut.LatestData);
        }


        // ----- ConfigureAndroid behavior -----


        /// <summary>
        /// MAC validation on Android configuration.
        /// Expected: invalid MACs throw <see cref="ArgumentException"/> (wrapped in <see cref="TargetInvocationException"/>).
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("ZZ:ZZ:ZZ:ZZ:ZZ:ZZ")]
        public void ConfigureAndroid_Throws_On_Invalid_Mac(string mac)
        {
            var sut = new ShimmerSDK_IMU();
            var mi = sut.GetType().GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return;

            var args = new object[]
            {
                "DevA", mac,
                true, true, 
                true, true, 
                true, true, 
                true, false, true 
            };

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, args));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }


        /// <summary>
        /// Bitmap build + index reset on Android configure.
        /// Expected: <c>shimmerAndroid</c> not null, bitmap has EXTs/IMU flags, and all indices reset to −1 with <c>firstDataPacketAndroid</c>=true.
        /// </summary>
        [Fact]
        public void ConfigureAndroid_Builds_SensorBitmap_And_Resets_Indices()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = sut.GetType().GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return;

            mi.Invoke(sut, new object[]
            {
                "Dev1", "00:11:22:33:44:55",
                true,  true,
                true,  true,
                true,  true,
                true,  false,  true
            });

            // driver present
            var fDriver = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fDriver);
            Assert.NotNull(fDriver!.GetValue(sut));

            // indices reset
            string[] indexFields =
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

            // first packet flag
            Assert.True((bool)sut.GetType().GetField("firstDataPacketAndroid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sut)!);
        }


        // ----- HandleEventAndroid: DATA_PACKET -----


        /// <summary>
        /// Rationale: first DATA_PACKET should map CAL indices and populate LatestData;
        /// a second DATA_PACKET should still raise and keep LatestData populated.
        /// Expected: LatestData != null; GyroscopeZ and MagnetometerZ have the expected values.
        /// </summary>
        [Fact]
        public void Android_HandleEventAndroid_DataPacket_Populates_LatestData()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "HandleEventAndroid");
            if (mi == null) return;

            SetField(sut, "firstDataPacketAndroid", true);

            var oc = MakeFullImuCluster();
            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc);

            mi.Invoke(sut, new object?[] { null, ev });
            Assert.NotNull(sut.LatestData);

            static double D(object? v) => v is SensorData sd ? sd.Data : Convert.ToDouble(v);

            var data = sut.LatestData!;
            var gyZ = data.GetType().GetField("GyroscopeZ")?.GetValue(data)
                      ?? data.GetType().GetProperty("GyroscopeZ")?.GetValue(data);
            var magZ = data.GetType().GetField("MagnetometerZ")?.GetValue(data)
                       ?? data.GetType().GetProperty("MagnetometerZ")?.GetValue(data);

            Assert.Equal(9, D(gyZ));
            Assert.Equal(12, D(magZ));

            mi.Invoke(sut, new object?[] { null, ev });
            Assert.NotNull(sut.LatestData);
        }


        // ----- HandleEventAndroid: STATE_CHANGE -----


        /// <summary>
        /// Rationale: state-change events should flip flags and complete pending TCS waits.
        /// Expected: CONNECTED completes _androidConnectedTcs; STREAMING completes _androidStreamingAckTcs
        /// and sets _androidIsStreaming=true and firstDataPacketAndroid=true.
        /// </summary>
        [Fact]
        public async Task Android_HandleEventAndroid_StateChange_Completes_Tasks_And_Flags()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "HandleEventAndroid");
            if (mi == null) return;

            var tcsConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsStreaming = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetField(sut, "_androidConnectedTcs", tcsConnected);
            SetField(sut, "_androidStreamingAckTcs", tcsStreaming);

            // CONNECTED
            var evConn = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_CONNECTED);
            mi.Invoke(sut, new object?[] { null, evConn });
            Assert.True(await Task.WhenAny(tcsConnected.Task, Task.Delay(200)) == tcsConnected.Task);

            // STREAMING
            var evStr = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_STREAMING);
            mi.Invoke(sut, new object?[] { null, evStr });
            Assert.True(await Task.WhenAny(tcsStreaming.Task, Task.Delay(200)) == tcsStreaming.Task);

            Assert.True((bool)(GetField(sut, "_androidIsStreaming") ?? false));
            Assert.True((bool)(GetField(sut, "firstDataPacketAndroid") ?? false));
        }


        // ----- HandleEventAndroid: NON-DATA is ignored -----


        /// <summary>
        /// Rationale: notification/PRR events are noise for LatestData.
        /// Expected: LatestData stays null after NON-DATA events.
        /// </summary>
        [Fact]
        public void Android_HandleEventAndroid_NonData_Leaves_LatestData_Null()
        {
            var sut = new ShimmerSDK_IMU();
            var mi = GetPrivMethod(typeof(ShimmerSDK_IMU), "HandleEventAndroid");
            if (mi == null) return;

            Assert.Null(sut.LatestData);

            var evNotif = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_NOTIFICATION_MESSAGE, "msg");
            var evPrr = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_PACKET_RECEPTION_RATE, 100);

            mi.Invoke(sut, new object?[] { null, evNotif });
            mi.Invoke(sut, new object?[] { null, evPrr });

            Assert.Null(sut.LatestData);
        }
    }
}
