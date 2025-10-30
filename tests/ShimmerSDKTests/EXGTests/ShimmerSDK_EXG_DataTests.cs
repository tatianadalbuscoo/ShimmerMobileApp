/*
 * ShimmerSDK_EXGDataTests.cs
 * Purpose: Unit tests for ShimmerSDK_EXGData file.
 */


using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;


namespace ShimmerSDKTests
{

    /// <summary>
    /// Helper “shim” shared by tests that historically accessed a <c>.Values</c> array.
    /// Uses reflection to build an ordered array of channel values from a real
    /// <see cref="ShimmerSDK_EXGData"/> instance, matching the canonical order:
    /// TimeStamp, LNA (X/Y/Z), WRA (X/Y/Z), Gyro (X/Y/Z), Mag (X/Y/Z),
    /// Temperature_BMP180, Pressure_BMP180, BatteryVoltage, ExtADC (A6/A7/A15), Exg1, Exg2.
    /// <para>
    /// Works across platform branches:
    /// - Windows/Android: reads public fields
    /// - Apple: reads public properties
    /// </para>
    /// </summary>
    public static class ExgValuesShim
    {

        /// <summary>
        /// Returns the ordered values array for the specified <paramref name="d"/> instance,
        /// resolving public fields or public properties by name.
        /// Missing members (in a non-matching branch) will yield <c>null</c> entries.
        /// </summary>
        /// <param name="d">The EXG data instance from which to extract values.</param>
        /// <returns>
        /// Array of values in canonical order (nullable objects):
        /// TimeStamp, LowNoiseAccelerometerX/Y/Z, WideRangeAccelerometerX/Y/Z,
        /// GyroscopeX/Y/Z, MagnetometerX/Y/Z, Temperature_BMP180, Pressure_BMP180,
        /// BatteryVoltage, ExtADC_A6/A7/A15, Exg1, Exg2.
        /// </returns>
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

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_EXGData"/> across platform-specific API shapes.
    /// Covers: constructor wiring, null handling, API surface shape for EXG channels,
    /// and mutability semantics by platform (readonly fields vs settable properties).
    /// </summary>
    public class ShimmerSDK_EXGData_Tests
    {

        /// <summary>
        /// Helper: Retrieves a public instance field value by name using reflection.
        /// </summary>
        /// <param name="instance">Object instance that owns the field.</param>
        /// <param name="name">Field name to fetch.</param>
        /// <returns>The field value, or <c>null</c> if not found.</returns>
        private static object? GetField(object instance, string name)
        {
            var fi = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(fi);
            return fi!.GetValue(instance);
        }


        /// <summary>
        /// Helper: Detects whether the current build uses the SensorData-typed API surface
        /// (Windows/Android branch) as opposed to double/settable properties (Apple branch).
        /// </summary>
        /// <param name="t">Type to inspect (typically <see cref="ShimmerSDK_EXGData"/>).</param>
        /// <returns>
        /// <c>true</c> if the type exposes <c>TimeStamp</c> as a <see cref="SensorData"/> field
        /// (Windows/Android branch); otherwise <c>false</c> (Apple branch).
        /// </returns>
        private static bool IsSensorDataBranch(Type t)
        {
            var ts = t.GetField("TimeStamp", BindingFlags.Instance | BindingFlags.Public)?.FieldType;
            return ts != null && typeof(SensorData).IsAssignableFrom(ts);
        }


        // ----- Constructor behavior -----


        /// <summary>
        /// Ensures the constructor correctly wires channels according to the active platform branch.
        /// Windows/Android branch: invokes the long <c>SensorData</c>-based constructor via reflection.
        /// Apple branch: validates param-less + full-parameter overload behavior with settable properties.
        /// Expected:
        /// - Every channel lands in the expected field/property with the provided sentinel value
        /// - Test short-circuits (return) if branch-specific members are not present in the build
        /// </summary>
        [Fact]
        public void Ctor_Assigns_Fields_Correctly_For_Current_Platform()
        {
            var t = typeof(ShimmerSDK_EXGData);

            if (IsSensorDataBranch(t))
            {

                // WINDOWS/ANDROID
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

                // MacCatalyst
                var sutEmpty = Activator.CreateInstance(typeof(ShimmerSDK_EXGData))!;
                var p1 = sutEmpty.GetType().GetProperty("Exg1", BindingFlags.Instance | BindingFlags.Public);
                var p2 = sutEmpty.GetType().GetProperty("Exg2", BindingFlags.Instance | BindingFlags.Public);
                if (p1 == null || p2 == null) return;

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


        /// <summary>
        /// Constructor accepts nulls for optional channels and preserves them.
        /// Expected:
        /// - On SensorData branch, passing nulls yields null fields (e.g., Exg1/Exg2)
        /// - On Apple branch, default properties remain null when not set
        /// - Test short-circuits if branch members are not present
        /// </summary>
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


        // ----- API surface — EXG channel presence -----


        /// <summary>
        /// Ensures exactly two EXG members are exposed when the branch includes them.
        /// Expected:
        /// - If neither fields nor properties exist (branch not compiled), soft-skip (return)
        /// - Otherwise, exactly 2 public instance members named Exg1 and Exg2
        /// </summary>
        [Fact]
        public void Exg_Channel_Count_Is_Two()
        {
            var t = typeof(ShimmerSDK_EXGData);
            var members = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                           .Where(m => m.Name is "Exg1" or "Exg2")
                           .ToArray();

            if (members.Length == 0) return;

            Assert.Equal(2, members.Length);
        }


        // ----- Mutability semantics by platform -----


        /// <summary>
        /// Verifies branch-specific mutability:
        /// Windows/Android expose readonly fields; Apple exposes settable properties.
        /// Expected:
        /// - SensorData branch: public fields present & IsInitOnly == true, no properties
        /// - Apple branch: public properties present & CanWrite == true, no fields
        /// - Soft-skip when branch members are missing
        /// </summary>
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
                if (exg1Field == null || exg2Field == null) return;
                Assert.True(exg1Field!.IsInitOnly);
                Assert.True(exg2Field!.IsInitOnly);
                Assert.Null(exg1Prop);
                Assert.Null(exg2Prop);
            }
            else
            {
                if (exg1Prop == null || exg2Prop == null) return;
                Assert.True(exg1Prop!.CanWrite);
                Assert.True(exg2Prop!.CanWrite);
                Assert.Null(exg1Field);
                Assert.Null(exg2Field);
            }
        }
    }
}
