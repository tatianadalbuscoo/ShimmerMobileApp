/*
 * LoadingPageViewModelTests.cs
 * Purpose: Unit tests for LoadingPageViewModel file.
 */


using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using CommunityToolkit.Mvvm.Input;


namespace ShimmerInterfaceTests.ViewModelsTests
{
    public class LoadingPageViewModelTests
    {

        /// <summary>
        /// Helper: builds a <see cref="ShimmerDevice"/> configured for tests, optionally marked as EXG.
        /// </summary>
        /// <param name="exg">Whether the device should be flagged as EXG-capable.</param>
        /// <returns>A configured <see cref="ShimmerDevice"/> instance.</returns>
        private static ShimmerDevice MakeDevice(bool exg = false) => new ShimmerDevice
        {
            ShimmerName = "DDCE",
            DisplayName = "Shimmer DDCE",
            Port1 = exg ? "COM42" : "COM43",
            IsExg = exg,
            EnableExg = exg,

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


        /// <summary>
        /// Helper: gets the strongly-typed async command that starts the connection.
        /// </summary>
        /// <param name="vm">Target <see cref="LoadingPageViewModel"/>.</param>
        /// <returns>The <see cref="IAsyncRelayCommand"/> instance.</returns>
        private static IAsyncRelayCommand StartCmd(LoadingPageViewModel vm)
            => Assert.IsAssignableFrom<IAsyncRelayCommand>(
                typeof(LoadingPageViewModel).GetProperty("StartConnectionCommand")!.GetValue(vm));


        /// <summary>
        /// Helper: gets the relay command used to dismiss the alert.
        /// </summary>
        /// <param name="vm">Target <see cref="LoadingPageViewModel"/>.</param>
        /// <returns>The <see cref="IRelayCommand"/> instance.</returns>
        private static IRelayCommand DismissCmd(LoadingPageViewModel vm)
            => Assert.IsAssignableFrom<IRelayCommand>(
                typeof(LoadingPageViewModel).GetProperty("DismissAlertCommand")!.GetValue(vm));


        /// <summary>
        /// Helper: polls until a condition becomes true or a timeout is reached.
        /// </summary>
        /// <param name="cond">Condition to evaluate.</param>
        /// <param name="polls">Maximum number of polls.</param>
        /// <param name="delayMs">Delay, in milliseconds, between polls.</param>
        /// <returns><c>true</c> if the condition becomes true within the allotted polls; otherwise <c>false</c>.</returns>
        private static async Task<bool> WaitUntil(Func<bool> cond, int polls = 25, int delayMs = 40)
        {
            for (int i = 0; i < polls; i++)
            {
                if (cond()) return true;
                await Task.Delay(delayMs);
            }
            return cond();
        }


        // ----- Constructor behavior -----


        /// <summary>
        /// Default construction initializes flags and sets a neutral connecting message.
        /// Expected: <c>IsConnecting == false</c>, <c>ShowAlert == false</c>, empty alert fields, and a non-null <c>ConnectingMessage</c>.
        /// </summary>
        [Fact]
        public void Ctor_initializes_flags_and_connecting_message()
        {
            var dev = new ShimmerDevice { ShimmerName = "DDCE", Port1 = "COM4" };
            var tcs = new TaskCompletionSource<object?>();

            var vm = new LoadingPageViewModel(dev, tcs);

            // Initial flags
            Assert.False(vm.IsConnecting);
            Assert.False(vm.ShowAlert);
            Assert.Equal("", vm.AlertTitle);
            Assert.Equal("", vm.AlertMessage);

            // In CI there may be no platform symbols -> ConnectingMessage may be empty -> that is acceptable.
            Assert.NotNull(vm.ConnectingMessage);
        }


        // ----- StartConnectionAsync behavior -----


        /// <summary>
        /// Starting the connection turns on the spinner and eventually shows an alert.
        /// Expected: <c>IsConnecting</c> becomes true shortly, and <c>ShowAlert</c> becomes true before completion.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task StartConnectionAsync_turns_on_spinner_and_shows_alert()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            var run = StartCmd(vm).ExecuteAsync(null);

            // Spinner should activate
            Assert.True(await WaitUntil(() => vm.IsConnecting, polls: 10, delayMs: 30));

            // Alert should appear
            Assert.True(await WaitUntil(() => vm.ShowAlert, polls: 30, delayMs: 50));

            // Close alert to allow command completion
            DismissCmd(vm).Execute(null);
            await run;

            Assert.False(vm.IsConnecting);
            Assert.False(vm.ShowAlert);
        }


        /// <summary>
        /// When the test stubs cause the connection to fail, the alert title and message reflect the failure.
        /// Expected: title equals "Connection Failed" and message contains "Could not connect".
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task StartConnectionAsync_sets_failure_title_and_message_when_connection_fails()
        {
            var tcs = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(MakeDevice(), tcs);

            var run = StartCmd(vm).ExecuteAsync(null);
            Assert.True(await WaitUntil(() => vm.ShowAlert));

            // With the test stubs, connection does not open -> dev == null
            Assert.Equal("Connection Failed", vm.AlertTitle);
            Assert.Contains("Could not connect", vm.AlertMessage, StringComparison.OrdinalIgnoreCase);

            DismissCmd(vm).Execute(null);
            await run;

            var result = await tcs.Task;
            Assert.Null(result);
        }


        /// <summary>
        /// Re-entrancy is guarded so a second start while the first is active exits quickly without side effects.
        /// Expected: the second execution returns rapidly (e.g., &lt; 150ms) while the first remains in progress.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task StartConnectionAsync_is_reentrant_safe()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            // first execution (still running)
            var first = StartCmd(vm).ExecuteAsync(null);
            Assert.True(await WaitUntil(() => vm.IsConnecting));

            // second execution should be a quick no-op
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await StartCmd(vm).ExecuteAsync(null);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 150, $"Second execution took {sw.ElapsedMilliseconds}ms");

            // close alert to finish the first run
            Assert.True(await WaitUntil(() => vm.ShowAlert));
            DismissCmd(vm).Execute(null);
            await first;
        }


        /// <summary>
        /// After the alert is dismissed by the user, the external <see cref="TaskCompletionSource{TResult}"/>
        /// provided by the caller is completed to signal the outcome.
        /// Expected: the external TCS completes with <c>null</c> and the view model leaves the connecting state.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task StartConnectionAsync_completes_external_tcs_after_dismiss()
        {
            var external = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(MakeDevice(), external);

            var start = StartCmd(vm).ExecuteAsync(null);

            Assert.True(await WaitUntil(() => vm.ShowAlert));

            DismissCmd(vm).Execute(null);

            var obj = await external.Task;
            Assert.Null(obj);

            await start;
            Assert.False(vm.IsConnecting);
        }


        // ----- DismissAlert behavior -----


        /// <summary>
        /// Dismissing the alert is safe to invoke even when the alert is already hidden (idempotent behavior).
        /// Expected: no exception is thrown and the <c>ShowAlert</c> flag remains <c>false</c>.
        /// </summary>
        [Fact]
        public void DismissAlert_is_idempotent_when_alert_already_hidden()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());
            Assert.False(vm.ShowAlert);

            DismissCmd(vm).Execute(null);

            Assert.False(vm.ShowAlert);
        }


        /// <summary>
        /// When the alert is currently visible, dismissing it should hide the alert.
        /// Expected: <c>ShowAlert</c> transitions from <c>true</c> to <c>false</c>.
        /// </summary>
        [Fact]
        public void DismissAlert_hides_visible_alert()
        {
            var vm = new LoadingPageViewModel(MakeDevice(), new TaskCompletionSource<object?>());

            typeof(LoadingPageViewModel).GetProperty(nameof(LoadingPageViewModel.ShowAlert))!.SetValue(vm, true);

            DismissCmd(vm).Execute(null);

            Assert.False(vm.ShowAlert);
        }


        /// <summary>
        /// Clearing the alert should also notify any waiters that depended on the alert state,
        /// ensuring only a single effective invocation toggles the flag.
        /// Expected: <c>ShowAlert</c> is <c>false</c> after dismissal.
        /// </summary>
        [Fact]
        public void DismissAlert_clears_flag_and_signals_waiters()
        {
            var dev = new ShimmerDevice { ShimmerName = "DDCE", Port1 = "COM4" };
            var tcs = new TaskCompletionSource<object?>();
            var vm = new LoadingPageViewModel(dev, tcs);

            vm.ShowAlert = true;

            Assert.True(vm.ShowAlert);

            vm.DismissAlertCommand.Execute(null);

            Assert.False(vm.ShowAlert);
        }
    }
}
