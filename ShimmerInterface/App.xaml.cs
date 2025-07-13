using ShimmerInterface.Views;

namespace ShimmerInterface;

public partial class App : Application
{
    public App()
    {
        // Inizializza i componenti definiti in App.xaml (risorse globali)
        InitializeComponent();

        // Crea una pagina di navigazione con MainPage come pagina iniziale
        // e imposta i colori della barra superiore (Barra di Navigazione)
        var navigationPage = new NavigationPage(new MainPage())
        {
            BarBackgroundColor = Color.FromArgb("#F0E5D8"), // Champagne
            BarTextColor = Color.FromArgb("#6D4C41")        // Marrone intenso
        };

        // Imposta la NavigationPage come pagina principale dell'app
        MainPage = navigationPage;

        // Registra un gestore globale per le eccezioni non gestite del dominio (es. crash non previsti)
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Console.WriteLine($"[UNHANDLED] {ex?.Message}");
        };

        // Registra un gestore per eccezioni non osservate nei task asincroni (es. await dimenticati)
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Console.WriteLine($"[TASK ERROR] {e.Exception?.Message}");
            e.SetObserved(); // Segnala che l'eccezione è stata gestita
        };
    }
}
