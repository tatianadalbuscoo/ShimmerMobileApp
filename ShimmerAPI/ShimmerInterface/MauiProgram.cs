using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace ShimmerInterface
{
    public static class MauiProgram
    {
        // Metodo principale per configurare e creare l'app MAUI
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Inizializza l'app principale (App.xaml.cs)
            builder
                .UseMauiApp<App>()

                // Abilita il supporto a SkiaSharp per disegni e grafici
                .UseSkiaSharp()

                // Configura i font personalizzati usati nell'app
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            // Abilita il logging di debug in fase di sviluppo
            builder.Logging.AddDebug();
#endif
            // Costruisce e restituisce l'app configurata
            return builder.Build();
        }
    }
}

