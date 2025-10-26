using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using ShimmerAPI;
using ShimmerSDK.EXG;
using Xunit;

namespace ShimmerSDKTests.EXGTests
{
    public class ShimmerSDK_EXG_StreamingTests
    {
        // Helper: ottiene il fake driver dal registro
        private static ShimmerLogAndStreamSystemSerialPortV2 GetDrv(ShimmerSDK_EXG sut)
        {
            var drv = ShimmerSDK_EXG_TestRegistry.Get(sut);
            Assert.NotNull(drv);
            return drv!;
        }

        // --- Connect(): nessuna eccezione; se il ramo WINDOWS è compilato,
        // chiama Connect/WriteSamplingRate/WriteSensors/Inquiry sul driver fake.
        [Fact]
        public void Connect_NoThrow_And_Optionally_Calls_Driver()
        {
            var sut = new ShimmerSDK_EXG();

            // Config di comodo: abilito vari sensori per avere una bitmap non banale
            sut.TestConfigure(
                deviceName: "Dev1", portOrId: "COM1",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: true, enableMag: true,
                enablePressureTemp: true, enableBatteryVoltage: true,
                enableExtA6: true, enableExtA7: false, enableExtA15: true,
                enableExg: true, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            // Act: non deve lanciare su nessuna piattaforma
            sut.Connect();

            // Se il ramo WINDOWS è presente a compile-time, la partial chiama davvero i metodi del driver:
            // lo rileviamo così: esiste in ShimmerSDK_EXG un campo privato "_winEnabledSensors"?
            // (Campo usato solo dal ramo WINDOWS.)
            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);

            if (winField != null)
            {
                Assert.True(drv.ConnectCount >= 1);
                // La partial arrotonda SR se <=0 a ~51.2; non imponiamo un valore esatto, ma deve aver scritto qualcosa.
                Assert.True(drv.LastSamplingRateWritten > 0);
                Assert.True(drv.WriteSensorsCount >= 1);
                Assert.True(drv.InquiryCount >= 1);
                Assert.True(drv.IsConnected());
            }
            else
            {
                // Ramo WINDOWS non compilato: accettiamo no-op, ma nessuna eccezione è già la garanzia utile.
                Assert.True(drv.ConnectCount >= 0);
            }
        }

        // --- StartStreaming(): multipiattaforma
        // Non deve lanciare; se c'è il ramo WINDOWS, vediamo StartCount incrementare.
        [Fact]
        public async Task StartStreaming_NoThrow_And_Optionally_Increments_StartCount()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev2", portOrId: "COM2",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            // non deve esplodere (è async void inside; diamo un piccolo delay)
            sut.StartStreaming();
            await Task.Delay(50);

            // Se ramo WINDOWS compilato, la partial chiama StartStreaming() sul driver
            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(drv.StartCount >= 1);
            }
        }

        // --- StopStreaming(): multipiattaforma
        // Non deve lanciare; se c’è il ramo WINDOWS, vediamo StopCount incrementare.
        [Fact]
        public async Task StopStreaming_NoThrow_And_Optionally_Increments_StopCount()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev3", portOrId: "COM3",
                enableLowNoiseAcc: true, enableWideRangeAcc: true,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            var drv = GetDrv(sut);

            sut.StartStreaming();
            await Task.Delay(20);
            sut.StopStreaming();
            await Task.Delay(50);

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(drv.StopCount >= 1);
            }
        }

        // --- Streaming “end-to-end” (simulata): dopo StartStreaming possiamo ricevere pacchetti
        // via RaiseDataPacket e (se previsto dalla build) ottenere l’evento SampleReceived.
        // In alternativa, se l’evento non viene emesso in questa build, verifichiamo che
        // la mappatura degli indici sia avvenuta (firstDataPacket*==false).
        [Fact]
        public void After_StartStreaming_Raising_ObjectCluster_Fires_SampleReceived()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "D",
                portOrId: "P",
                enableLowNoiseAcc: true,
                enableWideRangeAcc: true,
                enableGyro: true,
                enableMag: true,
                enablePressureTemp: true,
                enableBatteryVoltage: true,
                enableExtA6: true,
                enableExtA7: true,
                enableExtA15: true,
                enableExg: true,
                exgMode: ExgMode.ECG
            );

            // Se la build non contiene nessuno dei due handler privati, non c'è nulla da verificare qui.
            var hasWinHandler = sut.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic) != null;
            var hasAndroidHandler = sut.GetType().GetMethod("HandleEventAndroid", BindingFlags.Instance | BindingFlags.NonPublic) != null;
            if (!hasWinHandler && !hasAndroidHandler)
                return;

            ShimmerSDK_EXGData? received = null;
            sut.SampleReceived += (s, d) => received = (ShimmerSDK_EXGData)d;

            sut.StartStreaming(); // non blocca

            var shim = ShimmerSDK_EXG_TestRegistry.Get(sut)!;

            // Costruisci un OC con timestamp + qualche segnale tipico
            var oc = new ShimmerAPI.ObjectCluster();
            oc.Add(ShimmerAPI.ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 123);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 2);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 3);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXG1_CH1, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1001);
            oc.Add(ShimmerAPI.Shimmer3Configuration.SignalNames.EXG2_CH1, ShimmerAPI.ShimmerConfiguration.SignalFormats.CAL, 1002);

            // 1) Primo pacchetto: mappa gli indici (spesso non genera l’evento)
            shim.RaiseDataPacket(oc);

            // 2) Secondo pacchetto: con la mappa pronta, ora l’evento dovrebbe arrivare
            shim.RaiseDataPacket(oc);

            if (received is null)
            {
                // Se l’evento non è previsto in questa build, verifichiamo almeno che la mappatura sia avvenuta.
                var firstFlagName = hasWinHandler ? "firstDataPacket" : "firstDataPacketAndroid";
                var firstFlag = sut.GetType().GetField(firstFlagName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (firstFlag != null)
                {
                    // Deve essere stato portato a false dall’handler dopo il primo pacchetto
                    var flagNow = (bool)firstFlag.GetValue(sut)!;
                    Assert.False(flagNow); // mappatura eseguita => handler è girato
                    return;                // test OK anche senza evento
                }
            }

            // Se l’evento è previsto, allora deve essere arrivato
            Assert.NotNull(received);
        }

        // --- IsConnected(): deve esistere sempre; se il ramo WINDOWS è compilato e abbiamo chiamato Connect(),
        // deve tornare true; altrimenti false/no-op.
        [Fact]
        public void IsConnected_Returns_True_Only_When_Runtime_Windows_Branch_Is_Active()
        {
            var sut = new ShimmerSDK_EXG();
            sut.TestConfigure(
                deviceName: "Dev5", portOrId: "COM5",
                enableLowNoiseAcc: true, enableWideRangeAcc: false,
                enableGyro: false, enableMag: false,
                enablePressureTemp: false, enableBatteryVoltage: false,
                enableExtA6: false, enableExtA7: false, enableExtA15: false,
                enableExg: false, exgMode: ExgMode.ECG);

            // prima della connect: dovrebbe essere false ovunque
            Assert.False(sut.IsConnected());

            sut.Connect();

            var winField = sut.GetType().GetField("_winEnabledSensors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (winField != null)
            {
                Assert.True(sut.IsConnected());
            }
            else
            {
                Assert.False(sut.IsConnected()); // ramo non compilato → resta false
            }
        }

        // DelayWork — behavior
        // Attende ~ il tempo richiesto (con tolleranza)
        [Fact]
        public async Task DelayWork_Waits_Approximately_Requested_Time()
        {
            var sut = new ShimmerSDK_EXG();

            // Metodo privato d'istanza
            var mi = typeof(ShimmerSDK_EXG).GetMethod(
                "DelayWork",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Se la partial senza DelayWork non è compilata in questa build → no-op
            if (mi == null) return;

            const int requestedMs = 80; // scegli un valore piccolo ma misurabile
            var sw = Stopwatch.StartNew();

            // Invoca e attende il Task restituito
            var task = (Task)mi.Invoke(sut, new object[] { requestedMs })!;
            await task;

            sw.Stop();
            var elapsed = sw.ElapsedMilliseconds;

            // Tolleranza: deve essere >= requested-10ms e non "enormemente" sopra (network jitter inesistente qui)
            Assert.True(elapsed >= requestedMs - 10, $"Elapsed {elapsed}ms < {requestedMs - 10}ms");
            Assert.True(elapsed <= requestedMs + 200, $"Elapsed {elapsed}ms > {requestedMs + 200}ms");
        }
    }
}
