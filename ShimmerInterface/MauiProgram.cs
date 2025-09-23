/*
* Application entry point for configuring and creating the .NET MAUI app.
* This file sets up fonts, logging, and platform-specific services such as SkiaSharp.
*/


using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;


namespace ShimmerInterface
{
    public static class MauiProgram
    {

        /// <summary>
        /// Main entry point for configuring and creating the MAUI app.
        /// Sets up fonts, logging, SkiaSharp support, and builds the app instance.
        /// </summary>
        /// <returns>The configured <see cref="MauiApp"/> instance.</returns>
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Initialize the main application (App.xaml.cs)
            builder
                .UseMauiApp<App>()

                // Enable SkiaSharp support for drawing and charts
                .UseSkiaSharp()

                // Configure custom fonts used in the app
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG

            // Enable debug logging in development mode
            builder.Logging.AddDebug();
#endif

            // Build and return the configured application instance
            return builder.Build();
        }
    }
}
