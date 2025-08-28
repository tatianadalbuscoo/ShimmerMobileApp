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
    // Parametri bridge: mettili se vuoi in Preferences/Config
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
                var imu = new XR2Learn_ShimmerIMU
                {
                    // flag lato client (informativi): l’hardware è gestito dal server
                    EnableLowNoiseAccelerometer = true,
                    EnableWideRangeAccelerometer = true,
                    EnableGyroscope = true,
                    EnableMagnetometer = true,
                    EnablePressureTemperature = true,
                    EnableBattery = true,
                    EnableExtA6 = true,
                    EnableExtA7 = true,
                    EnableExtA15 = true,

                    BridgeHost = BridgeHost,
                    BridgePort = BridgePort,
                    BridgePath = BridgePath,
                    BridgeTargetMac = mac // subscribe a questo MAC
                };

                var cfg = new ShimmerDevice
                {
                    ShimmerName = $"Shimmer {mac}",
                    Port1 = $"ws://{BridgeHost}:{BridgePort}{BridgePath}",
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

                // una DataPage per device (come su Android/Windows)
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
