/*
 * ShimmerSDK_EXGTests.cs
 * Purpose: Unit tests for ShimmerSDK_EXG file.
 */


using System.Reflection;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;
using static ShimmerSDKTests.ExgValuesShim;


namespace ShimmerSDKTests.EXGTests
{

    /// <summary>
    /// Unit tests for <see cref="ShimmerSDK_EXG"/> core behaviors:
    /// enum shape, constructor defaults, sampling-rate quantization (sync/async),
    /// Windows/Android configuration helpers, event mapping, and private utilities.
    /// </summary>
    public class ShimmerSDK_EXGTests
    {

        /// <summary>
        /// Helper: reads a private instance field via reflection and returns it as the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="instance">Object instance that contains the field.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <returns>Typed value of the private field.</returns>
        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fi);
            var value = fi!.GetValue(instance);
            Assert.NotNull(value);
            return (T)value!;
        }


        // ----- ExgMode enum -----


        /// <summary>
        /// <see cref="ExgMode"/> contains exactly the four expected values.
        /// Expected: length is 4 and includes ECG, EMG, ExGTest, Respiration.
        /// </summary>
        [Fact]
        public void ExgMode_Has_All_Expected_Values()
        {
            var values = Enum.GetValues(typeof(ExgMode)).Cast<ExgMode>().ToArray();
            Assert.Equal(4, values.Length);
            Assert.Contains(ExgMode.ECG, values);
            Assert.Contains(ExgMode.EMG, values);
            Assert.Contains(ExgMode.ExGTest, values);
            Assert.Contains(ExgMode.Respiration, values);
        }


        /// <summary>
        /// Underlying integers are sequential starting at 0.
        /// Expected: ECG=0, EMG=1, ExGTest=2, Respiration=3.
        /// </summary>
        [Fact]
        public void ExgMode_Underlying_Int_Values_Are_Expected()
        {
            Assert.Equal(0, (int)ExgMode.ECG);
            Assert.Equal(1, (int)ExgMode.EMG);
            Assert.Equal(2, (int)ExgMode.ExGTest);
            Assert.Equal(3, (int)ExgMode.Respiration);
        }


        /// <summary>
        /// <c>ToString()</c> round-trips with <c>Enum.Parse</c> using default (case-sensitive) parsing.
        /// Expected: Enum.Parse(ToString()) equals the original value.
        /// </summary>
        [Fact]
        public void ExgMode_ToString_RoundTrip_Parse_CaseSensitive()
        {
            foreach (var v in Enum.GetValues(typeof(ExgMode)).Cast<ExgMode>())
            {
                var s = v.ToString();
                var parsed = (ExgMode)Enum.Parse(typeof(ExgMode), s);
                Assert.Equal(v, parsed);
            }
        }


        /// <summary>
        /// <c>Enum.TryParse</c> with <c>ignoreCase: true</c> accepts case-insensitive names.
        /// Expected: parsing succeeds and matches the expected enum.
        /// </summary>
        [Theory]
        [InlineData("ecg", ExgMode.ECG)]
        [InlineData("EMG", ExgMode.EMG)]
        [InlineData("ExGTest", ExgMode.ExGTest)]
        [InlineData("respiration", ExgMode.Respiration)]
        public void ExgMode_TryParse_Ignores_Case_When_Requested(string input, ExgMode expected)
        {
            var ok = Enum.TryParse<ExgMode>(input, ignoreCase: true, out var result);
            Assert.True(ok);
            Assert.Equal(expected, result);
        }


        /// <summary>
        /// <c>Enum.TryParse</c> with <c>ignoreCase: false</c> fails on incorrect casing.
        /// Expected: parsing returns false.
        /// </summary>
        [Theory]
        [InlineData("ecg")]
        [InlineData("emg")]
        [InlineData("exgtest")]
        [InlineData("RESPIRATION")]
        public void ExgMode_TryParse_Fails_When_CaseSensitive_And_Case_Mismatch(string input)
        {
            var ok = Enum.TryParse<ExgMode>(input, ignoreCase: false, out _);
            Assert.False(ok);
        }


        /// <summary>
        /// Unknown names do not parse, even with <c>ignoreCase: true</c>.
        /// Expected: parsing fails for invalid identifiers.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("EEG")]
        [InlineData("HEART")]
        public void ExgMode_TryParse_Fails_On_Unknown_Names(string input)
        {
            var ok = Enum.TryParse<ExgMode>(input, ignoreCase: true, out _);
            Assert.False(ok);
        }


        /// <summary>
        /// Numeric strings parse but may not be defined values.
        /// Expected: TryParse succeeds; Enum.IsDefined is false when out of range.
        /// </summary>
        [Theory]
        [InlineData("-1")]
        [InlineData("4")]
        [InlineData("123")]
        public void ExgMode_TryParse_Numeric_Parses_But_IsNotDefined(string input)
        {
            var ok = Enum.TryParse<ExgMode>(input, ignoreCase: true, out var result);
            Assert.True(ok);
            Assert.False(Enum.IsDefined(typeof(ExgMode), result));
        }


        /// <summary>
        /// Valid numeric strings map to defined enum values.
        /// Expected: parse succeeds and equals the expected value.
        /// </summary>
        [Theory]
        [InlineData("0", ExgMode.ECG)]
        [InlineData("1", ExgMode.EMG)]
        [InlineData("2", ExgMode.ExGTest)]
        [InlineData("3", ExgMode.Respiration)]
        public void ExgMode_TryParse_Numeric_Defined_Values(string input, ExgMode expected)
        {
            var ok = Enum.TryParse<ExgMode>(input, out var result);
            Assert.True(ok);
            Assert.True(Enum.IsDefined(typeof(ExgMode), result));
            Assert.Equal(expected, result);
        }


        /// <summary>
        /// Exhaustive switch covers all enum values.
        /// Expected: every value returns a non-empty mapping; default is never hit.
        /// </summary>
        [Fact]
        public void ExgMode_Exhaustive_Switch_Covers_All_Cases()
        {
            string Map(ExgMode m) => m switch
            {
                ExgMode.ECG => "ecg",
                ExgMode.EMG => "emg",
                ExgMode.ExGTest => "exgtest",
                ExgMode.Respiration => "resp",
                _ => throw new ArgumentOutOfRangeException(nameof(m), m, "Unexpected enum")
            };

            foreach (var v in Enum.GetValues(typeof(ExgMode)).Cast<ExgMode>())
            {
                var s = Map(v);
                Assert.False(string.IsNullOrWhiteSpace(s));
            }
        }


        // ----- Constructor behavior -----


        /// <summary>
        /// Constructor sets sensible defaults.
        /// Expected: <para>SamplingRate ≈ 51.2</para><para>_enableExg == false</para>
        /// </summary>
        [Fact]
        public void Ctor_Sets_Defaults()
        {
            var sut = new ShimmerSDK_EXG();
            Assert.Equal(51.2, sut.SamplingRate, precision: 1);
            var exgEnabled = GetPrivateField<bool>(sut, "_enableExg");
            Assert.False(exgEnabled);
        }


        /// <summary>
        /// Default EXG mode.
        /// Expected: _exgMode == ECG.
        /// </summary>
        [Fact]
        public void Ctor_Default_ExgMode_Is_ECG()
        {
            var sut = new ShimmerSDK_EXG();
            var mode = GetPrivateField<ExgMode>(sut, "_exgMode");
            Assert.Equal(ExgMode.ECG, mode);
        }


        // ----- SetFirmwareSamplingRateNearestAsync behavior -----


        /// <summary>
        /// Async mirrors sync for identical inputs.
        /// Expected: same quantized value for 51.2 Hz.
        /// </summary>
        [Fact]
        public async Task SetFirmwareSamplingRateNearestAsync_Mirrors_Sync()
        {
            var sut = new ShimmerSDK_EXG();
            double r1 = sut.SetFirmwareSamplingRateNearest(51.2);
            double r2 = await sut.SetFirmwareSamplingRateNearestAsync(51.2);
            Assert.Equal(r1, r2, precision: 8);
        }


        /// <summary>
        /// Two distinct inputs quantize to expected outputs.
        /// Expected: Applied(256) >= Applied(200) and ≈ 256 within ±0.1.
        /// </summary>
        [Fact]
        public async Task SetFirmwareSamplingRateNearestAsync_Quantizes_Two_Different_Inputs()
        {
            var sut = new ShimmerSDK_EXG();
            double a1 = await sut.SetFirmwareSamplingRateNearestAsync(200.0); // 32768/round(163.84)=32768/164≈199.8
            double a2 = await sut.SetFirmwareSamplingRateNearestAsync(256.0); // 32768/128=256
            Assert.True(a2 >= a1);
            Assert.InRange(a2, 255.9, 256.1);
        }


        // ----- SetFirmwareSamplingRateNearest behavior -----


        /// <summary>
        /// <para>Helper: reproduces the firmware quantization strategy:</para>
        /// <para><c>applied = clock / round(clock / requested, MidpointRounding.AwayFromZero)</c></para>
        /// <para>Where <c>clock</c> is the device base frequency (32,768 Hz) and the divider is clamped to at least 1.</para>
        /// </summary>
        /// <returns>The quantized sampling rate that the firmware would apply.</returns>
        private static double Quantize(double requested)
        {
            const double clock = 32768.0;
            int divider = Math.Max(1, (int)Math.Round(clock / requested, MidpointRounding.AwayFromZero));
            return clock / divider;
        }


        /// <summary>
        /// <c>SetFirmwareSamplingRateNearest</c> quantizes to clock/divider and updates <c>SamplingRate</c>.
        /// Expected: for 50 Hz request, applied and property are ≈ 50.03 Hz.
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_Quantizes_And_Updates_SR()
        {
            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(50.0);
            // 32768 / round(32768/50=655.36) = 32768/655 ≈ 50.02748
            Assert.InRange(applied, 50.02, 50.04);
            Assert.InRange(sut.SamplingRate, 50.02, 50.04);
        }


        /// <summary>
        /// Local monotonicity around a point.
        /// Expected: Applied(101) >= Applied(100).
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_Is_Locally_Monotonic()
        {
            var sut = new ShimmerSDK_EXG();
            var a1 = sut.SetFirmwareSamplingRateNearest(100.0);
            var a2 = sut.SetFirmwareSamplingRateNearest(101.0);
            Assert.True(a2 >= a1 - 1e-9);
        }


        /// <summary>
        /// Non-positive input handling.
        /// Expected: throws <see cref="ArgumentOutOfRangeException"/> for 0 or negative values.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void SetFirmwareSamplingRateNearest_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_EXG();
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.SetFirmwareSamplingRateNearest(requested));
        }


        /// <summary>
        /// Driver write integration (Windows branch only).
        /// Expected: Windows: driver receives the applied rate; cross-OS: property updated, driver stays 0.
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_Writes_To_Driver_When_Configured()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D", portOrId: "P",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            double applied = sut.SetFirmwareSamplingRateNearest(102.4);

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            bool windowsBranchPresent =
                sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic) != null ||
                sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic) != null;

            if (windowsBranchPresent)
            {
                Assert.Equal(applied, shimmer!.LastSamplingRateWritten, precision: 10);
            }
            else
            {
                Assert.Equal(0, shimmer!.LastSamplingRateWritten);
                Assert.InRange(sut.SamplingRate, applied - 1e-10, applied + 1e-10);
            }
        }


        /// <summary>
        /// Exact divisor scenario.
        /// Expected: 51.2 Hz remains 51.2 Hz (identity).
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_ExactDivisor_IsIdentity()
        {
            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(51.2);
            Assert.InRange(applied, 51.1999, 51.2001);
            Assert.InRange(sut.SamplingRate, 51.1999, 51.2001);
        }


        /// <summary>
        /// Quantized value matches the expected local quantizer.
        /// Expected: Applied == Quantize(requested); SamplingRate == Applied.
        /// </summary>
        [Theory]
        [InlineData(50.0)]
        [InlineData(75.0)]
        [InlineData(100.0)]
        [InlineData(123.456)]
        public void SetFirmwareSamplingRateNearest_MatchesExpectedQuantization(double requested)
        {
            var sut = new ShimmerSDK_EXG();
            double expected = Quantize(requested);
            double applied = sut.SetFirmwareSamplingRateNearest(requested);

            Assert.InRange(applied, expected - 1e-12, expected + 1e-12);
            Assert.InRange(sut.SamplingRate, expected - 1e-12, expected + 1e-12);
        }


        /// <summary>
        /// Idempotence for same input.
        /// Expected: calling twice with 200.0 yields the same applied rate.
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_IsIdempotent_ForSameInput()
        {
            var sut = new ShimmerSDK_EXG();
            var a1 = sut.SetFirmwareSamplingRateNearest(200.0);
            var a2 = sut.SetFirmwareSamplingRateNearest(200.0);
            Assert.InRange(a2, a1 - 1e-12, a1 + 1e-12);
        }


        /// <summary>
        /// Midpoint rounding behavior.
        /// Expected: with requested = clock/(N+0.5), applied = clock/(N+1).
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

            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(requested);

            Assert.InRange(applied, expected - 1e-12, expected + 1e-12);
        }


        /// <summary>
        /// Extreme high request clamps to device clock.
        /// Expected: applied ≈ 32768 Hz (divider = 1).
        /// </summary>
        [Fact]
        public void SetFirmwareSamplingRateNearest_ClampsToClock_ForVeryHighRequest()
        {
            const double clock = 32768.0;
            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(1e9);
            Assert.InRange(applied, clock - 1e-12, clock + 1e-12);
        }


        /// <summary>
        /// Extreme low request still yields a small positive rate.
        /// Expected: applied &gt; 0 and ≤ requested × 1.01.
        /// </summary>
        [Theory]
        [InlineData(0.1)]
        [InlineData(0.01)]
        public void SetFirmwareSamplingRateNearest_ProducesSmallPositive_ForVeryLowRequest(double requested)
        {
            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(requested);
            Assert.True(applied > 0);
            Assert.True(applied <= requested * 1.01);
        }


        // ----- TestConfigure/Configure* behavior -----


        /// <summary>
        /// Verifies that <c>TestConfigure</c> builds the correct sensor bitmap and attaches subscriptions.
        /// Expected: <c>EnabledSensors</c> equals the bitwise OR of all selected sensors (incl. EXG1/EXG2).
        /// </summary>
        [Fact]
        public void Configure_Builds_SensorBitmap_And_Subscribes()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D1",
                portOrId: "PORT",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: true,
                enableGyro: true,
                enableMag: true,
                enablePressureTemp: true,
                enableBatteryVoltage: true,
                enableExtA6: true,
                enableExtA7: false,
                enableExtA15: true,
                enableExg: true,
                exgMode: ExgMode.EMG
            );

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            int expected =
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6 |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15 |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;

            Assert.Equal(expected, shimmer!.EnabledSensors);
        }


        /// <summary>
        /// Verifies that when EXG is disabled, EXG bits are not set in the bitmap.
        /// Expected: <c>EnabledSensors</c> & EXG mask == 0.
        /// </summary>
        [Fact]
        public void Configure_Without_EXG_Does_Not_Set_EXG_Bits()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D2",
                portOrId: "PORT",
                enableLowNoiseAcc: false,
                enableWideRangeAcc: true,
                enableGyro: false,
                enableMag: true,
                enablePressureTemp: false,
                enableBatteryVoltage: true,
                enableExtA6: false,
                enableExtA7: true,
                enableExtA15: false,
                enableExg: false,
                exgMode: ExgMode.ECG
            );

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            int exgMask =
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT |
                (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;

            Assert.Equal(0, shimmer!.EnabledSensors & exgMask);
        }


        // ----- ConfigureWindows behavior -----


        /// <summary>
        /// Invokes <c>ConfigureWindows</c> when present and verifies driver object and event subscription exist.
        /// Expected: private <c>shimmer</c> is initialized and exposes <c>UICallback</c> event.
        /// </summary>
        [Fact]
        public void ConfigureWindows_IfPresent_BuildsDriverAndSubscribes()
        {
            var sut = new ShimmerSDK_EXG();

            var m = sut.GetType().GetMethod(
                "ConfigureWindows",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (m == null)
            {
                return;
            }

            m.Invoke(sut, new object[] {
                "D1","COM1",
                true,true,           // LowNoiseAcc, WideRangeAcc
                true,true,           // Gyro, Mag
                true,true,           // PressureTemp, Battery
                true,false,true,     // ExtA6, ExtA7, ExtA15
                true,                // EXG on
                ExgMode.EMG
            });

            var shimmerFi = sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic);
            var shimmer = shimmerFi?.GetValue(sut);
            Assert.NotNull(shimmer);

            var evt = shimmer!.GetType().GetEvent("UICallback");
            Assert.NotNull(evt);
        }


        // ----- GetSafe() behavior -----


        /// <summary>
        /// Validates <c>GetSafe</c> returns <c>null</c> for negative indices.
        /// Expected: result is <c>null</c> for index &lt; 0.
        /// </summary>
        [Fact]
        public void GetSafe_NegativeIndex_ReturnsNull()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("GetSafe",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("ANY", ShimmerConfiguration.SignalFormats.CAL, 42);

            var result = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(result);
        }


        /// <summary>
        /// Validates <c>GetSafe</c> returns <c>null</c> for out-of-range indices.
        /// Expected: result is <c>null</c> for index >= Count.
        /// </summary>
        [Fact]
        public void GetSafe_OutOfRangeIndex_ReturnsNull()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("GetSafe",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("X", ShimmerConfiguration.SignalFormats.CAL, 1);

            var result = mi.Invoke(null, new object[] { oc, 999 });
            Assert.Null(result);
        }


        /// <summary>
        /// Validates <c>GetSafe</c> returns the expected <c>SensorData</c> for a valid index.
        /// Expected: retrieved data matches the value inserted in the <c>ObjectCluster</c>.
        /// </summary>
        [Fact]
        public void GetSafe_ValidIndex_ReturnsData()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("GetSafe",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("A", ShimmerConfiguration.SignalFormats.CAL, 12.34);
            int idx = oc.GetIndex("A", ShimmerConfiguration.SignalFormats.CAL);

            var result = mi.Invoke(null, new object[] { oc, idx });
            Assert.NotNull(result);
            var sd = Assert.IsType<SensorData>(result);
            Assert.Equal(12.34, sd.Data, precision: 6);
        }


        // ----- FindSignal behavior -----


        /// <summary>
        /// Verifies that CAL is preferred over RAW when both exist.
        /// Expected: returned tuple refers to CAL entry for the given signal name.
        /// </summary>
        [Fact]
        public void FindSignal_Prefers_CAL_Over_RAW()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("FOO", "RAW", 1);
            oc.Add("FOO", ShimmerConfiguration.SignalFormats.CAL, 2);

            var res = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { oc, new[] { "FOO" } })!;
            Assert.Equal(ShimmerConfiguration.SignalFormats.CAL, res.fmt);
            Assert.Equal(oc.GetIndex("FOO", ShimmerConfiguration.SignalFormats.CAL), res.idx);
        }


        /// <summary>
        /// Verifies fallback order when CAL is missing: RAW, then UNCAL.
        /// Expected: RAW is used if present; otherwise UNCAL.
        /// </summary>
        [Fact]
        public void FindSignal_FallsBack_RAW_then_UNCAL()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var ocRaw = new ObjectCluster();
            ocRaw.Add("BAR", "RAW", 10);
            var resRaw = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { ocRaw, new[] { "BAR" } })!;
            Assert.Equal("RAW", resRaw.fmt);
            Assert.Equal(ocRaw.GetIndex("BAR", "RAW"), resRaw.idx);

            var ocUncal = new ObjectCluster();
            ocUncal.Add("BAZ", "UNCAL", 20);
            var resUncal = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { ocUncal, new[] { "BAZ" } })!;
            Assert.Equal("UNCAL", resUncal.fmt);
            Assert.Equal(ocUncal.GetIndex("BAZ", "UNCAL"), resUncal.idx);
        }


        /// <summary>
        /// Verifies format-agnostic fallback when CAL/RAW/UNCAL are not found.
        /// Expected: returns a match without format if present; otherwise not found.
        /// </summary>
        [Fact]
        public void FindSignal_FallsBack_FormatAgnostic()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("QUX", null, 33);

            var res = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { oc, new[] { "QUX" } })!;
            Assert.Null(res.fmt);
            Assert.Equal(oc.GetIndex("QUX", null), res.idx);
        }


        /// <summary>
        /// Verifies not-found behavior.
        /// Expected: returns (-1, null, null) for missing names.
        /// </summary>
        [Fact]
        public void FindSignal_NotFound_ReturnsMinusOneAndNulls()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("OTHER", ShimmerConfiguration.SignalFormats.CAL, 1);

            var res = ((int idx, string? name, string? fmt))mi.Invoke(null, new object[] { oc, new[] { "MISSING" } })!;
            Assert.Equal(-1, res.idx);
            Assert.Null(res.name);
            Assert.Null(res.fmt);
        }


        // ----- HandleEvent behavior -----


        /// <summary>
        /// Verifies Windows event handler maps indices, raises <c>SampleReceived</c>, and fills EXG channels when enabled.
        /// Expected: after first packet mapping, a subsequent packet triggers event with non-null EXG values.
        /// </summary>
        [Fact]
        public void HandleEvent_Maps_Indices_And_Raises_Sample()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D1", portOrId: "PORT",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: true, enableMag: true,
                enablePressureTemp: true, enableBatteryVoltage: true,
                enableExtA6: true, enableExtA7: true, enableExtA15: true,
                enableExg: true, exgMode: ExgMode.ECG);

            var handleEvent = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handleEvent == null) return;

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK_EXGData)d;

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
            oc.Add("EXG1_CH1", ShimmerConfiguration.SignalFormats.CAL, 1001);
            oc.Add("EXG2_CH1", ShimmerConfiguration.SignalFormats.CAL, 1002);

            shimmer!.RaiseDataPacket(oc);

            Assert.NotNull(received);
            Assert.True(Vals(received!).Length >= 21);
            Assert.NotNull(Vals(received!)[^2]);
            Assert.NotNull(Vals(received!)[^1]);
        }


        /// <summary>
        /// Verifies that when EXG is disabled, EXG channels remain <c>null</c> even if names exist in the payload.
        /// Expected: EXG CH1/CH2 are <c>null</c>.
        /// </summary>
        [Fact]
        public void HandleEvent_With_Exg_Disabled_Leaves_Exg_Nulls()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D1", portOrId: "PORT",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: true, enableMag: true,
                enablePressureTemp: true, enableBatteryVoltage: true,
                enableExtA6: true, enableExtA7: true, enableExtA15: true,
                enableExg: false, exgMode: ExgMode.ECG);

            var handleEvent = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handleEvent == null) return;

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK_EXGData)d;

            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 123);
            oc.Add("EXG1_CH1", ShimmerConfiguration.SignalFormats.CAL, 1001);
            oc.Add("EXG2_CH1", ShimmerConfiguration.SignalFormats.CAL, 1002);

            shimmer!.RaiseDataPacket(oc);

            Assert.NotNull(received);
            Assert.Null(Vals(received!)[^2]); 
            Assert.Null(Vals(received!)[^1]);
        }


        /// <summary>
        /// Verifies missing EXG names produce <c>null</c> EXG channels even when EXG is enabled.
        /// Expected: EXG CH1/CH2 remain <c>null</c> if signals are absent in the cluster.
        /// </summary>
        [Fact]
        public void HandleEvent_Missing_Exg_Signals_Stays_Null()
        {
            var sut = new ShimmerSDK_EXG();

            sut.TestConfigure(
                deviceName: "D1", portOrId: "PORT",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: true, exgMode: ExgMode.EMG);

            var handleEvent = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handleEvent == null) return;

            var shimmer = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(shimmer);

            ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK_EXGData)d;

            var oc = new ObjectCluster();
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 777);

            shimmer!.RaiseDataPacket(oc);

            Assert.NotNull(received);
            Assert.True(Vals(received!).Length >= 2);
            Assert.Null(Vals(received!)[^2]);
            Assert.Null(Vals(received!)[^1]);
        }


        // ----- ConfigureAndroid behavior -----


        /// <summary>
        /// Validates Android configuration rejects invalid MAC addresses.
        /// Expected: <see cref="ArgumentException"/> (wrapped by <see cref="TargetInvocationException"/>) with message containing "Invalid MAC".
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("ZZ:ZZ:ZZ:ZZ:ZZ:ZZ")]
        public void ConfigureAndroid_Throws_On_Invalid_Mac(string mac)
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return;

            var args = new object[]
            {
                "Dev1", mac,
                true, true,         // LN-Acc, WR-Acc
                true, true,         // Gyro, Mag
                true, true,         // Press/Temp, VBatt
                true, false, true,  // ExtA6, ExtA7, ExtA15
                true, ExgMode.ECG
            };

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, args));
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Invalid MAC", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Validates Android configuration builds sensor bitmap (with EXG) and initializes the Android driver state.
        /// Expected: private <c>shimmerAndroid</c> initialized and <c>_androidEnabledSensors</c> equals expected bitmap.
        /// </summary>
        [Fact]
        public void ConfigureAndroid_Builds_SensorBitmap_And_Initializes_Driver()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return;

            var args = new object[]
            {
                "Dev1", "00:11:22:33:44:55",
                true,  true,
                true,  true,
                true,  true,
                true,  false,  true,
                true,  ExgMode.EMG
            };

            mi.Invoke(sut, args);

            var fShimmerAndroid = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fShimmerAndroid);
            var shimmerAndroid = fShimmerAndroid!.GetValue(sut);
            Assert.NotNull(shimmerAndroid);

            int expected =
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6 |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15 |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT |
                (int)ShimmerAPI.ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;

            var fEnabled = sut.GetType().GetField("_androidEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fEnabled);
            Assert.Equal(expected, (int)fEnabled!.GetValue(sut)!);
        }


        /// <summary>
        /// Validates Android configuration resets index mapping.
        /// Expected: <c>firstDataPacketAndroid</c> = true and all index fields set to -1.
        /// </summary>
        [Fact]
        public void ConfigureAndroid_Resets_Index_Mapping()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return;

            mi.Invoke(sut, new object[]
            {
                "DevX", "01:23:45:67:89:AB",
                false, true,
                false, true,
                false, true,
                false, false, false,
                false, ExgMode.ECG
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


        // ----- Reflection helpers -----


        /// <summary>
        /// Reflection helper: gets the value of a private instance field.
        /// </summary>
        /// <param name="o">Target instance.</param>
        /// <param name="name">Private field name.</param>
        /// <returns>The field value, or <c>null</c> if the field does not exist.</returns>
        private static object? GetField(object o, string name)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(o);
        }


        /// <summary>
        /// Reflection helper: sets a private instance field.
        /// </summary>
        /// <param name="o">Target instance.</param>
        /// <param name="name">Private field name.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetField(object o, string name, object? value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f);
            f!.SetValue(o, value);
        }


        /// <summary>
        /// Reflection helper: retrieves a private method by name.
        /// </summary>
        /// <param name="t">Type that declares the method.</param>
        /// <param name="name">Method name.</param>
        /// <param name="isStatic">Whether the method is static.</param>
        /// <returns>The matching <see cref="MethodInfo"/> or <c>null</c>.</returns>
        private static MethodInfo? GetPrivMethod(Type t, string name, bool isStatic = false)
        {
            var flags = BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            return t.GetMethod(name, flags);
        }


        // ----- GetSafeA behavior -----


        /// <summary>
        /// Android helper <c>GetSafeA</c>: returns a value for valid indices, <c>null</c> otherwise.
        /// Expected: non-null for index 0; <c>null</c> for -1 and out-of-range indices.
        /// </summary>
        [Fact]
        public void Android_GetSafeA_Returns_Data_Else_Null()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "GetSafeA", isStatic: true);
            if (mi == null) return;

            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add("A", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1.23);
            oc.Add("B", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 4.56);

            var d1 = mi.Invoke(null, new object[] { oc, 0 });
            Assert.NotNull(d1);
            var d2 = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(d2);
            var d3 = mi.Invoke(null, new object[] { oc, 99 });
            Assert.Null(d3);
        }


        // ----- FindSignalA -----


        /// <summary>
        /// Android helper <c>FindSignalA</c>: preference order is CAL &gt; RAW &gt; UNCAL &gt; format-agnostic; not found returns (-1, null, null).
        /// Expected: each queried case yields a tuple consistent with the priority and available signals.
        /// </summary>
        [Fact]
        public void Android_FindSignalA_Prefers_CAL_Then_RAW_Then_UNCAL_Then_Default()
        {
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "FindSignalA", isStatic: true);
            if (mi == null) return;

            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add("X", "RAW", 10);
            oc.Add("Y", "UNCAL", 20);
            oc.Add("Z", null, 30);
            oc.Add("W", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 40);

            var (idx1, name1, fmt1) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "W", "X", "Y", "Z" } })!;
            Assert.True(idx1 >= 0);
            Assert.Equal("W", name1, ignoreCase: true);
            Assert.Equal(ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, fmt1);

            var (idx2, name2, fmt2) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "X" } })!;
            Assert.True(idx2 >= 0);
            Assert.Equal("X", name2, ignoreCase: true);
            Assert.Equal("RAW", fmt2);

            var (idx3, _, fmt3) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "Y" } })!;
            Assert.True(idx3 >= 0);
            Assert.Equal("UNCAL", fmt3);

            var (idx4, _, fmt4) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "Z" } })!;
            Assert.True(idx4 >= 0);
            Assert.Null(fmt4);

            var (idx5, name5, fmt5) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "NOPE" } })!;
            Assert.Equal(-1, idx5);
            Assert.Null(name5);
            Assert.Null(fmt5);
        }


        // ----- HandleEventAndroid: DATA_PACKET -----


        /// <summary>
        /// Android event handling (DATA_PACKET): first packet builds index mapping; subsequent packets raise <c>SampleReceived</c> with EXG filled when enabled.
        /// Expected: after initial mapping, EXG CH1/CH2 are non-null in received samples; second invocation still raises an event.
        /// </summary>
        [Fact]
        public void Android_HandleEventAndroid_DataPacket_Maps_And_Raises_Sample()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "HandleEventAndroid");
            if (mi == null) return;

            SetField(sut, "_enableExg", true);
            SetField(sut, "firstDataPacketAndroid", true);

            ShimmerSDK.EXG.ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK.EXG.ShimmerSDK_EXGData)d;

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
            oc.Add("EXG1_CH1", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1001);
            oc.Add("EXG2_CH1", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1002);

            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc);
            mi.Invoke(sut, new object?[] { null, ev });

            Assert.NotNull(received);
            Assert.True(Vals(received!).Length >= 21);
            Assert.NotNull(Vals(received!)[^2]);
            Assert.NotNull(Vals(received!)[^1]);

            received = null;
            mi.Invoke(sut, new object?[] { null, ev });
            Assert.NotNull(received);
        }


        // ----- HandleEventAndroid: STATE_CHANGE -----


        /// <summary>
        /// Android state-change handling: updates flags and completes pending tasks for CONNECTED and STREAMING.
        /// Expected: CONNECTED completes <c>_androidConnectedTcs</c>; STREAMING completes <c>_androidStreamingAckTcs</c>; flags <c>_androidIsStreaming</c> and <c>firstDataPacketAndroid</c> become <c>true</c>.
        /// </summary>
        [Fact]
        public async Task Android_HandleEventAndroid_StateChange_SetsFlags_And_Completes_Tasks()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "HandleEventAndroid");
            if (mi == null) return;

            var tcsConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsStreaming = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetField(sut, "_androidConnectedTcs", tcsConnected);
            SetField(sut, "_androidStreamingAckTcs", tcsStreaming);

            var evConn = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_CONNECTED);
            mi.Invoke(sut, new object?[] { null, evConn });
            Assert.True(await Task.WhenAny(tcsConnected.Task, Task.Delay(200)) == tcsConnected.Task);

            var evStr = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_STREAMING);
            mi.Invoke(sut, new object?[] { null, evStr });
            Assert.True(await Task.WhenAny(tcsStreaming.Task, Task.Delay(200)) == tcsStreaming.Task);

            Assert.True((bool)GetField(sut, "_androidIsStreaming")!);
            Assert.True((bool)GetField(sut, "firstDataPacketAndroid")!);
        }
    }
}
