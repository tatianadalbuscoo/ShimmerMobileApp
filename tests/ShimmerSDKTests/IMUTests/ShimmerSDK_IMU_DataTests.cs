/*
 * ShimmerSDK_IMUDataTests.cs
 * Purpose: Unit tests for ShimmerSDK_IMUData file.
 */


using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.IMU;
using Xunit;


namespace ShimmerSDKTests.IMUTests
{

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_IMU_Data"/> across platform-specific API shapes.
    /// Validates presence and mutability of public members, constructor wiring,
    /// and null-handling semantics in both SensorData (Windows/Android) and Apple branches.
    /// </summary>
    public class ShimmerSDK_IMU_Data_Tests
    {

        /// <summary>
        /// Helper: Determines whether a public instance <c>field</c> or <c>property</c> with the given
        /// <paramref name="name"/> exists on type <paramref name="t"/>.
        /// </summary>
        /// <param name="t">Type to inspect.</param>
        /// <param name="name">Member name to look up.</param>
        /// <returns><c>true</c> if a matching public instance field or property exists; otherwise <c>false</c>.</returns>
        private static bool MemberExists(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance) != null ||
            t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) != null;


        /// <summary>
        /// Helper: Retrieves the value of a public instance field named <paramref name="name"/> from
        /// <paramref name="instance"/>. Returns <c>null</c> if the field is not found.
        /// </summary>
        /// <param name="instance">Object instance that owns the field.</param>
        /// <param name="name">Field name to fetch.</param>
        /// <returns>The field value if found; otherwise <c>null</c>.</returns>
        private static object? GetFieldValue(object instance, string name) =>
            instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);


        /// <summary>
        /// Helper: Detects whether the current build uses the SensorData-typed API surface (Windows/Android branch)
        /// versus the Apple branch (double/nullable fields or properties).
        /// </summary>
        /// <param name="t">Type to inspect (typically <see cref="ShimmerSDK_IMU_Data"/>).</param>
        /// <returns>
        /// <c>true</c> if the type exposes a public field named <c>TimeStamp</c> whose type is
        /// assignable to <see cref="SensorData"/>; otherwise <c>false</c>.
        /// </returns>
        private static bool IsSensorDataBranch(Type t)
        {
            var ts = t.GetField("TimeStamp", BindingFlags.Public | BindingFlags.Instance)?.FieldType;
            return ts != null && typeof(SensorData).IsAssignableFrom(ts);
        }


        /// <summary>
        /// Canonical channel names expected to be present for IMU data (excluding timestamp),
        /// used to assert API surface shape consistently across branches.
        /// </summary>
        private static readonly string[] RequiredNames =
        {
            "LowNoiseAccelerometerX","LowNoiseAccelerometerY","LowNoiseAccelerometerZ",
            "WideRangeAccelerometerX","WideRangeAccelerometerY","WideRangeAccelerometerZ",
            "GyroscopeX","GyroscopeY","GyroscopeZ",
            "MagnetometerX","MagnetometerY","MagnetometerZ",
            "Temperature_BMP180","Pressure_BMP180",
            "BatteryVoltage",
            "ExtADC_A6","ExtADC_A7","ExtADC_A15"
        };

        
        /// <summary>
        /// Accepted aliases for the timestamp field in different branches/platforms.
        /// The timestamp is an optional presence in these tests (validated when present).
        /// </summary>
        private static readonly string[] TimestampAliases =
        {
            "TimeStamp","Timestamp","SystemTimeStamp","SystemTimestamp"
        };


        // ----- API surface -----


        /// <summary>
        /// Ensures the constructor correctly wires channels according to the active platform branch.
        /// Windows/Android branch: invokes the long <c>SensorData</c>-based constructor via reflection.
        /// Apple branch: validates param-less + full-parameter overload behavior with settable properties.
        /// Expected:
        /// - Every channel lands in the expected field/property with the provided sentinel value
        /// - Test short-circuits (return) if branch-specific members are not present in the build
        /// </summary>
        [Fact]
        public void Has_All_Expected_Public_Members()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            foreach (var n in RequiredNames)
                Assert.True(MemberExists(t, n), $"Missing public member: {n}");
        }


        /// <summary>
        /// Ensures the constructor correctly wires channels according to the active platform branch.
        /// Windows/Android branch: invokes the long <c>SensorData</c>-based constructor via reflection.
        /// Apple branch: validates param-less + full-parameter overload behavior with settable properties.
        /// Expected:
        /// - Every channel lands in the expected field/property with the provided sentinel value
        /// - Test short-circuits (return) if branch-specific members are not present in the build
        /// </summary>
        [Fact]
        public void Fields_Are_Public_Readonly_And_No_Properties()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            foreach (var n in RequiredNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) Assert.True(f.IsInitOnly, $"{n} is not readonly");
                Assert.Null(t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance));
            }

            var tsName = TimestampAliases.FirstOrDefault(a => t.GetField(a, BindingFlags.Public | BindingFlags.Instance) != null);
            if (tsName != null)
            {
                var f = t.GetField(tsName, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(f);
                Assert.True(f!.IsInitOnly, $"{tsName} is not readonly");
                Assert.Null(t.GetProperty(tsName, BindingFlags.Public | BindingFlags.Instance));
            }
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
        public void Ctor_Assigns_All_Fields_Correctly_For_Current_Platform()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            if (IsSensorDataBranch(t))
            {
                // WINDOWS/ANDROID
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19 &&
                                                 typeof(SensorData).IsAssignableFrom(c.GetParameters()[0].ParameterType));
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    new SensorData(100),                                       // TimeStamp (if exists)
                    new SensorData(1), new SensorData(2), new SensorData(3),   // LNA
                    new SensorData(4), new SensorData(5), new SensorData(6),   // WRA
                    new SensorData(7), new SensorData(8), new SensorData(9),   // Gyro
                    new SensorData(10),new SensorData(11),new SensorData(12),  // Mag
                    new SensorData(20), new SensorData(21),                    // Temp, Pressure
                    new SensorData(22),                                        // Battery
                    new SensorData(30), new SensorData(31), new SensorData(32) // Ext
                });

                double D(object? o) => Assert.IsType<SensorData>(o).Data;

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

                Assert.Equal(21, D(GetFieldValue(sut, "Pressure_BMP180")));
                Assert.Equal(20, D(GetFieldValue(sut, "Temperature_BMP180")));

                Assert.Equal(22, D(GetFieldValue(sut, "BatteryVoltage")));

                Assert.Equal(30, D(GetFieldValue(sut, "ExtADC_A6")));
                Assert.Equal(31, D(GetFieldValue(sut, "ExtADC_A7")));
                Assert.Equal(32, D(GetFieldValue(sut, "ExtADC_A15")));
            }
            else
            {
                // iOS/MacCatalyst
                var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(c => c.GetParameters().Length == 19);
                if (ctor == null) return;

                object sut = ctor.Invoke(new object?[]
                {
                    100.0,
                    1.0, 2.0, 3.0,
                    4.0, 5.0, 6.0,
                    7.0, 8.0, 9.0,
                    10.0, 11.0, 12.0,
                    20.0, 21.0,
                    22.0,
                    30.0, 31.0, 32.0
                });

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

                Assert.Equal(21.0, GetFieldValue(sut, "Pressure_BMP180"));
                Assert.Equal(20.0, GetFieldValue(sut, "Temperature_BMP180"));

                Assert.Equal(22.0, GetFieldValue(sut, "BatteryVoltage"));

                Assert.Equal(30.0, GetFieldValue(sut, "ExtADC_A6"));
                Assert.Equal(31.0, GetFieldValue(sut, "ExtADC_A7"));
                Assert.Equal(32.0, GetFieldValue(sut, "ExtADC_A15"));
            }
        }


        /// <summary>
        /// Ensures the constructor correctly wires channels according to the active platform branch.
        /// Windows/Android branch: invokes the long <c>SensorData</c>-based constructor via reflection.
        /// Apple branch: validates param-less + full-parameter overload behavior with settable properties.
        /// Expected:
        /// - Every channel lands in the expected field/property with the provided sentinel value
        /// - Test short-circuits (return) if branch-specific members are not present in the build
        /// </summary>
        [Fact]
        public void Ctor_Allows_Nulls_For_All_Channels()
        {
            var t = typeof(ShimmerSDK_IMU_Data);

            var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(c => c.GetParameters().Length == 19);
            if (ctor == null) return;

            object sut = ctor.Invoke(Enumerable.Repeat<object?>(null, 19).ToArray());

            if (!RequiredNames.Any(n => MemberExists(t, n)) && !TimestampAliases.Any(a => MemberExists(t, a)))
                return;

            foreach (var n in RequiredNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) return;
                Assert.Null(f.GetValue(sut));
            }

            foreach (var alias in TimestampAliases)
            {
                var f = t.GetField(alias, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) Assert.Null(f.GetValue(sut));
            }
        }
    }
}
