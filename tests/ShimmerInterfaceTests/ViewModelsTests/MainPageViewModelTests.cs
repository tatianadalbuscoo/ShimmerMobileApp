// tests/ShimmerInterfaceTests/ViewModelsTests/MainPageViewModelTests.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using ShimmerInterface.ViewModels;
using ShimmerInterface.Models;
using CommunityToolkit.Mvvm.Input;

namespace ShimmerInterfaceTests.ViewModelsTests
{
    using Microsoft.Maui.Controls;
    using ShimmerInterface.Views;

    public class MainPageViewModelTests
    {
        // --------- riflessione metodi privati ----------
        static MethodInfo MI(Type t, string name, params Type[] args)
            => t.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, args, null)!;

        static Task CallInitialScanAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "InitialScanAsync").Invoke(vm, Array.Empty<object>())!;
        static Task CallRefreshDevicesAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "RefreshDevicesAsync").Invoke(vm, Array.Empty<object>())!;
        static Task CallLoadDevicesAsync(MainPageViewModel vm)
            => (Task)MI(typeof(MainPageViewModel), "LoadDevicesAsync").Invoke(vm, Array.Empty<object>())!;
        static void CallCreateTabbedPage(MainPageViewModel vm)
            => MI(typeof(MainPageViewModel), "CreateTabbedPage").Invoke(vm, Array.Empty<object>());

        private static IAsyncRelayCommand<INavigation> ConnectAsyncCmd(MainPageViewModel vm)
            => Assert.IsAssignableFrom<IAsyncRelayCommand<INavigation>>(vm.ConnectCommand);

        // --------- app fittizia ----------
        static TestMainPage UseTestApp()
        {
            var app = new Application();
            var main = new TestMainPage();
            Application.Current = app;
            app.MainPage = main;
            return main;
        }

        // ===============================
        // Ctor
        // ===============================
        [Fact]
        public async Task Ctor_creates_commands_and_initial_state()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            Assert.NotNull(vm.ConnectCommand);
            Assert.NotNull(vm.RefreshDevicesCommand);
            Assert.NotNull(vm.AvailableDevices);

            // può essere già "Scanning…" a causa della initial scan che parte nel ctor
            var allowed = new[] { "Refreshing devices…", "Scanning devices…" };
            Assert.Contains(vm.OverlayMessage, allowed);

            // assicurati che la initial scan sia terminata prima di verificare IsRefreshing
            await (Task)typeof(MainPageViewModel)
                .GetMethod("InitialScanAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(vm, Array.Empty<object>())!;

            Assert.False(vm.IsRefreshing);
        }


        [Fact]
        public async Task Ctor_initial_scan_does_not_throw_and_leaves_not_refreshing()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            await CallInitialScanAsync(vm);

            Assert.False(vm.IsRefreshing);
            Assert.Empty(vm.AvailableDevices);
        }

        // ===============================
        // InitialScanAsync
        // ===============================
        [Fact]
        public async Task InitialScanAsync_sets_overlay_message_and_toggles_busy()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            await CallInitialScanAsync(vm);

            Assert.Equal("Scanning devices…", vm.OverlayMessage);
            Assert.False(vm.IsRefreshing);
        }

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

        // ===============================
        // RefreshDevicesAsync
        // ===============================
        [Fact]
        public async Task RefreshDevicesAsync_when_not_refreshing_sets_flags_and_returns()
        {
            UseTestApp();
            var vm = new MainPageViewModel();

            // evita race: aspetta che la initial scan sia finita
            await CallInitialScanAsync(vm);

            await CallRefreshDevicesAsync(vm);

            Assert.Equal("Refreshing devices…", vm.OverlayMessage);
            Assert.False(vm.IsRefreshing);
        }

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

        // ===============================
        // LoadDevicesAsync
        // ===============================
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

        // ===============================
        // Connect(INavigation) — guard
        // ===============================
        [Fact]
        public async Task Connect_no_selection_shows_error_alert_and_returns()
        {
            var main = UseTestApp();
            var vm = new MainPageViewModel();

            await ConnectAsyncCmd(vm).ExecuteAsync(main.Navigation);

            Assert.True(main.LastAlert.HasValue);
            Assert.Equal("Error", main.LastAlert.Value.title);
        }

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

        // ===============================
        // CreateTabbedPage
        // ===============================
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

            // importante: cast del primo elemento a object per rispettare List<(object, ShimmerDevice)>
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

        // ===============================
        // ExtractShimmerName
        // ===============================
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

        // ===============================
        // IsHexString
        // ===============================
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

        [Theory]
        [InlineData("GHIJ")]
        [InlineData("12 34")]
        [InlineData("1-23")]
        [InlineData("")] // empty deve essere false
        public void IsHexString_false_for_non_hex_values(string s)
        {
            var mi = typeof(MainPageViewModel).GetMethod("IsHexString",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var ok = (bool)mi.Invoke(null, new object?[] { s })!;
            Assert.False(ok);
        }

        // ===============================
        // DeviceIdRegex
        // ===============================
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
