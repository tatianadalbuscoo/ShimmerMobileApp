using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Microsoft.Maui;     // per Platform.OnRequestPermissionsResult
using Microsoft.Maui.ApplicationModel;

namespace ShimmerInterface
{
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
        const int RequestCodeBt = 42;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Chiedi i permessi PRIMA di fare scan/connessioni/leggere MAC
            RequestBtPermissionsIfNeeded();
        }

        void RequestBtPermissionsIfNeeded()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31)) // Android 12+
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
                // Android 6–11: Location necessaria per lo scan BLE
                var permsLegacy = new[]
                {
                    Manifest.Permission.AccessFineLocation
                };
                RequestMissing(permsLegacy);
            }
        }

        void RequestMissing(string[] permissions)
        {
            var missing = new List<string>();
            foreach (var p in permissions)
                if (CheckSelfPermission(p) != Permission.Granted)
                    missing.Add(p);

            if (missing.Count > 0)
                RequestPermissions(missing.ToArray(), RequestCodeBt); // oppure ActivityCompat.RequestPermissions(this, ...)
        }

        // (Consigliato) gestisci il callback dei permessi
        public override void OnRequestPermissionsResult(
            int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            // Notifica a MAUI Essentials (se la usi per altri permessi)
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == RequestCodeBt)
            {
                // Qui puoi verificare se sono stati concessi e, se sì, proseguire con scan/connessione
                bool granted = grantResults.All(r => r == Permission.Granted);
                // TODO: se !granted mostra un toast/dialog e disabilita l’azione che richiede BT
            }
        }
    }
}
