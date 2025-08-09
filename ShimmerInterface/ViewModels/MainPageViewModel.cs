using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI.IMU;
using XR2Learn_ShimmerAPI;
using System.Diagnostics;
using System.Text.RegularExpressions;


#if WINDOWS
using System.Management;
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
    private readonly List<(XR2Learn_ShimmerIMU shimmer, ShimmerDevice device)> connectedShimmers = new();

    // Command to connect to selected Shimmer devices
    public IRelayCommand<INavigation> ConnectCommand { get; }

    // Command to refresh the list of available devices.
    public IRelayCommand RefreshDevicesCommand { get; }


    /// <summary>
    /// Constructor: initializes commands and loads devices on startup.
    /// </summary>
    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        RefreshDevicesCommand = new RelayCommand(LoadDevices);

    LoadDevices();              

    }


    /// <summary>
    /// Scans available serial ports and creates a ShimmerDevice for each one.
    /// Extracts Shimmer names from WMI when available.
    /// Only shows devices with known Shimmer names (filters out "Unknown").
    /// </summary>
    /// <summary>
    /// Scans available serial ports and creates a ShimmerDevice for each one.
    /// Extracts Shimmer names from WMI when available.
    /// Only shows devices with known Shimmer names (filters out "Unknown").
    /// </summary>
    /// <summary>
    /// Scans available serial ports and creates a ShimmerDevice for each one.
    /// Extracts Shimmer names from WMI when available.
    /// Only shows devices with known Shimmer names (filters out "Unknown").
    /// </summary>
    /// <summary>
    /// Scans available serial ports and creates a ShimmerDevice for each one.
    /// Extracts Shimmer names from WMI when available.
    /// Only shows devices with known Shimmer names (filters out "Unknown").
    /// </summary>
    private void LoadDevices()
    {
        Console.WriteLine("=== LoadDevices() INIZIATO ===");

#if WINDOWS
        Console.WriteLine("Ramo WINDOWS");
        // Clear the current list of available Shimmer devices
        AvailableDevices.Clear();

        // Get a sorted list of all available serial port names (e.g., COM3, COM4).
        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        // Retrieve a dictionary of Shimmer device names via WMI (Windows Management Instrumentation),
        // where key = COM port name, value = Shimmer device name.
        var shimmerNames = GetShimmerNamesFromWMI();

        // Loop through each detected serial port
        foreach (var port in ports)
        {
            // Check if a Shimmer name is associated with this port
            if (shimmerNames.TryGetValue(port, out string? shimmerName))
            {
                // Skip ports labeled as "Unknown"
                if (shimmerName != "Unknown")
                {

                    // Add the device to the observable list with its details
                    string displayName = $"Shimmer {shimmerName}";

                    AvailableDevices.Add(new ShimmerDevice
                    {
                        DisplayName = displayName,      // Shown to the user
                        Port1 = port,                   // Serial port name
                        IsSelected = false,             // Not selected by default
                        ShimmerName = shimmerName,      // Device identifier

                        // Set default sensor configuration
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
                    Console.WriteLine($"Aggiunto device Windows: {displayName}");
                }
            }
        }

#elif MACCATALYST
private void LoadDevices()
{
    Console.WriteLine("Ramo MACCATALYST - LoadDevices fallback");

    AvailableDevices.Clear();

    // Qui puoi mettere lo scan BLE reale in futuro
    // Per ora, fallback se non trovi nulla
    if (AvailableDevices.Count == 0)
    {
        AvailableDevices.Add(new ShimmerDevice
        {
            DisplayName = "Nessun Shimmer trovato (BLE)",
            Port1 = "",
            ShimmerName = "",
            IsSelected = false,

            // Switch sensori default (opzionale)
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
}

#else
        Console.WriteLine("Ramo ELSE - nessuna piattaforma supportata");
    // altri OS: nulla
#endif

        Console.WriteLine($"=== LoadDevices() COMPLETATO - Totale devices: {AvailableDevices.Count} ===");
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
            var tcs = new TaskCompletionSource<XR2Learn_ShimmerIMU?>();

            // Show a loading page to initialize the connection
            var loadingPage = new LoadingPage(device, tcs);
            await Application.Current!.MainPage!.Navigation.PushModalAsync(loadingPage);


            // Wait for the loading page to finish connecting
            var shimmer = await tcs.Task;

            // Close the loading page after connection attempt
            await Application.Current.MainPage.Navigation.PopModalAsync();

            // If connection was successful, add to the connected list
            if (shimmer != null)
            {
                connectedShimmers.Add((shimmer, device));
            }
        }

        // If at least one device was connected, create the tabbed interface
        if (connectedShimmers.Count > 0)
        {
            CreateTabbedPage();
        }
    }


    /// <summary>
    /// Creates a tabbed page with one tab per connected Shimmer device.
    /// Each tab hosts a DataPage displaying sensor data for a specific device.
    /// </summary>
    private void CreateTabbedPage()
    {

        // Create a new TabbedPage to hold one page per Shimmer device
        var tabbedPage = new TabbedPage();

        // Loop through all connected Shimmer devices
        foreach (var (shimmer, device) in connectedShimmers)
        {

            // Create a new DataPage to display sensor data from the connected Shimmer device
            var dataPage = new DataPage(shimmer, device);

            // Set the title of the tab: use Shimmer ID if known, otherwise use index number
            string tabTitle = !string.IsNullOrEmpty(device?.ShimmerName) && device.ShimmerName != "Unknown"
                ? $"Shimmer {device.ShimmerName}"
                : $"Shimmer {connectedShimmers.IndexOf((shimmer, device!)) + 1}";

            // Add the DataPage to the TabbedPage
            dataPage.Title = tabTitle;
            tabbedPage.Children.Add(dataPage);
        }

        // Set the new TabbedPage as the main page of the application inside a NavigationPage
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
