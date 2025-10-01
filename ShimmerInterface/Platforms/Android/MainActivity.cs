/* 
 * MainActivity for MAUI (Android): hosts the app and requests runtime Bluetooth permissions.
 */


using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;


namespace ShimmerInterface
{

    /// <summary>
    /// Main Android activity hosting the MAUI app and handling runtime Bluetooth permission requests.
    /// </summary>
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize
                               | ConfigChanges.Orientation
                               | ConfigChanges.UiMode
                               | ConfigChanges.ScreenLayout
                               | ConfigChanges.SmallestScreenSize
                               | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {

        // Request code for Bluetooth permission prompts
        const int RequestCodeBt = 42;


        /// <summary>
        /// Android lifecycle entry point; ensures required Bluetooth permissions are requested at startup.
        /// </summary>
        /// <param name="savedInstanceState">Previously saved instance state, or <c>null</c> on first launch.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestBtPermissionsIfNeeded();
        }


        /// <summary>
        /// Requests the appropriate set of Bluetooth permissions based on the current Android version.
        /// </summary>
        void RequestBtPermissionsIfNeeded()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var perms31 = new[]
                {
                    Manifest.Permission.BluetoothConnect,
                    Manifest.Permission.BluetoothScan
                };
                RequestMissing(perms31);
            }
            else
            {
                var permsLegacy = new[]
                {
                    Manifest.Permission.AccessFineLocation
                };
                RequestMissing(permsLegacy);
            }
        }


        /// <summary>
        /// Checks the provided permissions and triggers a system prompt for those not yet granted.
        /// </summary>
        void RequestMissing(string[] permissions)
        {
            // Su API < 23 i permessi sono grant all’install: nulla da fare
            if (!OperatingSystem.IsAndroidVersionAtLeast(23))
                return;

            var missing = new List<string>(permissions.Length);
            foreach (var p in permissions)
            {
                // Usa ContextCompat invece di this.CheckSelfPermission
                if (ContextCompat.CheckSelfPermission(this, p) != Permission.Granted)
                    missing.Add(p);
            }

            if (missing.Count > 0)
            {
                // Usa ActivityCompat invece di this.RequestPermissions
                ActivityCompat.RequestPermissions(this, missing.ToArray(), RequestCodeBt);
            }
        }
    }
}
