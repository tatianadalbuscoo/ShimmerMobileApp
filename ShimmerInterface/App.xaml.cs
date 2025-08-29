using ShimmerInterface.Views;

#if IOS || MACCATALYST
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel; // MainThread
using XR2Learn_ShimmerAPI.IMU;
using ShimmerInterface.Models;
#endif

namespace ShimmerInterface;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Application.Current.UserAppTheme = AppTheme.Light;

#if IOS || MACCATALYST
        // 1) pagina di attesa
        MainPage = new NavigationPage(new ContentPage
        {
            Title = "Loading…",
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Children =
                {
                    new ActivityIndicator { IsRunning = true, IsVisible = true, HeightRequest = 32 },
                    new Label { Text = "Connessione al bridge…", FontSize = 16, Margin = new Thickness(0,12,0,0) }
                }
            }
        });

        // 2) avvio init async (non si può await nel costruttore)
        _ = InitIosTabsAsync();
#else
        // flusso originale (Windows/Android)
        MainPage = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"),
            BarTextColor = Color.FromArgb("#6D4C41")
        };
#endif

        // error handlers globali
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
    // Parametri bridge
    const string BridgeHost = "172.20.10.2";
    const int    BridgePort = 8787;
    const string BridgePath = "/";

    private async Task InitIosTabsAsync()
    {
        try
        {
            var activeMacs = await QueryActiveMacsAsync(BridgeHost, BridgePort, BridgePath);

            if (activeMacs.Length == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MainPage = new NavigationPage(new ContentPage
                    {
                        Title = "No active Shimmer",
                        Content = new Label { Text = $"Nessun device attivo su ws://{BridgeHost}:{BridgePort}{BridgePath}", Padding = 20 }
                    });
                });
                return;
            }

            var tabs = new TabbedPage { Title = "Shimmer Streams" };

            foreach (var mac in activeMacs)
            {
                // 1) chiedi la config reale al server
                var cfgMap = await QueryConfigAsync(BridgeHost, BridgePort, BridgePath, mac);

                // 2) IMU con flag informativi in base alla config del server
                var imu = new XR2Learn_ShimmerIMU
                {
                    EnableLowNoiseAccelerometer  = cfgMap.TryGetValue("lna", out var b1) && b1,
                    EnableWideRangeAccelerometer = cfgMap.TryGetValue("wra", out var b2) && b2,
                    EnableGyroscope              = cfgMap.TryGetValue("gyro", out var b3) && b3,
                    EnableMagnetometer           = cfgMap.TryGetValue("mag", out var b4) && b4,
                    EnablePressureTemperature    = cfgMap.TryGetValue("pt",  out var b5) && b5,
                    EnableBattery                = cfgMap.TryGetValue("batt",out var b6) && b6,
                    EnableExtA6                  = cfgMap.TryGetValue("a6",  out var b7) && b7,
                    EnableExtA7                  = cfgMap.TryGetValue("a7",  out var b8) && b8,
                    EnableExtA15                 = cfgMap.TryGetValue("a15", out var b9) && b9,

                    BridgeHost = BridgeHost,
                    BridgePort = BridgePort,
                    BridgePath = BridgePath,
                    BridgeTargetMac = mac // subscribe a questo MAC
                };

                // 3) ShimmerDevice coerente con i flag IMU (DataPage crea le serie in base a questi)
                var cfg = new ShimmerDevice
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

                var page = new DataPage(imu, cfg) { Title = mac };
                tabs.Children.Add(page);
            }

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
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MainPage = new NavigationPage(new ContentPage
                {
                    Title = "Errore",
                    Content = new Label
                    {
                        Text = $"Init fallito: {ex.Message}\nControlla che il bridge sia raggiungibile.",
                        Padding = 20
                    }
                });
            });
        }
    }

    private static async Task<string[]> QueryActiveMacsAsync(string host, int port, string path)
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{path}");
        await ws.ConnectAsync(uri, default);

        // hello
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}"),
                           WebSocketMessageType.Text, true, default);
        await ReceiveOneAsync(ws); // hello_ack

        // list_active
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
                return arr.EnumerateArray()
                          .Select(x => x.GetString() ?? "")
                          .Where(x => x.Length > 0)
                          .ToArray();
            }
        }
        catch { /* ignore parse errors */ }

        return Array.Empty<string>();
    }

    private static async Task<Dictionary<string,bool>> QueryConfigAsync(string host, int port, string path, string mac)
    {
        var result = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["lna"] = false, ["wra"] = false, ["gyro"] = false, ["mag"] = false,
            ["pt"] = false, ["batt"] = false, ["a6"] = false, ["a7"] = false, ["a15"] = false
        };

        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{path}");
        await ws.ConnectAsync(uri, default);

        // hello
        await ws.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}"),
                           WebSocketMessageType.Text, true, default);
        await ReceiveOneAsync(ws); // hello_ack

        // get_config
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
                result["lna"]  = Get("EnableLowNoiseAccelerometer");
                result["wra"]  = Get("EnableWideRangeAccelerometer");
                result["gyro"] = Get("EnableGyroscope");
                result["mag"]  = Get("EnableMagnetometer");
                result["pt"]   = Get("EnablePressureTemperature");
                result["batt"] = Get("EnableBattery");
                result["a6"]   = Get("EnableExtA6");
                result["a7"]   = Get("EnableExtA7");
                result["a15"]  = Get("EnableExtA15");
            }
        }
        catch { /* ignore parse errors; default = all false */ }

        return result;
    }

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
