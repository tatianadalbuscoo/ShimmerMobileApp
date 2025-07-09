using ShimmerInterface.Views;

namespace ShimmerInterface;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // NavigationPage con colori personalizzati
        var navigationPage = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"), // Champagne
            BarTextColor = Color.FromArgb("#6D4C41")        // Marrone intenso
        };

        MainPage = navigationPage;

        // Gestione globale delle eccezioni
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
