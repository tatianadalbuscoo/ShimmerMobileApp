/* 
 * MAUI App entry (cross-platform).
 * - iOS/macOS: connects via WebSocket to an Android-hosted Shimmer bridge and builds tabs per device (EXG/IMU).
 * - Android/Windows: navigates directly to MainPage.
 * - Includes simple loading UI, global error logging, and WS helpers (discover/config/mode). 
 */


using ShimmerInterface.Views;


#if IOS || MACCATALYST

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using ShimmerSDK.IMU;
using ShimmerInterface.Models;
using ShimmerSDK.EXG; 

#endif


namespace ShimmerInterface;


/// <summary>
/// MAUI app bootstrap: on iOS/macOS connects via WebSocket to a Shimmer bridge
/// running on an Android device reachable over the LAN/hotspot (BridgeHost/Port).
/// </summary>
public partial class App : Application
{

    /// <summary>
    /// App ctor: initializes resources, sets theme, and configures platform-specific startup.
    /// </summary>
    public App()
    {
        InitializeComponent();
        Application.Current.UserAppTheme = AppTheme.Light;

#if IOS || MACCATALYST
        
        // Lightweight “loading” page; the async init continues in background.
        MainPage = new NavigationPage(new ContentPage
        {
            Title = "Loading…",
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Children =
                {
                    new ActivityIndicator { IsRunning = true, IsVisible = true, HeightRequest = 32 },
                    new Label { Text = "Connecting to the bridge…", FontSize = 16, Margin = new Thickness(0,12,0,0) }
                }
            }
        });

        _ = InitIosTabsAsync();

#else

        // Default (Windows/Android): navigate directly to MainPage.
        MainPage = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"),
            BarTextColor = Color.FromArgb("#6D4C41")
        };

#endif

        // Global error logging to avoid silent task crashes.
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Console.WriteLine($"[UNHANDLED] {ex?.Message}");
        };
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Console.WriteLine($"[TASK ERROR] {e.Exception?.Message}");
            e.SetObserved();
        };
    }


#if IOS || MACCATALYST

    // IP/host of the Android bridge reachable from iOS/macOS.
    const string BridgeHost = "172.20.10.2";

    // TCP port exposed by the bridge WebSocket server.
    const int    BridgePort = 8787;

    // Path segment for the bridge endpoint.
    const string BridgePath = "/";


    /// <summary>
    /// Builds the iOS/macOS UI: discovers devices via the bridge and creates one tab per device (EXG or IMU).
    /// Shows a fallback page if no devices are active or if initialization fails.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the UI has been updated.</returns>
    private async Task InitIosTabsAsync()
    {
        try
        {
            var activeMacs = await QueryActiveMacsAsync(BridgeHost, BridgePort, BridgePath);

            if (activeMacs.Length == 0)
            {

                // No devices → show info page and return
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MainPage = new NavigationPage(new ContentPage
                    {
                        Title = "No active Shimmer",
                        Content = new Label { Text = $"No active device at ws://{BridgeHost}:{BridgePort}{BridgePath}", Padding = 20 }
                    });
                });
                return;
            }

            var tabs = new TabbedPage { Title = "Shimmer Streams" };
            foreach (var mac in activeMacs)
            {
                
                // Read device config from bridge
                var cfgMap  = await QueryConfigAsync(BridgeHost, BridgePort, BridgePath, mac);
                bool exgOn  = cfgMap.TryGetValue("exg", out var bx) && bx;

                if (exgOn)
                {
                    
                    // EXG-only page
                    var exg = new ShimmerSDK_EXG
                    {
                        BridgeHost = BridgeHost,
                        BridgePort = BridgePort,
                        BridgePath = BridgePath,
                        BridgeTargetMac = mac
                    };

                    var exgMode = await QueryExgModeAsync(BridgeHost, BridgePort, BridgePath, mac);

                    var cfgExg = new ShimmerDevice
                    {
                        ShimmerName = $"Shimmer {mac}",
                        Port1 = $"ws://{BridgeHost}:{BridgePort}{BridgePath}",
                        EnableExg = true,
                        IsExgModeECG         = exgMode == "ecg",
                        IsExgModeEMG         = exgMode == "emg",
                        IsExgModeTest        = exgMode == "test",
                        IsExgModeRespiration = exgMode == "resp"
                    };

                    var exgPage = new DataPage(exg, cfgExg) { Title = $"EXG • {mac}" };
                    tabs.Children.Add(exgPage);
                }
                else
                {
                    
                    // IMU-only page
                    var imu = new ShimmerSDK_IMU
                    {
                        EnableLowNoiseAccelerometer  = cfgMap.TryGetValue("lna",  out var b1) && b1,
                        EnableWideRangeAccelerometer = cfgMap.TryGetValue("wra",  out var b2) && b2,
                        EnableGyroscope              = cfgMap.TryGetValue("gyro", out var b3) && b3,
                        EnableMagnetometer           = cfgMap.TryGetValue("mag",  out var b4) && b4,
                        EnablePressureTemperature    = cfgMap.TryGetValue("pt",   out var b5) && b5,
                        EnableBattery                = cfgMap.TryGetValue("batt", out var b6) && b6,
                        EnableExtA6                  = cfgMap.TryGetValue("a6",   out var b7) && b7,
                        EnableExtA7                  = cfgMap.TryGetValue("a7",   out var b8) && b8,
                        EnableExtA15                 = cfgMap.TryGetValue("a15",  out var b9) && b9,

                        BridgeHost = BridgeHost,
                        BridgePort = BridgePort,
                        BridgePath = BridgePath,
                        BridgeTargetMac = mac
                    };

                    var cfgImu = new ShimmerDevice
                    {
                        ShimmerName = $"Shimmer {mac}",
                        Port1 = $"ws://{BridgeHost}:{BridgePort}{BridgePath}",

                        EnableLowNoiseAccelerometer  = imu.EnableLowNoiseAccelerometer,
                        EnableWideRangeAccelerometer = imu.EnableWideRangeAccelerometer,
                        EnableGyroscope              = imu.EnableGyroscope,
                        EnableMagnetometer           = imu.EnableMagnetometer,
                        EnablePressureTemperature    = imu.EnablePressureTemperature,
                        EnableBattery                = imu.EnableBattery,
                        EnableExtA6                  = imu.EnableExtA6,
                        EnableExtA7                  = imu.EnableExtA7,
                        EnableExtA15                 = imu.EnableExtA15
                    };

                    var imuPage = new DataPage(imu, cfgImu) { Title = $"IMU • {mac}" };
                    tabs.Children.Add(imuPage);
            
                  }
              }

                // Swap to the tabs UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MainPage = new NavigationPage(tabs)
                    {
                        BarBackgroundColor = Color.FromArgb("#F0E5D8"),
                        BarTextColor = Color.FromArgb("#6D4C41")
                    };
                });
            }
            catch (Exception ex)
            {

            // Error path → show message page
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MainPage = new NavigationPage(new ContentPage
                {
                    Title = "Error",
                    Content = new Label
                    {
                        Text = $"Initialization failed: {ex.Message}\nPlease check that the bridge is reachable.",
                        Padding = 20
                    }
                });
            });
        }
    }


    /// <summary>
    /// WebSocket call: gets the list of active Shimmer device MACs from the bridge.
    /// </summary>
    /// <param name="host">Bridge host/IP.</param>
    /// <param name="port">Bridge TCP port.</param>
    /// <param name="path">Bridge URL path (e.g., "/").</param>
    /// <returns>Array of MAC strings; empty on error or none.</returns>
    private static async Task<string[]> QueryActiveMacsAsync(string host, int port, string path)
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{path}");
        await ws.ConnectAsync(uri, default);

        // Handshake: hello → hello_ack
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}"),
                            WebSocketMessageType.Text, true, default);
        await ReceiveOneAsync(ws); // hello_ack

        // Request active devices
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"list_active\"}"),
                            WebSocketMessageType.Text, true, default);
        var raw = await ReceiveOneAsync(ws);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.GetString() == "active_devices"
                && root.TryGetProperty("macs", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {

                // Extract non-empty MACs
                return arr.EnumerateArray()
                            .Select(x => x.GetString() ?? "")
                            .Where(x => x.Length > 0)
                            .ToArray();
            }
        }
        catch {}

        return Array.Empty<string>();
    }


    /// <summary>
    /// WebSocket call: fetches per-device configuration flags from the bridge and normalizes them.
    /// </summary>
    /// <param name="host">Bridge host/IP.</param>
    /// <param name="port">Bridge TCP port.</param>
    /// <param name="path">Bridge URL path (e.g., "/").</param>
    /// <param name="mac">Target device MAC address.</param>
    /// <returns>
    /// Dictionary of feature flags (e.g., lna, wra, gyro, mag, pt, batt, a6, a7, a15, exg1, exg2, exg).
    /// Defaults to false on errors or missing fields.
    /// </returns>
    private static async Task<Dictionary<string, bool>> QueryConfigAsync(string host, int port, string path, string mac)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {

            // IMU 
            ["lna"] = false,
            ["wra"] = false,
            ["gyro"] = false,
            ["mag"] = false,
            ["pt"] = false,
            ["batt"] = false,
            ["a6"] = false,
            ["a7"] = false,
            ["a15"] = false,
            // EXG
            ["exg1"] = false,
            ["exg2"] = false,
            ["exg"] = false
        };

        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{path}");
        await ws.ConnectAsync(uri, default);

        // Handshake: hello → hello_ack
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}"),
                           WebSocketMessageType.Text, true, default);
        await ReceiveOneAsync(ws); // hello_ack

        // Request config for MAC
        var msg = $"{{\"type\":\"get_config\",\"mac\":\"{mac}\"}}";
        await ws.SendAsync(Encoding.UTF8.GetBytes(msg),
                           WebSocketMessageType.Text, true, default);

        var raw = await ReceiveOneAsync(ws);
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.GetString() == "config" &&
                root.TryGetProperty("ok", out var ok) && ok.GetBoolean() &&
                root.TryGetProperty("cfg", out var cfg))
            {
                bool Get(string name) => cfg.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

                // IMU flags
                result["lna"] = Get("EnableLowNoiseAccelerometer");
                result["wra"] = Get("EnableWideRangeAccelerometer");
                result["gyro"] = Get("EnableGyroscope");
                result["mag"] = Get("EnableMagnetometer");
                result["pt"] = Get("EnablePressureTemperature");
                result["batt"] = Get("EnableBattery");
                result["a6"] = Get("EnableExtA6");
                result["a7"] = Get("EnableExtA7");
                result["a15"] = Get("EnableExtA15");

                // EXG flag
                bool exg1 = Get("EnableExg1") || Get("EXG1Enabled");
                bool exg2 = Get("EnableExg2") || Get("EXG2Enabled");
                bool exg = Get("EnableExg") || exg1 || exg2;

                result["exg1"] = exg1;
                result["exg2"] = exg2;
                result["exg"] = exg;
            }
        }
        catch {}

        return result;
    }


    /// <summary>
    /// WebSocket call: reads the EXG operating mode for a given device; returns "" on error/unknown.
    /// </summary>
    /// <param name="host">Bridge host/IP.</param>
    /// <param name="port">Bridge TCP port.</param>
    /// <param name="path">Bridge URL path (e.g., "/").</param>
    /// <param name="mac">Target device MAC address.</param>
    /// <returns>Lower-cased mode string ("ecg", "emg", "test", "resp") or empty if unknown.</returns>
    private static async Task<string> QueryExgModeAsync(string host, int port, string path, string mac)
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{path}");
        await ws.ConnectAsync(uri, default);

        // Handshake: hello → hello_ack
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}"),
                           WebSocketMessageType.Text, true, default);
        await ReceiveOneAsync(ws); // hello_ack

        // Request config for the given MAC
        var msg = $"{{\"type\":\"get_config\",\"mac\":\"{mac}\"}}";
        await ws.SendAsync(Encoding.UTF8.GetBytes(msg),
                           WebSocketMessageType.Text, true, default);

        var raw = await ReceiveOneAsync(ws);
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.GetString() == "config" &&
                root.TryGetProperty("ok", out var ok) && ok.GetBoolean() &&
                root.TryGetProperty("cfg", out var cfg))
            {
                
                // Preferred key
                if (cfg.TryGetProperty("ExgMode", out var m) && m.ValueKind == JsonValueKind.String)
                    return (m.GetString() ?? "").ToLowerInvariant();

                // Common alternative
                if (cfg.TryGetProperty("exg_mode", out var m2) && m2.ValueKind == JsonValueKind.String)
                    return (m2.GetString() ?? "").ToLowerInvariant();
            }
        }
        catch (Exception ex)
        { 
                Console.WriteLine($"[EXG] Parse/WS error for {mac}: {ex.Message}");
                return ""; // fallback: mode unknown
        }

        return "";
    }


    /// <summary>
    /// Receives a single complete WebSocket text message (concatenates frames) as a UTF-8 string.
    /// </summary>
    /// <param name="ws">An open <see cref="ClientWebSocket"/>.</param>
    /// <returns>Message text; empty if no data before close.</returns>
    private static async Task<string> ReceiveOneAsync(ClientWebSocket ws)
    {
        var buf = new byte[8192];
        var sb = new StringBuilder();

        while (true)
        {
            var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), default);
            if (res.MessageType == WebSocketMessageType.Close) break;

            sb.Append(Encoding.UTF8.GetString(buf, 0, res.Count));
            if (res.EndOfMessage) break;
        }

        return sb.ToString();
    }

#endif

}
