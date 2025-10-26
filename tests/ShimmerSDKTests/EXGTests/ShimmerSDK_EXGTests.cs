using System;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;

namespace ShimmerSDKTests.EXGTests
{
    public class ShimmerSDK_EXGTests
    {
        // Helper riflessione: prende un campo privato e lo restituisce tipizzato
        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fi);
            var value = fi!.GetValue(instance);
            Assert.NotNull(value);
            return (T)value!;
        }

        // public enum ExgMode

        // ExgMode — behavior
        // Contiene esattamente i 4 valori attesi
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

        // ExgMode — behavior
        // I valori sottostanti sono sequenziali e partono da 0: ECG=0, EMG=1, ExGTest=2, Respiration=3
        [Fact]
        public void ExgMode_Underlying_Int_Values_Are_Expected()
        {
            Assert.Equal(0, (int)ExgMode.ECG);
            Assert.Equal(1, (int)ExgMode.EMG);
            Assert.Equal(2, (int)ExgMode.ExGTest);
            Assert.Equal(3, (int)ExgMode.Respiration);
        }

        // ExgMode — behavior
        // ToString() fa round-trip con Enum.Parse (case-sensitive di default)
        [Fact]
        public void ExgMode_ToString_RoundTrip_Parse_CaseSensitive()
        {
            foreach (var v in Enum.GetValues(typeof(ExgMode)).Cast<ExgMode>())
            {
                var s = v.ToString();
                var parsed = (ExgMode)Enum.Parse(typeof(ExgMode), s); // case-sensitive
                Assert.Equal(v, parsed);
            }
        }

        // ExgMode — behavior
        // TryParse(ignoreCase: true) accetta input case-insensitive
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

        // ExgMode — behavior
        // TryParse(ignoreCase: false) fallisce con case sbagliato
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

        // ExgMode — behavior
        // Nomi non validi NON vengono parsati (anche in ignoreCase)
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

        // ExgMode — behavior
        // Stringhe numeriche fanno parse ma NON sono valori definiti dell'enum
        [Theory]
        [InlineData("-1")]
        [InlineData("4")]
        [InlineData("123")]
        public void ExgMode_TryParse_Numeric_Parses_But_IsNotDefined(string input)
        {
            var ok = Enum.TryParse<ExgMode>(input, ignoreCase: true, out var result);
            Assert.True(ok); // parse riuscito perché è numerico...
            Assert.False(Enum.IsDefined(typeof(ExgMode), result)); // ...ma non è un valore definito (ECG=0..Respiration=3)
        }

        // ExgMode — behavior
        // Numerici validi (0..3) sono definiti
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


        // ExgMode — behavior
        // Switch completo su tutti i valori: nessun default colpito
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

        // Ctor() — behavior
        // Imposta default sensati: SR≈51.2, EXG off
        [Fact]
        public void Ctor_Sets_Defaults()
        {
            var sut = new ShimmerSDK_EXG();
            Assert.Equal(51.2, sut.SamplingRate, precision: 1);
            var exgEnabled = GetPrivateField<bool>(sut, "_enableExg");
            Assert.False(exgEnabled);
        }

        // Ctor() — behavior
        // Modalità EXG predefinita = ECG
        [Fact]
        public void Ctor_Default_ExgMode_Is_ECG()
        {
            var sut = new ShimmerSDK_EXG();
            var mode = GetPrivateField<ExgMode>(sut, "_exgMode");
            Assert.Equal(ExgMode.ECG, mode);
        }


        // SetFirmwareSamplingRateNearest() — behavior
        // Quantizza a clock/divider e aggiorna SamplingRate
        [Fact]
        public void SetFirmwareSamplingRateNearest_Quantizes_And_Updates_SR()
        {
            var sut = new ShimmerSDK_EXG();
            double applied = sut.SetFirmwareSamplingRateNearest(50.0);
            // 32768 / round(32768/50=655.36) = 32768/655 ≈ 50.02748
            Assert.InRange(applied, 50.02, 50.04);
            Assert.InRange(sut.SamplingRate, 50.02, 50.04);
        }

        // SetFirmwareSamplingRateNearest() — behavior
        // Monotonia locale: una richiesta un po' maggiore non deve diminuire l'applicato
        [Fact]
        public void SetFirmwareSamplingRateNearest_Is_Locally_Monotonic()
        {
            var sut = new ShimmerSDK_EXG();
            var a1 = sut.SetFirmwareSamplingRateNearest(100.0);
            var a2 = sut.SetFirmwareSamplingRateNearest(101.0);
            Assert.True(a2 >= a1 - 1e-9);
        }

        // SetFirmwareSamplingRateNearest() — behavior
        // Input non-positivo -> ArgumentOutOfRangeException
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void SetFirmwareSamplingRateNearest_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_EXG();
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.SetFirmwareSamplingRateNearest(requested));
        }

        // SetFirmwareSamplingRateNearest() — integration
        // Se il ramo WINDOWS è presente, scrive nel driver; altrove aggiorna comunque SamplingRate
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

            // Se esiste il campo/metodo specifico di Windows, il SUT chiama WriteSamplingRate(applied).
            bool windowsBranchPresent =
                sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic) != null ||
                sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic) != null;

            if (windowsBranchPresent)
            {
                Assert.Equal(applied, shimmer!.LastSamplingRateWritten, precision: 10);
            }
            else
            {
                // Build cross-OS: il fake resta a 0, ma la proprietà nel SUT è aggiornata.
                Assert.Equal(0, shimmer!.LastSamplingRateWritten);
                Assert.InRange(sut.SamplingRate, applied - 1e-10, applied + 1e-10);
            }
        }

        // SetFirmwareSamplingRateNearestAsync() — behavior
        // Async replica il sync (stesso input)
        [Fact]
        public async Task SetFirmwareSamplingRateNearestAsync_Mirrors_Sync()
        {
            var sut = new ShimmerSDK_EXG();
            double r1 = sut.SetFirmwareSamplingRateNearest(51.2);
            double r2 = await sut.SetFirmwareSamplingRateNearestAsync(51.2);
            Assert.Equal(r1, r2, precision: 8);
        }

        // SetFirmwareSamplingRateNearestAsync() — behavior
        // Due input diversi portano a due quantizzazioni attese (es. ~204.8Hz e ~256Hz)
        [Fact]
        public async Task SetFirmwareSamplingRateNearestAsync_Quantizes_Two_Different_Inputs()
        {
            var sut = new ShimmerSDK_EXG();
            double a1 = await sut.SetFirmwareSamplingRateNearestAsync(200.0); // 32768/round(163.84)=32768/164≈199.8
            double a2 = await sut.SetFirmwareSamplingRateNearestAsync(256.0); // 32768/128=256
            Assert.True(a2 >= a1);
            Assert.InRange(a2, 255.9, 256.1);
        }


        // SetFirmwareSamplingRateNearest behavior

            // Utility locale: stessa quantizzazione del metodo sotto test
            // divider = round(32768 / requested, MidpointRounding.AwayFromZero)
            // applied = 32768 / divider
            private static double Quantize(double requested)
            {
                const double clock = 32768.0;
                int divider = Math.Max(1, (int)Math.Round(clock / requested, MidpointRounding.AwayFromZero));
                return clock / divider;
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Caso "divisore esatto": 51.2 Hz = 32768 / 640 → deve restare 51.2
            [Fact]
            public void SetFirmwareSamplingRateNearest_ExactDivisor_IsIdentity()
            {
                var sut = new ShimmerSDK_EXG();
                double applied = sut.SetFirmwareSamplingRateNearest(51.2);
                Assert.InRange(applied, 51.1999, 51.2001);
                Assert.InRange(sut.SamplingRate, 51.1999, 51.2001);
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Quantizza a clock/divider e aggiorna SamplingRate
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

            // SetFirmwareSamplingRateNearest() — behavior
            // Monotonia locale: aumentare leggermente la richiesta non deve ridurre l'applicato
            [Fact]
            public void SetFirmwareSamplingRateNearest_IsLocallyMonotonic()
            {
                var sut = new ShimmerSDK_EXG();
                var a1 = sut.SetFirmwareSamplingRateNearest(100.0);
                var a2 = sut.SetFirmwareSamplingRateNearest(101.0);
                Assert.True(a2 >= a1 - 1e-12);
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Idempotenza: chiamare due volte con la stessa richiesta produce lo stesso applicato
            [Fact]
            public void SetFirmwareSamplingRateNearest_IsIdempotent_ForSameInput()
            {
                var sut = new ShimmerSDK_EXG();
                var a1 = sut.SetFirmwareSamplingRateNearest(200.0);
                var a2 = sut.SetFirmwareSamplingRateNearest(200.0);
                Assert.InRange(a2, a1 - 1e-12, a1 + 1e-12);
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Gestione input non-positivi: deve lanciare ArgumentOutOfRangeException
            [Theory]
            [InlineData(0)]
            [InlineData(-1)]
            [InlineData(-100)]
            public void SetFirmwareSamplingRateNearest_Throws_OnNonPositive(double requested)
            {
                var sut = new ShimmerSDK_EXG();
                Assert.Throws<ArgumentOutOfRangeException>(() => sut.SetFirmwareSamplingRateNearest(requested));
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Tie-breaking (.5): con MidpointRounding.AwayFromZero deve fare "ceiling" del divisore.
            // Scegliamo requested = clock / (N + 0.5) → divider atteso = N + 1 → applied = clock / (N+1).
            [Theory]
            [InlineData(100)]
            [InlineData(200)]
            [InlineData(655)]
            public void SetFirmwareSamplingRateNearest_RoundsHalfAwayFromZero_OnDivider(int N)
            {
                const double clock = 32768.0;
                double requested = clock / (N + 0.5);       // forza il .5 sul divisore
                double expected = clock / (N + 1);         // AwayFromZero → N+1

                var sut = new ShimmerSDK_EXG();
                double applied = sut.SetFirmwareSamplingRateNearest(requested);

                Assert.InRange(applied, expected - 1e-12, expected + 1e-12);
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Estremi: richiesta molto alta → divider=1 → applied = clock
            [Fact]
            public void SetFirmwareSamplingRateNearest_ClampsToClock_ForVeryHighRequest()
            {
                const double clock = 32768.0;
                var sut = new ShimmerSDK_EXG();
                double applied = sut.SetFirmwareSamplingRateNearest(1e9); // enorme
                Assert.InRange(applied, clock - 1e-12, clock + 1e-12);
            }

            // SetFirmwareSamplingRateNearest() — behavior
            // Estremi: richiesta molto bassa → divider grande → applied piccolo (>0)
            [Theory]
            [InlineData(0.1)]
            [InlineData(0.01)]
            public void SetFirmwareSamplingRateNearest_ProducesSmallPositive_ForVeryLowRequest(double requested)
            {
                var sut = new ShimmerSDK_EXG();
                double applied = sut.SetFirmwareSamplingRateNearest(requested);
                Assert.True(applied > 0);
                // deve essere inferiore alla richiesta (perché il divider quantizzato può crescere)
                Assert.True(applied <= requested * 1.01); // tolleriamo 1%
            }






            // TestConfigure()/Configure* — behavior
            // Costruisce bitmap sensori e sottoscrive
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

        // TestConfigure()/Configure* — behavior
        // EXG disabilitato: nessun bit EXG attivo
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


        // Esegue davvero ConfigureWindows se disponibile; altrimenti esce (conta come pass).
        // Esegue davvero ConfigureWindows se disponibile; altrimenti esce (passa su build non-Windows).
        [Fact]
        public void ConfigureWindows_IfPresent_BuildsDriverAndSubscribes()
        {
            // ✅ classe dal namespace, non annidata
            var sut = new ShimmerSDK_EXG();

            var m = sut.GetType().GetMethod(
                "ConfigureWindows",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (m == null)
            {
                // ramo WINDOWS non compilato -> nessuna asserzione qui
                return;
            }

            // ✅ enum dal namespace, non annidato nella classe
            m.Invoke(sut, new object[] {
        "D1","COM1",
        true,true,   // LowNoiseAcc, WideRangeAcc
        true,true,   // Gyro, Mag
        true,true,   // PressureTemp, Battery
        true,false,true, // ExtA6, ExtA7, ExtA15
        true,        // EXG on
        ExgMode.EMG
    });

            var shimmerFi = sut.GetType().GetField("shimmer", BindingFlags.Instance | BindingFlags.NonPublic);
            var shimmer = shimmerFi?.GetValue(sut);
            Assert.NotNull(shimmer);

            var evt = shimmer!.GetType().GetEvent("UICallback");
            Assert.NotNull(evt);
        }

        // GetSafe() — behavior
        // Ritorna null per idx < 0 (skip se il metodo non è compilato)
        [Fact]
        public void GetSafe_NegativeIndex_ReturnsNull()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("GetSafe",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return; // ramo WINDOWS non presente → test no-op cross-OS

            var oc = new ObjectCluster();
            oc.Add("ANY", ShimmerConfiguration.SignalFormats.CAL, 42);

            var result = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(result);
        }

        // GetSafe() — behavior
        // Ritorna null per index fuori range (>= Count)
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

        // GetSafe() — behavior
        // Ritorna il SensorData giusto per index valido
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

        // FindSignal() — behavior
        // Preferisce CAL quando sono presenti sia CAL che RAW
        [Fact]
        public void FindSignal_Prefers_CAL_Over_RAW()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            // Stesso nome in due formati
            oc.Add("FOO", "RAW", 1);
            oc.Add("FOO", ShimmerConfiguration.SignalFormats.CAL, 2);

            var res = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { oc, new[] { "FOO" } })!;
            Assert.Equal(ShimmerConfiguration.SignalFormats.CAL, res.fmt);
            Assert.Equal(oc.GetIndex("FOO", ShimmerConfiguration.SignalFormats.CAL), res.idx);
        }

        // FindSignal() — behavior
        // Se CAL non esiste, ricade su RAW poi UNCAL
        [Fact]
        public void FindSignal_FallsBack_RAW_then_UNCAL()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            // Caso RAW
            var ocRaw = new ObjectCluster();
            ocRaw.Add("BAR", "RAW", 10);
            var resRaw = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { ocRaw, new[] { "BAR" } })!;
            Assert.Equal("RAW", resRaw.fmt);
            Assert.Equal(ocRaw.GetIndex("BAR", "RAW"), resRaw.idx);

            // Caso UNCAL
            var ocUncal = new ObjectCluster();
            ocUncal.Add("BAZ", "UNCAL", 20);
            var resUncal = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { ocUncal, new[] { "BAZ" } })!;
            Assert.Equal("UNCAL", resUncal.fmt);
            Assert.Equal(ocUncal.GetIndex("BAZ", "UNCAL"), resUncal.idx);
        }

        // FindSignal() — behavior
        // Se non trova CAL/RAW/UNCAL, prova senza formato (format-agnostic)
        [Fact]
        public void FindSignal_FallsBack_FormatAgnostic()
        {
            var mi = typeof(ShimmerSDK_EXG).GetMethod("FindSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (mi == null) return;

            var oc = new ObjectCluster();
            oc.Add("QUX", null, 33); // formato assente

            var res = ((int idx, string name, string fmt))mi.Invoke(null, new object[] { oc, new[] { "QUX" } })!;
            Assert.Null(res.fmt);
            Assert.Equal(oc.GetIndex("QUX", null), res.idx);
        }

        // FindSignal() — behavior
        // Nome inesistente → (-1, null, null)
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



        // HandleEvent() — behavior
        // Mappa indici, emette evento e valorizza EXG* quando EXG è abilitato
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

            // Se il metodo privato HandleEvent non è compilato in questa build, bypassa il test.
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
            Assert.True(received!.Values.Length >= 21);
            Assert.NotNull(received!.Values[^2]); // EXG CH1
            Assert.NotNull(received!.Values[^1]); // EXG CH2
        }

        // HandleEvent() — behavior
        // Con EXG disabilitato, i due canali EXG devono risultare null anche se i nomi esistono
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
            Assert.Null(received!.Values[^2]); // EXG CH1 nullo
            Assert.Null(received!.Values[^1]); // EXG CH2 nullo
        }

        // HandleEvent() — behavior
        // Se i nomi EXG non sono presenti nell'ObjectCluster, i canali EXG restano null
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
            // Niente EXG*; solo timestamp
            oc.Add(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL, 777);

            shimmer!.RaiseDataPacket(oc);

            Assert.NotNull(received);
            Assert.True(received!.Values.Length >= 2); // almeno ts + 2 slot EXG
            Assert.Null(received!.Values[^2]); // EXG CH1 mancante -> null
            Assert.Null(received!.Values[^1]); // EXG CH2 mancante -> null
        }

        // ConfigureAndroid() — behavior
        // MAC non valido => ArgumentException. Se il metodo non esiste (niente ramo ANDROID), bypass.
        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("ZZ:ZZ:ZZ:ZZ:ZZ:ZZ")]
        public void ConfigureAndroid_Throws_On_Invalid_Mac(string mac)
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente → test no-op

            var args = new object[]
            {
        "Dev1", mac,
        true, true,  // LN-Acc, WR-Acc
        true, true,  // Gyro, Mag
        true, true,  // Press/Temp, VBatt
        true, false, true, // ExtA6, ExtA7, ExtA15
        true, ExgMode.ECG
            };

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, args));
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Contains("Invalid MAC", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ConfigureAndroid() — behavior
        // MAC valido: costruisce bitmap sensori con EXG e inizializza shimmerAndroid; bypass se assente.
        [Fact]
        public void ConfigureAndroid_Builds_SensorBitmap_And_Initializes_Driver()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            // MAC valido in formato standard
            var args = new object[]
            {
        "Dev1", "00:11:22:33:44:55",
        true,  true,   // LN-Acc, WR-Acc
        true,  true,   // Gyro, Mag
        true,  true,   // Press/Temp, VBatt
        true,  false,  true,   // ExtA6, ExtA7, ExtA15
        true,  ExgMode.EMG
            };

            mi.Invoke(sut, args);

            // shimmerAndroid non nullo
            var fShimmerAndroid = sut.GetType().GetField("shimmerAndroid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fShimmerAndroid);                      // il campo deve esistere su ANDROID
            var shimmerAndroid = fShimmerAndroid!.GetValue(sut);
            Assert.NotNull(shimmerAndroid);

            // bitmap attesa
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

        // ConfigureAndroid() — behavior
        // Reset mappa indici: firstDataPacketAndroid = true e tutti gli index a -1; bypass se assente.
        [Fact]
        public void ConfigureAndroid_Resets_Index_Mapping()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConfigureAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            mi.Invoke(sut, new object[]
            {
        "DevX", "01:23:45:67:89:AB",
        false, true,   // LN-Acc off, WR-Acc on
        false, true,   // Gyro off, Mag on
        false, true,   // Press/Temp off, VBatt on
        false, false, false, // ExtA6/7/15 off
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
        // NB: gli indici EXG vengono risolti al primo DATA_PACKET, quindi qui non li verifichiamo.
    };

            foreach (var fld in indexFields)
            {
                var fi = sut.GetType().GetField(fld, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(fi);
                Assert.Equal(-1, (int)fi!.GetValue(sut)!);
            }
        }




        // // ConnectInternalAndroid() — behavior
        // Se non configurato (shimmerAndroid == null) deve lanciare InvalidOperationException.
        // Se il metodo non esiste (ramo ANDROID non compilato), il test viene bypassato.
        [Fact]
        public void ConnectInternalAndroid_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ConnectInternalAndroid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente → test no-op cross-OS

            var ex = Assert.Throws<TargetInvocationException>(() => mi.Invoke(sut, Array.Empty<object>()));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Contains("ConfigureAndroid", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ApplySamplingRateWithSafeRestartAsync() — behavior
        // Con requestedHz <= 0 deve lanciare ArgumentOutOfRangeException anche se non configurato.
        // Se il metodo non esiste (ramo ANDROID non compilato), bypass.
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void ApplySamplingRateWithSafeRestartAsync_Throws_On_NonPositive(double requested)
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ApplySamplingRateWithSafeRestartAsync",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            // Invoke async method via reflection, catturando l'eccezione inner
            var task = (Task<double>)mi.Invoke(sut, new object[] { requested })!;
            var agg = Assert.Throws<AggregateException>(() => task.GetAwaiter().GetResult());
            var inner = agg.InnerException!;
            Assert.IsType<ArgumentOutOfRangeException>(inner);
        }

        // ApplySamplingRateWithSafeRestartAsync() — behavior
        // Con requestedHz > 0 ma senza configurazione Android → InvalidOperationException.
        // Se il metodo non esiste (ramo ANDROID non compilato), bypass.
        [Fact]
        public void ApplySamplingRateWithSafeRestartAsync_Throws_When_Not_Configured()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = typeof(ShimmerSDK_EXG).GetMethod("ApplySamplingRateWithSafeRestartAsync",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) return; // ramo ANDROID non presente

            var task = (Task<double>)mi.Invoke(sut, new object[] { 100.0 })!;
            var agg = Assert.Throws<AggregateException>(() => task.GetAwaiter().GetResult());
            var inner = agg.InnerException!;
            Assert.IsType<InvalidOperationException>(inner);
            Assert.Contains("ConfigureAndroid", inner.Message, StringComparison.OrdinalIgnoreCase);
        }


        // ===== Helpers riflessione brevi =====
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

        // ========== GetSafeA() ==========
        [Fact]
        public void Android_GetSafeA_Returns_Data_Else_Null()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "GetSafeA", isStatic: true);
            if (mi == null) return; // ramo ANDROID non presente

            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add("A", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1.23);
            oc.Add("B", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 4.56);

            // idx valido
            var d1 = mi.Invoke(null, new object[] { oc, 0 });
            Assert.NotNull(d1);
            // idx -1 -> null
            var d2 = mi.Invoke(null, new object[] { oc, -1 });
            Assert.Null(d2);
            // idx out-of-range -> null
            var d3 = mi.Invoke(null, new object[] { oc, 99 });
            Assert.Null(d3);
        }

        // ========== FindSignalA() ==========
        [Fact]
        public void Android_FindSignalA_Prefers_CAL_Then_RAW_Then_UNCAL_Then_Default()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "FindSignalA", isStatic: true);
            if (mi == null) return; // ramo ANDROID non presente

            var oc = new ShimmerAPI.ObjectCluster();
            // Solo RAW per X
            oc.Add("X", "RAW", 10);
            // Solo UNCAL per Y
            oc.Add("Y", "UNCAL", 20);
            // Solo default (format null) per Z
            oc.Add("Z", null, 30);
            // CAL per W (prioritario)
            oc.Add("W", ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 40);

            // Cerca in ordine W → X → Y → Z; deve trovare W con CAL
            var (idx1, name1, fmt1) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "W", "X", "Y", "Z" } })!;
            Assert.True(idx1 >= 0);
            Assert.Equal("W", name1, ignoreCase: true);
            Assert.Equal(ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, fmt1);

            // Se CAL non esiste, cade su RAW
            var (idx2, name2, fmt2) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "X" } })!;
            Assert.True(idx2 >= 0);
            Assert.Equal("X", name2, ignoreCase: true);
            Assert.Equal("RAW", fmt2);

            // Se non c'è CAL/RAW, cade su UNCAL
            var (idx3, name3, fmt3) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "Y" } })!;
            Assert.True(idx3 >= 0);
            Assert.Equal("UNCAL", fmt3);

            // Se non c'è CAL/RAW/UNCAL, usa default (fmt null)
            var (idx4, name4, fmt4) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "Z" } })!;
            Assert.True(idx4 >= 0);
            Assert.Null(fmt4);

            // Non trovato
            var (idx5, name5, fmt5) = ((int, string?, string?))mi.Invoke(null, new object[] { oc, new[] { "NOPE" } })!;
            Assert.Equal(-1, idx5);
            Assert.Null(name5);
            Assert.Null(fmt5);
        }

        // ========== HandleEventAndroid(): DATA_PACKET ==========
        [Fact]
        public void Android_HandleEventAndroid_DataPacket_Maps_And_Raises_Sample()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "HandleEventAndroid");
            if (mi == null) return; // ramo ANDROID non presente

            // Abilita EXG e stato "primo pacchetto"
            SetField(sut, "_enableExg", true);
            SetField(sut, "firstDataPacketAndroid", true);

            // Ascolta l'evento pubblico
            ShimmerSDK.EXG.ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK.EXG.ShimmerSDK_EXGData)d;

            // Costruisci ObjectCluster con CAL + EXG
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

            // Invoca come se fosse un pacchetto dati
            var ev = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET, oc);
            mi.Invoke(sut, new object?[] { null, ev });

            Assert.NotNull(received);
            Assert.True(received!.Values.Length >= 21);
            Assert.NotNull(received.Values[^2]); // EXG1_CH1
            Assert.NotNull(received.Values[^1]); // EXG2_CH1

            // secondo pacchetto: usa indici già mappati (no eccezioni, evento arriva)
            received = null;
            mi.Invoke(sut, new object?[] { null, ev });
            Assert.NotNull(received);
        }

        // ========== HandleEventAndroid(): STATE_CHANGE ==========
        [Fact]
        public async Task Android_HandleEventAndroid_StateChange_SetsFlags_And_Completes_Tasks()
        {
            var sut = new ShimmerSDK_EXG();
            var mi = GetPrivMethod(typeof(ShimmerSDK_EXG), "HandleEventAndroid");
            if (mi == null) return; // ramo ANDROID non presente

            // Prepara TCS tramite riflessione
            var tcsConnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcsStreaming = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            SetField(sut, "_androidConnectedTcs", tcsConnected);
            SetField(sut, "_androidStreamingAckTcs", tcsStreaming);

            // Evento: CONNECTED
            var evConn = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_CONNECTED);
            mi.Invoke(sut, new object?[] { null, evConn });
            Assert.True(await Task.WhenAny(tcsConnected.Task, Task.Delay(200)) == tcsConnected.Task);

            // Evento: STREAMING
            var evStr = new ShimmerAPI.CustomEventArgs(
                (int)ShimmerAPI.ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE,
                ShimmerAPI.ShimmerBluetooth.SHIMMER_STATE_STREAMING);
            mi.Invoke(sut, new object?[] { null, evStr });
            Assert.True(await Task.WhenAny(tcsStreaming.Task, Task.Delay(200)) == tcsStreaming.Task);

            // Flag privati aggiornati
            Assert.True((bool)GetField(sut, "_androidIsStreaming")!);
            Assert.True((bool)GetField(sut, "firstDataPacketAndroid")!);
        }


    }
}
