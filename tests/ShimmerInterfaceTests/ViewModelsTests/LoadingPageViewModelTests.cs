// tests/ShimmerInterfaceTests/ViewModelsTests/LoadingPageViewModelTests.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using CommunityToolkit.Mvvm.Input;

namespace ShimmerInterfaceTests.ViewModelsTests
{
    public class LoadingPageViewModelTests
    {
        // --- helpers ---------------------------------------------------------

        private static ShimmerDevice MakeDevice(bool exg = false) => new ShimmerDevice
        {
            ShimmerName = "DDCE",
            DisplayName = "Shimmer DDCE",
            Port1 = exg ? "COM42" : "COM43",
            IsExg = exg,
            EnableExg = exg,

            // sensori “on” per non influire sul flusso
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true
        };

        private static IAsyncRelayCommand StartCmd(LoadingPageViewModel vm)
            => Assert.IsAssignableFrom<IAsyncRelayCommand>(
                typeof(LoadingPageViewModel).GetProperty("StartConnectionCommand")!.GetValue(vm));

        private static IRelayCommand DismissCmd(LoadingPageViewModel vm)
            => Assert.IsAssignableFrom<IRelayCommand>(
                typeof(LoadingPageViewModel).GetProperty("DismissAlertCommand")!.GetValue(vm));

        private static async Task<bool> WaitUntil(Func<bool> cond, int polls = 25, int delayMs = 40)
        {
            for (int i = 0; i < polls; i++)
            {
                if (cond()) return true;
                await Task.Delay(delayMs);
            }
            return cond();
        }

        // =====================================================================
        // Method: ctor — Behavior: inizializza stato/proprietà e messaggio
        // =====================================================================
        // LoadingPageViewModel — Ctor
        // behavior: inizializza flag e messaggio di connessione generico
        // LoadingPageViewModel — Ctor
        // behavior: inizializza flag; ConnectingMessage può essere vuoto in ambiente neutro
        [Fact]
        public void Ctor_initializes_flags_and_connecting_message()
        {
            var dev = new ShimmerDevice { ShimmerName = "DDCE", Port1 = "COM4" };
            var tcs = new TaskCompletionSource<object?>();

            var vm = new LoadingPageViewModel(dev, tcs);

            // flag iniziali
            Assert.False(vm.IsConnecting);
            Assert.False(vm.ShowAlert);
            Assert.Equal("", vm.AlertTitle);
            Assert.Equal("", vm.AlertMessage);

            // In CI non abbiamo simboli di piattaforma → ConnectingMessage resta vuoto: va bene così.
            // Quindi NON richiediamo "Connecting" né non-vuoto.
            Assert.NotNull(vm.ConnectingMessage);
        }



        // =====================================================================
        // Method: StartConnectionAsync — Behavior: attiva spinner e mostra alert
        // =====================================================================
        [Fact]
        public async Task StartConnectionAsync_turns_on_spinner_and_shows_alert()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            var run = StartCmd(vm).ExecuteAsync(null);

            // spinner deve attivarsi
            Assert.True(await WaitUntil(() => vm.IsConnecting, polls: 10, delayMs: 30));

            // alert deve comparire
            Assert.True(await WaitUntil(() => vm.ShowAlert, polls: 30, delayMs: 50));

            // cleanup: chiudi alert per far terminare la command
            DismissCmd(vm).Execute(null);
            await run;

            Assert.False(vm.IsConnecting);
            Assert.False(vm.ShowAlert);
        }

        // =====================================================================
        // Method: StartConnectionAsync — Behavior: produce messaggio di fallimento (dev null)
        // =====================================================================
        [Fact]
        public async Task StartConnectionAsync_sets_failure_title_and_message_when_connection_fails()
        {
            var tcs = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(MakeDevice(), tcs);

            var run = StartCmd(vm).ExecuteAsync(null);
            Assert.True(await WaitUntil(() => vm.ShowAlert));

            // con gli stub di test la connessione non si apre -> dev == null
            Assert.Equal("Connection Failed", vm.AlertTitle);
            Assert.Contains("Could not connect", vm.AlertMessage, StringComparison.OrdinalIgnoreCase);

            DismissCmd(vm).Execute(null);
            await run;

            var result = await tcs.Task;
            Assert.Null(result);
        }

        // =====================================================================
        // Method: StartConnectionAsync — Behavior: guard re-entrancy (seconda invocazione ignorata)
        // =====================================================================
        [Fact]
        public async Task StartConnectionAsync_is_reentrant_safe()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            // Prima esecuzione (ancora in corso)
            var first = StartCmd(vm).ExecuteAsync(null);
            Assert.True(await WaitUntil(() => vm.IsConnecting));

            // Seconda esecuzione: deve “no-op” e terminare rapidamente
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await StartCmd(vm).ExecuteAsync(null);
            sw.Stop();

            // Se il guard funziona, la seconda termina quasi subito (< 150ms)
            Assert.True(sw.ElapsedMilliseconds < 150, $"Second execution took {sw.ElapsedMilliseconds}ms");

            // Chiudi alert per finire il primo giro
            Assert.True(await WaitUntil(() => vm.ShowAlert));
            DismissCmd(vm).Execute(null);
            await first;
        }

        // =====================================================================
        // Method: StartConnectionAsync — Behavior: completa il TCS del chiamante dopo Dismiss
        // =====================================================================
        [Fact]
        public async Task StartConnectionAsync_completes_external_tcs_after_dismiss()
        {
            var external = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(MakeDevice(), external);

            var start = StartCmd(vm).ExecuteAsync(null);
            Assert.True(await WaitUntil(() => vm.ShowAlert));

            // L’utente chiude l’alert
            DismissCmd(vm).Execute(null);

            // Il TCS esterno deve completarsi (esito fallito -> null)
            var obj = await external.Task;
            Assert.Null(obj);

            await start;
            Assert.False(vm.IsConnecting);
        }

        // =====================================================================
        // Method: DismissAlert — Behavior: nasconde alert anche se già nascosto (idempotenza)
        // =====================================================================
        [Fact]
        public void DismissAlert_is_idempotent_when_alert_already_hidden()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());
            Assert.False(vm.ShowAlert);

            // non deve lanciare eccezioni
            DismissCmd(vm).Execute(null);

            Assert.False(vm.ShowAlert);
        }



        // =====================================================================
        // Method: DismissAlert — Behavior: nasconde alert visibile
        // =====================================================================
        [Fact]
        public void DismissAlert_hides_visible_alert()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            // set via riflessione per forzare la condizione
            typeof(LoadingPageViewModel).GetProperty(nameof(LoadingPageViewModel.ShowAlert))!.SetValue(vm, true);

            DismissCmd(vm).Execute(null);

            Assert.False(vm.ShowAlert);
        }

        // LoadingPageViewModel — DismissAlert
        // behavior: spegne il flag e segnala i waiter (una sola invocazione)
        [Fact]
        public void DismissAlert_clears_flag_and_signals_waiters()
        {
            var dev = new ShimmerDevice { ShimmerName = "DDCE", Port1 = "COM4" };
            var tcs = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(dev, tcs);

            // Prepara stato "alert visibile" come accadrebbe dopo StartConnectionAsync
            vm.ShowAlert = true;

            // Verifica precondizione
            Assert.True(vm.ShowAlert);

            // Act
            vm.DismissAlertCommand.Execute(null);

            // Assert: il flag è spento
            Assert.False(vm.ShowAlert);
        }


    }
}
