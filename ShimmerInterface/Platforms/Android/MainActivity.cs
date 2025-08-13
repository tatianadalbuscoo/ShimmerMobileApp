using Android.App;
using Android.Content.PM;
using Android.OS;

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
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestBtPermissionsIfNeeded();
        }

        const int RequestCode = 42;

        void RequestBtPermissionsIfNeeded()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var perms31 = new[]
                {
                    Android.Manifest.Permission.BluetoothConnect,
                    Android.Manifest.Permission.BluetoothScan,
                };
                RequestMissing(perms31);
            }
            else
            {
                var permsLegacy = new[]
                {
                    Android.Manifest.Permission.Bluetooth,
                    Android.Manifest.Permission.BluetoothAdmin,
                    Android.Manifest.Permission.AccessFineLocation,
                };
                RequestMissing(permsLegacy);
            }
        }

        void RequestMissing(string[] permissions)
        {
            var missing = new List<string>();
            foreach (var p in permissions)
            {
                if (CheckSelfPermission(p) != Permission.Granted)
                    missing.Add(p);
            }
            if (missing.Count > 0)
                RequestPermissions(missing.ToArray(), RequestCode);
        }
    }
}
