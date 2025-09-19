using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Maui.ApplicationModel; // per MainThread.InvokeOnMainThreadAsync
using System.Linq;
using System.Threading.Tasks;

#if ANDROID
using ShimmerSDK;
#endif



#if WINDOWS
using System.Management;
using ShimmerSDK;
#endif

namespace ShimmerInterface.ViewModels;


/// <summary>
/// ViewModel for the main page. Handles Shimmer device selection and connection.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{

    // List of all available Shimmer devices detected on serial ports
    public ObservableCollection<ShimmerDevice> AvailableDevices { get; } = new();

    // Internal list to keep track of connected Shimmer instances
    private readonly List<(object shimmer, ShimmerDevice device)> connectedShimmers = new();

    // NEW: stato per overlay/avviso durante il refresh
    [ObservableProperty] private bool isRefreshing;

    // Testo mostrato nell'overlay (refresh/scan iniziale)
    [ObservableProperty] private string overlayMessage = "Refreshing devices…";


    // Command to connect to selected Shimmer devices
    public IRelayCommand<INavigation> ConnectCommand { get; }

    // Command to refresh the list of available devices.
    public IRelayCommand RefreshDevicesCommand { get; }


    /// <summary>
    /// Constructor: initializes commands and loads devices on startup.
    /// </summary>
   /* public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        RefreshDevicesCommand = new AsyncRelayCommand(LoadDevicesAsync); // ← era RelayCommand
        _ = LoadDevicesAsync(); // ← carica subito
    }*/

    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync); // <-- usa il wrapper
        _ = InitialScanAsync(); // start CON overlay "Scanning devices…"
    }

    private async Task InitialScanAsync()
    {
        try
        {
            OverlayMessage = "Scanning devices…"; // <<< testo diverso all'avvio
            IsRefreshing = true;

            // Mostra subito l'overlay prima di iniziare lo scan
            await Task.Yield();
            await Task.Delay(50);

            await LoadDevicesAsync();
        }
        catch (Exception ex)
        {
            await App.Current!.MainPage!.DisplayAlert("Initial scan failed", ex.Message, "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }


    private async Task RefreshDevicesAsync()
    {
        if (IsRefreshing)
        {
            await App.Current!.MainPage!.DisplayAlert("Please wait", "A device refresh is already in progress.", "OK");
            return;
        }

        try
        {
            OverlayMessage = "Refreshing devices…"; // <<< NEW
            IsRefreshing = true;

            await Task.Yield();
            await Task.Delay(50);

            await LoadDevicesAsync();
            // opzionale: OK finale
            // await App.Current!.MainPage!.DisplayAlert("Refresh complete", "Device list is up to date.", "OK");
        }
        catch (Exception ex)
        {
            await App.Current!.MainPage!.DisplayAlert("Refresh failed", ex.Message, "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }





    private async Task LoadDevicesAsync()
    {
#if WINDOWS
    // Esegui scoperta e detection su thread in background
    var devices = await Task.Run(async () =>
    {
        var list = new List<ShimmerDevice>();

        var ports = SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        var shimmerNames = GetShimmerNamesFromWMI();

        foreach (var port in ports)
        {
            if (!shimmerNames.TryGetValue(port, out string? shimmerName)) continue;
            if (shimmerName == "Unknown") continue;

            var device = new ShimmerDevice
            {
                DisplayName = $"Shimmer {shimmerName}",
                Port1 = port,
                ShimmerName = shimmerName,

                IsSelected = false,
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

            // Rileva board kind (EXG vs IMU) – anche questo off-UI-thread
            try
            {
                var (ok, kind, raw) = await ShimmerSensorScanner.GetExpansionBoardKindWindowsAsync(device.DisplayName, port);

                device.IsExg      = ok && kind == ShimmerSensorScanner.BoardKind.EXG;
                device.BoardRawId = raw;
                device.RightBadge = ok ? (device.IsExg ? "EXG" : "IMU") : "device off";
            }
            catch
            {
                device.IsExg = false;
                device.RightBadge = "device off";
            }

            list.Add(device);
        }

        return list;
    });

    // Aggiorna la ObservableCollection SOLO sul MainThread
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
        AvailableDevices.Clear();
        foreach (var d in devices)
            AvailableDevices.Add(d);
    });

#elif MACCATALYST || IOS
        // Bridge mode (come prima)
        AvailableDevices.Clear();
        AvailableDevices.Add(new ShimmerDevice
        {
            DisplayName = "Bridge mode (iOS/Mac) — apri DataPage",
            Port1 = "(gestito da App → DataPage)",
            ShimmerName = "Bridge",
            IsSelected = false,
            ChannelsDisplay = "(n/a)",
            IsExg = false,

            EnableLowNoiseAccelerometer = true,
            EnableWideRangeAccelerometer = true,
            EnableGyroscope = true,
            EnableMagnetometer = true,
            EnablePressureTemperature = true,
            EnableBattery = true,
            EnableExtA6 = true,
            EnableExtA7 = true,
            EnableExtA15 = true
        });

#elif ANDROID
    AvailableDevices.Clear();
    try
    {
        var adapter = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
        if (adapter == null)
        {
            AvailableDevices.Add(new ShimmerDevice { DisplayName = "Bluetooth not available", Port1 = "(no adapter)", ShimmerName = "----" });
        }
        else if (!adapter.IsEnabled)
        {
            AvailableDevices.Add(new ShimmerDevice { DisplayName = "Bluetooth disabled", Port1 = "(enable it from settings)", ShimmerName = "----" });
        }
        else
        {
            var bonded = adapter.BondedDevices;
            var any = false;

            // IMPORTANT: evita blocchi UI
            foreach (var d in bonded)
            {
                var name = d?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!name.Contains("Shimmer", StringComparison.OrdinalIgnoreCase)) continue;

                any = true;
                var mac = d!.Address;
                var shimmerName = ExtractShimmerName(deviceId: string.Empty, friendlyName: name);

                var (ok, kind, raw) = await ShimmerSensorScanner.GetExpansionBoardKindAndroidAsync("scan", mac);

                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = name,
                    Port1 = mac,
                    ShimmerName = shimmerName,
                    BoardRawId = raw,
                    IsExg = ok && kind == ShimmerSensorScanner.BoardKind.EXG,

                    // ⬇️ prima: "unknown" –> ora "device off"
                    RightBadge = ok
                        ? (kind == ShimmerSensorScanner.BoardKind.EXG ? "EXG" : "IMU")
                        : "device off",

                    ChannelsDisplay = ok
                        ? (kind == ShimmerSensorScanner.BoardKind.EXG ? "EXG" : "IMU")
                        : "(off)",

                    EnableLowNoiseAccelerometer = true,
                    EnableWideRangeAccelerometer = true,
                    EnableGyroscope = true,
                    EnableMagnetometer = true,
                    EnablePressureTemperature = true,
                    EnableBattery = true,
                    EnableExtA6 = true,
                    EnableExtA7 = true,
                    EnableExtA15 = true
                });

            }

            if (!any)
            {
                AvailableDevices.Add(new ShimmerDevice { DisplayName = "No Shimmer paired", Port1 = "(Pair in settings)",  ShimmerName = "----" });
            }
        }
    }
    catch (Exception ex)
    {
        AvailableDevices.Add(new ShimmerDevice { DisplayName = "Bluetooth Error", Port1 = ex.Message, ShimmerName = "----" });
    }

#else
    Console.WriteLine("No supported platforms.");
#endif
    }






    /// <summary>
    /// Connects to all selected Shimmer devices and, if successful,
    /// opens a tabbed interface with a data page for each device.
    /// </summary>
    /// <param name="nav">The navigation object used to manage page transitions.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task Connect(INavigation? nav)
    {

        // Get all devices that the user has selected
        var selectedDevices = AvailableDevices.Where(d => d.IsSelected).ToList();

        // If no device is selected, show an error message and stop
        if (selectedDevices.Count == 0)
        {
            await App.Current!.MainPage!.DisplayAlert("Error", "Please select at least one Shimmer device", "OK");
            return;
        }

        // Clear the list of previously connected devices
        connectedShimmers.Clear();

        // Loop through each selected device and try to connect
        foreach (var device in selectedDevices)
        {

            // Create a TaskCompletionSource to wait for the device to be initialized
            var tcs = new TaskCompletionSource<object?>();

            // Show a loading page to initialize the connection
            var loadingPage = new LoadingPage(device, tcs);
            await Application.Current!.MainPage!.Navigation.PushModalAsync(loadingPage);


            // Wait for the loading page to finish connecting
            var shimmer = await tcs.Task;

            // Close the loading page after connection attempt
            await Application.Current.MainPage.Navigation.PopModalAsync();

            // Se la connessione è riuscita, aggiungi alla lista
            if (shimmer != null)
            {
                connectedShimmers.Add((shimmer, device));
            }
            else
            {
                // >>> NEW: marca subito come device off e avvisa
                device.RightBadge = "device off";
                await App.Current!.MainPage!.DisplayAlert(
                    "Device off",
                    $"{device.DisplayName} appears to be powered off. Turn it on and try again.",
                    "OK"
                );
            }

        }

        // If at least one device was connected, create the tabbed interface
        if (connectedShimmers.Count > 0)
        {
            CreateTabbedPage();
        }
    }


    private void CreateTabbedPage()
    {
        var tabbedPage = new TabbedPage();
        foreach (var (shimmer, device) in connectedShimmers)
        {
            string TitleFor(ShimmerDevice d, int index) =>
                !string.IsNullOrEmpty(d?.ShimmerName) && d.ShimmerName != "Unknown"
                    ? $"Shimmer {d.ShimmerName}"
                    : $"Shimmer {index + 1}";


#if WINDOWS || ANDROID
    if (shimmer is XR2Learn_ShimmerAPI.IMU.XR2Learn_ShimmerIMU sImu)
    {
        var page = new DataPage(sImu, device);
        page.Title = TitleFor(device, connectedShimmers.IndexOf((shimmer, device)));
        tabbedPage.Children.Add(page);
    }
    else if (shimmer is XR2Learn_ShimmerAPI.GSR.XR2Learn_ShimmerEXG sExg)
    {
        var page = new DataPage(sExg, device); // costruttore EXG in DataPage
        page.Title = TitleFor(device, connectedShimmers.IndexOf((shimmer, device))) + " (EXG)";
        tabbedPage.Children.Add(page);
    }
#else
            // Android/iOS: solo IMU
            if (shimmer is XR2Learn_ShimmerAPI.IMU.XR2Learn_ShimmerIMU sImuDroid)
            {
                var page = new DataPage(sImuDroid, device);
                page.Title = TitleFor(device, connectedShimmers.IndexOf((shimmer, device)));
                tabbedPage.Children.Add(page);
            }
            // (se mai capitasse un EXG su Android, qui lo ignoriamo)
#endif

        }


        if (Application.Current != null)
        {
            Application.Current.MainPage = new NavigationPage(tabbedPage);
        }
    }



    ///// Windows ///////////////////////////////////////////////////////////////////////////////////////////////////////////


    /// <summary>
    /// Extracts Shimmer names from Windows WMI for Bluetooth COM ports.
    /// Only includes known devices (excludes "Unknown").
    /// </summary>
    /// <returns>
    /// Dictionary mapping COM port (e.g., "COM15") to Shimmer name (e.g., "DDCE").
    /// </returns>
    private static Dictionary<string, string> GetShimmerNamesFromWMI()
    {

        // Create a dictionary to hold the mapping: COM port -> Shimmer name
        var shimmerNames = new Dictionary<string, string>();

#if WINDOWS
        try
        {

            // Use WMI to search for all plug-and-play devices whose names include "(COM"
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {

                    // Read the Name and DeviceID of each matching device
                    string name = obj["Name"]?.ToString() ?? "";
                    string? deviceId = obj["DeviceID"]?.ToString();

                    // Skip this entry if name or device ID is missing
                    if (name == null || deviceId == null) continue;

                    // Only consider Bluetooth devices that are mapped to a COM port
                    if (!name.Contains("Bluetooth") || !name.Contains("COM")) continue;

                    // Extract the COM port name from the string, e.g., from "Bluetooth Device (COM4)" => "COM4"
                    int start = name.LastIndexOf("(COM");
                    int end = name.IndexOf(")", start);
                    if (start < 0 || end <= start) continue;

                    string comPort = name.Substring(start + 1, end - start - 1); // "COM4", "COM5", etc.

                    // Extract Shimmer name
                    string shimmerName = ExtractShimmerName(deviceId, name);
                    
                    // Only include valid Shimmer names (ignore "Unknown")
                    if (shimmerName != "Unknown")
                    {
                        shimmerNames[comPort] = shimmerName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI Error: {ex.Message}");
        }
#endif

        return shimmerNames;
    }


    /// <summary>
    /// Extracts the Shimmer name from DeviceID or friendly name.
    /// Looks for a pattern like "Shimmer3-XXXX" in the name,
    /// or falls back to a hexadecimal ID in the DeviceID string.
    /// </summary>
    /// <param name="deviceId">The full device identifier string from the system (e.g., from WMI).</param>
    /// <param name="friendlyName">The user-friendly device name shown by Windows (e.g., "Shimmer3-DDCE (COM4)").</param>
    /// <returns>
    /// The 4-character uppercase Shimmer ID (e.g., "DDCE"), or "Unknown" if not found.
    /// </returns>
    private static string ExtractShimmerName(string deviceId, string friendlyName)
    {
        // Try to find "Shimmer3-XXXX" pattern in friendly name
        if (!string.IsNullOrEmpty(friendlyName))
        {
            // Find the position of "Shimmer3-" in the name
            var shimmerIndex = friendlyName.IndexOf("Shimmer3-");
            if (shimmerIndex >= 0)
            {

                // Get the substring right after "Shimmer3-" (expecting a 4-character ID)
                var startIndex = shimmerIndex + "Shimmer3-".Length;

                // Ensure there are at least 4 characters after that
                if (friendlyName.Length >= startIndex + 4)
                {
                    var candidate = friendlyName.Substring(startIndex, 4);

                    // If it's a valid hex string (e.g., "DDCE"), return it in uppercase
                    if (IsHexString(candidate))
                    {
                        return candidate.ToUpper();
                    }
                }
            }
        }

        // If not found in name, try extracting from the DeviceID using regex
        if (!string.IsNullOrEmpty(deviceId))
        {

            // Match the DeviceID against the expected regex pattern
            var match = DeviceIdRegex().Match(deviceId);

            // If the pattern matches, return the extracted value in uppercase
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }
        }

        // If no valid name could be found, return "Unknown"
        return "Unknown";
    }


    /// <summary>
    /// Checks if a string contains only hexadecimal characters (0–9, A–F, a–f).
    /// </summary>
    /// <param name="str">The input string to validate.</param>
    /// <returns>
    /// True if the string contains only valid hexadecimal characters; otherwise, false.
    /// </returns>
    private static bool IsHexString(string str)
    {

        // Loop through each character in the input string
        foreach (char c in str)
        {
            // If any character is not a valid hexadecimal digit, return false
            if (!Uri.IsHexDigit(c))
                return false;
        }

        // All characters are valid hex digits
        return true;
    }








    /// <summary>
    /// Provides a regular expression to extract a 4-character hexadecimal identifier
    /// from a DeviceID string that matches the pattern '&amp;00066680XXXX_',  where XXXX are hexadecimal digits.
    /// The matching is case-insensitive.
    /// </summary>
    [GeneratedRegex(@"&00066680([A-F0-9]{4})_", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceIdRegex();

}

