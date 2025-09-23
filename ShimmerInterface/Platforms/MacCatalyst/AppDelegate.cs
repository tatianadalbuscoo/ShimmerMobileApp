/* 
 * macOS AppDelegate for MAUI: bootstraps and returns the MauiApp.
 */


using Foundation;


namespace ShimmerInterface
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
