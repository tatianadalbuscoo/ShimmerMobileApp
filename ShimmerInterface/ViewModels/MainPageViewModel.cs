/*
 * MainPageViewModel — MAUI MVVM
 * Discovers Shimmer devices (Windows: serial/WMI; Android: bonded Bluetooth),
 * exposes refresh/connect commands with overlay feedback,
 * and builds tabbed DataPages for connected devices.
 * LoadingPage is invoked here by ConnectCommand (per selected device) on Windows/Android.
 * Note: iOS/macOS use the WebSocket bridge path; this VM (and LoadingPage here) are not used there.
 */


using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using ShimmerSDK;
using System.Text.RegularExpressions;


#if WINDOWS
using System.Management;
#endif


namespace ShimmerInterface.ViewModels;


/// <summary>
/// ViewModel for the main devices screen.
/// Discovers Shimmer devices (Windows: serial/WMI; Android: bonded Bluetooth),
/// exposes the list via <see cref="AvailableDevices"/>, and provides commands to
/// refresh (<see cref="RefreshDevicesCommand"/>) and connect (<see cref="ConnectCommand"/>).
/// Manages the refresh overlay state (<see cref="isRefreshing"/>, <see cref="overlayMessage"/>),
/// orchestrates per-device connection via a modal loading page, and builds a tabbed UI for
/// each connected device.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{

    // List of all available Shimmer devices detected on serial ports
    public ObservableCollection<ShimmerDevice> AvailableDevices { get; } = new();

    // Internal list to keep track of connected Shimmer instances
    private readonly List<(object shimmer, ShimmerDevice device)> connectedShimmers = new();

    // Command to connect to selected Shimmer devices
    public IRelayCommand<INavigation> ConnectCommand { get; }

    // Command to refresh the list of available devices.
    public IRelayCommand RefreshDevicesCommand { get; }

    // overlay/notice state while refreshing
    [ObservableProperty] private bool isRefreshing;

    // Text displayed in the overlay (initial scan or refresh)
    [ObservableProperty] private string overlayMessage = "Refreshing devices…";


    /// <summary>
    /// Constructor: initializes a new instance of the <see cref="MainPageViewModel"/> class,
    /// wires commands, and starts the initial device scan with overlay.
    /// </summary>
    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync);
        _ = InitialScanAsync();
    }


    /// <summary>
    /// Performs the initial device scan:
    /// sets the overlay state/message, yields to the UI to show it,
    /// runs the discovery, and restores state even on error.
    /// </summary>
    /// <returns>A task that represents the asynchronous scan operation.</returns>
    private async Task InitialScanAsync()
    {
        try
        {

            // Set the overlay text for the very first scan and show the busy state.
            OverlayMessage = "Scanning devices…";
            IsRefreshing = true;

            // Give the UI thread a chance to render the overlay before the scan starts.
            await Task.Yield();
            await Task.Delay(50);

            // Discover and populate the device list.
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


    /// <summary>
    /// Refreshes the list of available devices:
    /// guards against re-entrancy, shows the overlay, yields to the UI,
    /// performs discovery, and clears the busy state on exit.
    /// </summary>
    /// <returns>A task that represents the asynchronous refresh operation.</returns>
    private async Task RefreshDevicesAsync()
    {

        // Prevent concurrent refreshes to avoid UI glitches and race conditions.
        if (IsRefreshing)
        {
            await App.Current!.MainPage!.DisplayAlert("Please wait", "A device refresh is already in progress.", "OK");
            return;
        }

        try
        {

            // Set overlay message and switch on the busy state.
            OverlayMessage = "Refreshing devices…";
            IsRefreshing = true;

            // Give the UI a chance to render the overlay before scanning.
            await Task.Yield();
            await Task.Delay(50);

            // Perform the actual discovery/population.
            await LoadDevicesAsync();
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


    /// <summary>
    /// Discovers available Shimmer devices on Windows and Android, then updates
    /// <see cref="AvailableDevices"/> on the UI thread.
    /// </summary>
    /// <returns>A task representing the asynchronous discovery and UI update.</returns>
    private async Task LoadDevicesAsync()
    {

#if WINDOWS

        // Run the whole scan off the UI thread.
        var devices = await Task.Run(async () =>
        {
            var list = new List<ShimmerDevice>();

            // Enumerate COM ports in a stable order for deterministic UI.
            var ports = SerialPortsManager
                .GetAvailableSerialPortsNames()
                .OrderBy(p => p)
                .ToList();

            // Resolve COM → ShimmerName ("DDCE", "E0D9", ...) using WMI.
            var shimmerNames = GetShimmerNamesFromWMI();

            // Build device rows and probe board kind (EXG vs IMU).
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

                // Sensor expansion board kind (off UI thread).
                try
                {
                    var (ok, kind, raw) = await ShimmerSensorScanner.GetExpansionBoardKindWindowsAsync(device.DisplayName, port);

                    device.IsExg      = ok && kind == ShimmerSensorScanner.BoardKind.EXG;
                    device.BoardRawId = raw;
                    device.RightBadge = ok ? (device.IsExg ? "EXG" : "IMU") : "device off";
                }
                catch
                {

                    // Mark as off.
                    device.IsExg = false;
                    device.RightBadge = "device off";
                }

                list.Add(device);
            }

            return list;
        });

        // Publish results to the ObservableCollection on the main thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            AvailableDevices.Clear();
            foreach (var d in devices)
                AvailableDevices.Add(d);
        });


#elif ANDROID

        // Start from a clean slate on each refresh.
        AvailableDevices.Clear();
    try
    {
        var adapter = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;

        // Basic adapter checks → inform the user in the list itself.
        if (adapter == null)
        {
            AvailableDevices.Add(new ShimmerDevice 
            { 
                DisplayName = "Bluetooth not available", 
                Port1 = "(no adapter)", 
                ShimmerName = "----" 
            });
        }
        else if (!adapter.IsEnabled)
        {
            AvailableDevices.Add(new ShimmerDevice {
                DisplayName = "Bluetooth disabled",
                Port1 = "(enable it from settings)",
                ShimmerName = "----" 
            });
        }
        else
        {

            // Use paired devices; filter those whose Name contains "Shimmer".
            var bonded = adapter.BondedDevices;
            var any = false;

            foreach (var d in bonded)
            {
                if (d is null) continue;

                var name = d.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!name.Contains("Shimmer", StringComparison.OrdinalIgnoreCase)) continue;

                any = true;

                var mac = d.Address ?? string.Empty;
                if (string.IsNullOrWhiteSpace(mac)) continue; 


                // Try to extract the 4-char Shimmer ID from the friendly name if present.
                var shimmerName = ExtractShimmerName(deviceId: string.Empty, friendlyName: name);

                var (ok, kind, raw) = await ShimmerSensorScanner.GetExpansionBoardKindAndroidAsync("scan", mac);

                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = name,
                    Port1 = mac,
                    ShimmerName = shimmerName,
                    BoardRawId = raw,
                    IsExg = ok && kind == ShimmerSensorScanner.BoardKind.EXG,

                    // Badge mirrors EXG/IMU or "device off" when device fails.
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

            // If none matched, add a “pair first” row.
            if (!any)
            {
                AvailableDevices.Add(new ShimmerDevice 
                { 
                    DisplayName = "No Shimmer paired",
                    Port1 = "(Pair in settings)",
                    ShimmerName = "----" }
                );
            }
        }
    }
    catch (Exception ex)
    {
        AvailableDevices.Add(new ShimmerDevice 
        { 
            DisplayName = "Bluetooth Error",
            Port1 = ex.Message,
            ShimmerName = "----" 
        });
    }

#endif

    }


    /// <summary>
    /// Connects to all selected Shimmer devices. For each selected item it shows a modal
    /// loading page that performs the connection and returns the created Shimmer instance.
    /// If at least one device connects successfully, a tabbed UI is created.
    /// </summary>
    /// <param name="nav">The navigation object used to manage page transitions.</param>
    /// <returns>A task representing the asynchronous connection workflow.</returns>
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

            // If connection is successful, add to list
            if (shimmer != null)
            {
                connectedShimmers.Add((shimmer, device));
            }
            else
            {

                // Failure path: mark the device as off and notify the user.
                device.RightBadge = "device off";
                await App.Current!.MainPage!.DisplayAlert(
                    "Device off",
                    $"{device.DisplayName} appears to be powered off. Turn it on and try again.",
                    "OK"
                );
            }

        }

        // If at least one device connected, proceed to build the tabbed UI.
        if (connectedShimmers.Count > 0)
        {
            CreateTabbedPage();
        }
    }


    /// <summary>
    /// Builds a tabbed UI with one <see cref="DataPage"/> per connected device
    /// and sets it as the application's main page.
    /// </summary>
    private void CreateTabbedPage()
    {

        // Create the tab container that will host one page per device.
        var tabbedPage = new TabbedPage();

        // For each connected (shimmer instance, device metadata) tuple, add a tab.
        foreach (var (shimmer, device) in connectedShimmers)
        {
            string TitleFor(ShimmerDevice d, int index) =>
                !string.IsNullOrEmpty(d?.ShimmerName) && d.ShimmerName != "Unknown"
                    ? $"Shimmer {d.ShimmerName}"
                    : $"Shimmer {index + 1}";

            // IMU path: create a DataPage bound to the IMU shimmer instance.
            if (shimmer is ShimmerSDK.IMU.ShimmerSDK_IMU sImu)
            {

                // DataPage IMU constructor
                var page = new DataPage(sImu, device);
                page.Title = TitleFor(device, connectedShimmers.IndexOf((shimmer, device)));
                tabbedPage.Children.Add(page);
            }

            // EXG path: create a DataPage bound to the EXG shimmer instance.
            else if (shimmer is ShimmerSDK.EXG.ShimmerSDK_EXG sExg)
            {

                // DataPage EXG constructor
                var page = new DataPage(sExg, device);
                page.Title = TitleFor(device, connectedShimmers.IndexOf((shimmer, device))) + " (EXG)";
                tabbedPage.Children.Add(page);
            }
        }

        // Swap the app shell to the newly built tabs (wrapped in a NavigationPage for a top bar).
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
        catch (Exception)
        {}

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

        if (string.IsNullOrEmpty(str)) return false;

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
