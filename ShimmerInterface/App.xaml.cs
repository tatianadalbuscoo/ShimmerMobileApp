using ShimmerInterface.Views;
#if IOS || MACCATALYST
using XR2Learn_ShimmerAPI.IMU;
using ShimmerInterface.Models;
#endif

namespace ShimmerInterface;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Forza Light mode (come prima)
        Application.Current.UserAppTheme = AppTheme.Light;

#if IOS || MACCATALYST
        // 1) IMU configurata per il bridge WebSocket
        var imu = new XR2Learn_ShimmerIMU
        {
            // Flag sensori (come già avevi)
            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true,

            // Endpoint del bridge (HOTSPOT iPhone → IP del telefono/tablet Android con WsBridgeManager)
            BridgeHost = "172.20.10.2",
            BridgePort = 8787,
            BridgePath = "/",
            BridgeTargetMac = "00:06:66:80:E1:23"  // MAC RN-42 reale
        };

        // 2) Config “visuale” per la DataPage (mostriamo l’endpoint a schermo, ecc.)
        var cfg = new ShimmerDevice
        {
            ShimmerName = "Shimmer via Bridge",
            Port1 = $"ws://{imu.BridgeHost}:{imu.BridgePort}{imu.BridgePath}",
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

        // 3) Vai direttamente su DataPage (overload iOS/Mac che accetta imu+cfg)
        var navigationPage = new NavigationPage(new DataPage(imu, cfg))
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"),
            BarTextColor = Color.FromArgb("#6D4C41")
        };
        MainPage = navigationPage;
#else
        // FLUSSO ORIGINALE (Windows/Android): apri MainPage
        var navigationPage = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"),
            BarTextColor = Color.FromArgb("#6D4C41")
        };
        MainPage = navigationPage;
#endif

        // Handlers globali errori (come prima)
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
}
