/*
 * MainPageViewModelTests.cs
 * Purpose: Unit tests for MainPageViewModel file.
 */


using System.Reflection;
using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using CommunityToolkit.Mvvm.Input;


namespace ShimmerInterfaceTests.ViewModelsTests
{

    using Microsoft.Maui.Controls;
    using ShimmerInterface.Views;


    /// <summary>
    /// Unit tests for <see cref="MainPageViewModel"/> covering:
    /// command construction, initial scan lifecycle, refresh/load semantics,
    /// connect command guard behavior, tabbed page composition, and parsing utilities.
    /// </summary>
    public class MainPageViewModelTests
    {

        /// <summary>
        /// Helper: retrieves a private instance method by name and argument types.
        /// </summary>
        /// <param name="t">Type declaring the method.</param>
        /// <param name="name">Private method name.</param>
        /// <param name="args">Parameter type array for overload resolution.</param>
        /// <returns>The resolved <see cref="MethodInfo"/>.</returns>
        static MethodInfo MI(Type t, string name, params Type[] args)
            => t.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, args, null)!;


        /// <summary>
        /// Helper: Invokes the private <c>InitialScanAsync</c>.
        /// </summary>
        /// <param name="vm">Target <see cref="MainPageViewModel"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        static Task CallInitialScanAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "InitialScanAsync").Invoke(vm, Array.Empty<object>())!;


        /// <summary>
        /// Helper: Invokes the private <c>RefreshDevicesAsync</c>.
        /// </summary>
        /// <param name="vm">Target <see cref="MainPageViewModel"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        static Task CallRefreshDevicesAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "RefreshDevicesAsync").Invoke(vm, Array.Empty<object>())!;


        /// <summary>
        /// Helper: Invokes the private <c>LoadDevicesAsync</c>.
        /// </summary>
        /// <param name="vm">Target <see cref="MainPageViewModel"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        static Task CallLoadDevicesAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "LoadDevicesAsync").Invoke(vm, Array.Empty<object>())!;


        /// <summary>
        /// Helper: Invokes the private <c>CreateTabbedPage</c>.
        /// </summary>
        /// <param name="vm">Target <see cref="MainPageViewModel"/>.</param>
        /// <returns>Nothing.</returns>
        static void CallCreateTabbedPage(MainPageViewModel vm)
            => MI(typeof(MainPageViewModel), "CreateTabbedPage").Invoke(vm, Array.Empty<object>());


        /// <summary>
        /// Helper: Strongly-typed accessor for the async <c>ConnectCommand</c>.
        /// </summary>
        /// <param name="vm">The view model instance.</param>
        /// <returns>The <see cref="IAsyncRelayCommand{T}"/> for <see cref="INavigation"/>.</returns>
        private static IAsyncRelayCommand<INavigation> ConnectAsyncCmd(MainPageViewModel vm)
            => Assert.IsAssignableFrom<IAsyncRelayCommand<INavigation>>(vm.ConnectCommand);


        // ----- Dummy app -----


        /// <summary>
        /// Sets up a minimal MAUI <see cref="Application"/> and a <see cref="TestMainPage"/> as <c>MainPage</c>.
        /// Ensures <c>Application.Current</c> and <c>Application.Current.MainPage</c> are initialized for navigation.
        /// </summary>
        /// <returns>The created <see cref="TestMainPage"/> instance.</returns>
        static TestMainPage UseTestApp()
        {
            var app = new Application();
            var main = new TestMainPage();
            Application.Current = app;
            app.MainPage = main;
            return main;
        }


        // -----  Constructor behavior -----


        /// <summary>
        /// Constructor creates commands and starts initial scan, setting an overlay message.
        /// Expected: commands are non-null; overlay is either "Refreshing devices…" or "Scanning devices…";
        /// after the initial scan completes, <c>IsRefreshing</c> is false.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task Ctor_creates_commands_and_initial_state()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            Assert.NotNull(vm.ConnectCommand);
            Assert.NotNull(vm.RefreshDevicesCommand);
            Assert.NotNull(vm.AvailableDevices);

            var allowed = new[] { "Refreshing devices…", "Scanning devices…" };
            Assert.Contains(vm.OverlayMessage, allowed);

            await (Task)typeof(MainPageViewModel)
                .GetMethod("InitialScanAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(vm, Array.Empty<object>())!;

            Assert.False(vm.IsRefreshing);
        }


        /// <summary>
        /// Constructor-triggered initial scan completes without throwing and leaves the VM not refreshing.
        /// Expected: <c>IsRefreshing == false</c> and <c>AvailableDevices</c> remains empty by default.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task Ctor_initial_scan_does_not_throw_and_leaves_not_refreshing()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            await CallInitialScanAsync(vm);

            Assert.False(vm.IsRefreshing);
            Assert.Empty(vm.AvailableDevices);
        }


        // ----- InitialScanAsync behavior -----


        /// <summary>
        /// Updates the overlay message and toggles busy state appropriately.
        /// Expected: overlay becomes "Scanning devices…" and <c>IsRefreshing == false</c> upon completion.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task InitialScanAsync_sets_overlay_message_and_toggles_busy()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            await CallInitialScanAsync(vm);

            Assert.Equal("Scanning devices…", vm.OverlayMessage);
            Assert.False(vm.IsRefreshing);
        }


        /// <summary>
        /// Does not corrupt an already populated device collection when no devices are found.
        /// Expected: existing items remain intact and in order.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task InitialScanAsync_no_devices_keeps_collection_consistent()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            vm.AvailableDevices.Add(new ShimmerDevice { DisplayName = "X" });
            await CallInitialScanAsync(vm);

            Assert.Single(vm.AvailableDevices);
            Assert.Equal("X", vm.AvailableDevices[0].DisplayName);
        }


        // ----- RefreshDevicesAsync behavior -----


        /// <summary>
        /// When no refresh is currently in progress, the operation updates the overlay text
        /// and clears the busy state once finished.
        /// Expected: overlay equals "Refreshing devices…" and <c>IsRefreshing == false</c>.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task RefreshDevicesAsync_when_not_refreshing_sets_flags_and_returns()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            // Avoid race: wait for the initial scan to finish
            await CallInitialScanAsync(vm);

            await CallRefreshDevicesAsync(vm);

            Assert.Equal("Refreshing devices…", vm.OverlayMessage);
            Assert.False(vm.IsRefreshing);
        }


        /// <summary>
        /// If a refresh is already underway, the operation exits early and displays an informational alert.
        /// Expected: a "Please wait" alert is shown; no additional refresh action is performed.
        /// </summary>
        /// <returns>A task representing the asynchronous assertion flow.</returns>
        [Fact]
        public async Task RefreshDevicesAsync_when_already_refreshing_shows_alert_and_exits_early()
        {
            var main = UseTestApp();
            var vm = new MainPageViewModel();

            typeof(MainPageViewModel).GetProperty("IsRefreshing")!.SetValue(vm, true);

            await CallRefreshDevicesAsync(vm);

            Assert.True(main.LastAlert.HasValue);
            Assert.Equal("Please wait", main.LastAlert.Value.title);
        }


        // ----- LoadDevicesAsync behavior -----


        /// <summary>
        /// In the test setup, the load operation is a no-op and must neither throw nor mutate the preexisting list.
        /// Expected: no exception is thrown and the item originally in the collection remains present.
        /// </summary>
        [Fact]
        public async Task LoadDevicesAsync_noop_does_not_throw_and_does_not_clear_list()
        {
            UseTestApp();
            var vm = new MainPageViewModel();
            vm.AvailableDevices.Add(new ShimmerDevice { DisplayName = "keep" });

            var ex = await Record.ExceptionAsync(() => CallLoadDevicesAsync(vm));
            Assert.Null(ex);
            Assert.Single(vm.AvailableDevices);
        }


        /// <summary>
        /// Repeated invocations of the load operation should be idempotent in tests.
        /// Expected: the device list remains unchanged after multiple calls.
        /// </summary>
        [Fact]
        public async Task LoadDevicesAsync_multiple_calls_keep_idempotent_in_tests()
        {
            UseTestApp();
            var vm = new MainPageViewModel();
            vm.AvailableDevices.Add(new ShimmerDevice { DisplayName = "A" });

            await CallLoadDevicesAsync(vm);
            await CallLoadDevicesAsync(vm);

            Assert.Single(vm.AvailableDevices);
        }


        // ----- Connect behavior -----


        /// <summary>
        /// Guard logic prevents navigation when no device is selected and surfaces an error to the user.
        /// Expected: an "Error" alert is displayed and no navigation occurs.
        /// </summary>
        [Fact]
        public async Task Connect_no_selection_shows_error_alert_and_returns()
        {
            var main = UseTestApp();
            var vm = new MainPageViewModel();

            await ConnectAsyncCmd(vm).ExecuteAsync(main.Navigation);

            Assert.True(main.LastAlert.HasValue);
            Assert.Equal("Error", main.LastAlert.Value.title);
        }


        /// <summary>
        /// When all connection attempts fail, no tabbed UI should be created and the shell page should remain unchanged.
        /// Expected: no <see cref="TabbedPage"/> is assigned; <c>Application.Current.MainPage</c> stays as <see cref="TestMainPage"/>.
        /// </summary>
        [Fact]
        public async Task Connect_does_not_build_tabs_when_all_fail()
        {
            var main = UseTestApp();
            var vm = new MainPageViewModel();

            var d = new ShimmerDevice { DisplayName = "Shimmer ABCD", IsSelected = true };
            vm.AvailableDevices.Add(d);

            var nav = (StubNavigation)main.Navigation;
            _ = ConnectAsyncCmd(vm).ExecuteAsync(main.Navigation);

            await Task.Delay(20);

            var lp = nav.Pushed.OfType<LoadingPage>().FirstOrDefault();
            Assert.NotNull(lp);
            lp!.Tcs.TrySetResult(null);

            await Task.Delay(20);

            Assert.IsType<TestMainPage>(Application.Current!.MainPage);
        }


        // ----- CreateTabbedPage behavior -----


        /// <summary>
        /// The tab creation routine composes one tab for IMU and one for EXG and assigns them under a navigation root.
        /// Expected: <see cref="TabbedPage"/> contains exactly two children and the EXG tab title includes "(EXG)".
        /// </summary>
        [Fact]
        public void CreateTabbedPage_creates_tabs_for_IMU_and_EXG_and_sets_MainPage()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            var devImu = new ShimmerDevice { ShimmerName = "E0D9", DisplayName = "Shimmer E0D9" };
            var devExg = new ShimmerDevice { ShimmerName = "DDCE", DisplayName = "Shimmer DDCE", IsExg = true };

            var sImu = new ShimmerSDK.IMU.ShimmerSDK_IMU();
            var sExg = new ShimmerSDK.EXG.ShimmerSDK_EXG();

            var fi = typeof(MainPageViewModel).GetField("connectedShimmers",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var list = (System.Collections.IList)fi.GetValue(vm)!;

            // Cast the first element to object to match List<(object, ShimmerDevice)>
            list.Add(ValueTuple.Create((object)sImu, devImu));
            list.Add(ValueTuple.Create((object)sExg, devExg));

            var ex = Record.Exception(() => CallCreateTabbedPage(vm));
            Assert.Null(ex);

            var main = Application.Current!.MainPage as NavigationPage;
            Assert.NotNull(main);
            var tabs = main!.Root as TabbedPage;
            Assert.NotNull(tabs);
            Assert.Equal(2, tabs!.Children.Count);

            var t1 = tabs.Children[0].Title!;
            var t2 = tabs.Children[1].Title!;
            Assert.Contains("Shimmer", t1);
            Assert.Contains("Shimmer", t2);
            Assert.Contains("(EXG)", t2);
        }


        // ----- ExtractShimmerName behavior -----


        /// <summary>
        /// The parsing routine extracts a 4-hex suffix from a USB DeviceId string, using the friendly name only as a fallback hint.
        /// Expected: returns either "DDCE" or "AB12" for the provided samples (case-insensitive).
        /// </summary>
        /// <param name="deviceId">USB DeviceId candidate containing the Shimmer token.</param>
        /// <param name="friendly">Friendly name provided as a secondary hint.</param>
        [Theory]
        [InlineData("USB\\VID_000666&PID_0080&MI_00\\7&2AA93E4F&0&00066680DDCE_00", "Foo")]
        [InlineData("USB\\VID_000666&PID_0080&MI_00\\1&X&0&00066680AB12_", "Bar")]
        public void ExtractShimmerName_from_DeviceId_regex(string deviceId, string friendly)
        {
            var mi = typeof(MainPageViewModel).GetMethod("ExtractShimmerName",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var res = (string)mi.Invoke(null, new object?[] { deviceId, friendly })!;
            Assert.True(res is "DDCE" or "AB12");
        }


        /// <summary>
        /// The parsing routine recognizes friendly names like "Shimmer3-XXXX (COMY)" and returns the 4-hex token in uppercase.
        /// Expected: the extracted token matches the expected value exactly (uppercase).
        /// </summary>
        /// <param name="friendly">Friendly device name containing the token.</param>
        /// <param name="expected">Expected 4-hex token.</param>
        [Theory]
        [InlineData("Shimmer3-DDCE (COM4)", "DDCE")]
        [InlineData("Shimmer3-ab12 (COM9)", "AB12")]
        [InlineData("XShimmer3-00ffY (COM1)", "00FF")]
        public void ExtractShimmerName_from_friendly_name_pattern(string friendly, string expected)
        {
            var mi = typeof(MainPageViewModel).GetMethod("ExtractShimmerName",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var res = (string)mi.Invoke(null, new object?[] { "", friendly })!;
            Assert.Equal(expected, res);
        }


        /// <summary>
        /// When neither DeviceId nor friendly name contain a valid 4-hex token, a sentinel value is returned.
        /// Expected: the string "Unknown" is produced for empty, null, or irrelevant inputs.
        /// </summary>
        /// <param name="friendly">Optional friendly name; may be null or unrelated.</param>
        /// <param name="deviceIdLabel">Theory label only; not used by the logic.</param>
        [Theory]
        [InlineData("", "NoMatch")]
        [InlineData(null, "AlsoNo")]
        [InlineData("Random Name (COM7)", "NoHex")]
        public void ExtractShimmerName_returns_Unknown_when_not_found(string? friendly, string deviceIdLabel)
        {
            var mi = typeof(MainPageViewModel).GetMethod("ExtractShimmerName",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var res = (string)mi.Invoke(null, new object?[] { "", friendly ?? "" })!;
            Assert.Equal("Unknown", res);
        }


        // ----- IsHexString behavior -----


        /// <summary>
        /// Hexadecimal validation accepts only hex characters without separators and is case-insensitive.
        /// Expected: true is returned for purely hexadecimal inputs.
        /// </summary>
        /// <param name="s">Candidate string to validate.</param>
        [Theory]
        [InlineData("AB12")]
        [InlineData("00ff")]
        [InlineData("deadBEEF")]
        public void IsHexString_true_for_hex_values(string s)
        {
            var mi = typeof(MainPageViewModel).GetMethod("IsHexString",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var ok = (bool)mi.Invoke(null, new object?[] { s })!;
            Assert.True(ok);
        }


        /// <summary>
        /// Inputs containing non-hex characters, whitespace, separators, or being empty must be rejected.
        /// Expected: false is returned for any input that is not strictly hexadecimal.
        /// </summary>
        /// <param name="s">Candidate string to validate.</param>
        [Theory]
        [InlineData("GHIJ")]
        [InlineData("12 34")]
        [InlineData("1-23")]
        [InlineData("")]
        public void IsHexString_false_for_non_hex_values(string s)
        {
            var mi = typeof(MainPageViewModel).GetMethod("IsHexString",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var ok = (bool)mi.Invoke(null, new object?[] { s })!;
            Assert.False(ok);
        }


        // ----- DeviceIdRegex behavior -----


        /// <summary>
        /// The DeviceId pattern matches the 4-hex token following "00066680" and captures it in the first group.
        /// Expected: a successful match with capture "DDCE".
        /// </summary>
        [Fact]
        public void DeviceIdRegex_matches_and_captures_uppercase()
        {
            var mi = typeof(MainPageViewModel).GetMethod("DeviceIdRegex",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var rx = (System.Text.RegularExpressions.Regex)mi.Invoke(null, Array.Empty<object>())!;
            var m = rx.Match(@"...&00066680DDCE_...");
            Assert.True(m.Success);
            Assert.Equal("DDCE", m.Groups[1].Value);
        }


        /// <summary>
        /// Case-insensitive matching ensures the 4-hex token is captured even when lowercase.
        /// Expected: a successful match with capture "ab12".
        /// </summary>
        [Fact]
        public void DeviceIdRegex_is_case_insensitive_and_captures_lowercase()
        {
            var mi = typeof(MainPageViewModel).GetMethod("DeviceIdRegex",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var rx = (System.Text.RegularExpressions.Regex)mi.Invoke(null, Array.Empty<object>())!;
            var m = rx.Match(@"...&00066680ab12_...");
            Assert.True(m.Success);
            Assert.Equal("ab12", m.Groups[1].Value);
        }
    }
}
