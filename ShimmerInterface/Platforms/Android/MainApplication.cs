/* 
 * MAUI Android Application class.
 * Hosts the app process and builds the MauiApp in CreateMauiApp(). 
 */


using Android.App;
using Android.Runtime;


namespace ShimmerInterface
{
    [Application]
    public class MainApplication : MauiApplication
    {

        /// <summary>
        /// Required ctor for Android runtime (JNI handle + ownership).
        /// </summary>
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }


        /// <summary>
        /// Builds and returns the MAUI app instance.
        /// </summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
