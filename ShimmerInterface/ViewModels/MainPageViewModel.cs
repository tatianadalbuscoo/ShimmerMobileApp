using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI.IMU;
using XR2Learn_ShimmerAPI;
using System.Diagnostics;
using System.Text.RegularExpressions;

#if MACCATALYST || IOS
using CoreBluetooth;
using Foundation;
using System.Collections.Concurrent;
using System.Threading;
using CoreFoundation;
#endif



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

#if WINDOWS
    AvailableDevices.Clear();

    var ports = XR2Learn_SerialPortsManager
        .GetAvailableSerialPortsNames()
        .OrderBy(p => p)
        .ToList();

    var shimmerNames = GetShimmerNamesFromWMI();

    foreach (var port in ports)
    {
        if (shimmerNames.TryGetValue(port, out string? shimmerName))
        {
            if (shimmerName != "Unknown")
            {
                string displayName = $"Shimmer {shimmerName}";
                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = displayName,
                    Port1 = port,
                    IsSelected = false,
                    ShimmerName = shimmerName,
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
    }

#elif MACCATALYST || IOS
        Console.WriteLine("Ramo MACCATALYST/IOS - BLE scan");
        AvailableDevices.Clear();

        try
        {
            // Scansiona per ~3 secondi i dispositivi BLE
            var found = MacBleScanner.Scan(TimeSpan.FromSeconds(3));

            if (found.Count == 0)
            {
                // Nessuno trovato → mostra una card informativa
                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = "Nessun Shimmer trovato (BLE)",
                    Port1 = "(BLE non rilevato)",
                    ShimmerName = "----",
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
                });
            }
            else
            {
                foreach (var dev in found)
                {
                    string shimmerName = ExtractShimmerName(deviceId: string.Empty, friendlyName: dev.Name);

                    AvailableDevices.Add(new ShimmerDevice
                    {
                        DisplayName = dev.Name,   // es. "Shimmer3-DDCE"
                        Port1 = dev.Name,         // hint BLE name per ramo Mac
                        IsSelected = false,
                        ShimmerName = shimmerName,

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

                    Console.WriteLine($"Aggiunto device Mac BLE: {dev.Name} ({dev.Identifier})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore scan BLE: {ex.Message}");
            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = "Errore scansione BLE",
                Port1 = ex.Message,
                ShimmerName = "----",
                IsSelected = false
            });
        }

#elif ANDROID
    AvailableDevices.Clear();

    try
    {
        var adapter = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
        if (adapter == null)
        {
            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = "Bluetooth not available",
                Port1 = "(no adapter)",
                ShimmerName = "----",
                IsSelected = false
            });
        }
        else if (!adapter.IsEnabled)
        {
            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = "Bluetooth disabled\r\n",
                Port1 = "(enable it from settings)",
                ShimmerName = "----",
                IsSelected = false
            });
        }
        else
        {
            var bonded = adapter.BondedDevices;
            var any = false;
            foreach (var d in bonded)
            {

                var name = d?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!name.Contains("Shimmer", StringComparison.OrdinalIgnoreCase)) continue;

                any = true;

                // MAC address -> lo usiamo come Port1 per la Connect su Android
                var mac = d!.Address;

                // Prova a estrarre l'ID a 4 char tipo "Shimmer3-XXXX"
                var shimmerName = ExtractShimmerName(deviceId: string.Empty, friendlyName: name);

                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = name,  
                    Port1 = mac,         
                    IsSelected = false,
                    ShimmerName = shimmerName,

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
                AvailableDevices.Add(new ShimmerDevice
                {
                    DisplayName = "No Shimmer paired",
                    Port1 = "(Do the pairing in Bluetooth settings.)",
                    ShimmerName = "----",
                    IsSelected = false
                });
            }
        }
    }
    catch (Exception ex)
    {
        AvailableDevices.Add(new ShimmerDevice
        {
            DisplayName = "Bluetooth Error\r\n",
            Port1 = ex.Message,
            ShimmerName = "----",
            IsSelected = false
        });
    }

#else
    Console.WriteLine("ELSE branch - no supported platforms");
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

#if MACCATALYST || IOS
internal sealed class MacBleScanner
{
    private sealed class ScanDelegate : CBCentralManagerDelegate
    {
        public volatile bool PoweredOn = false;
        public readonly ConcurrentDictionary<string, (string Name, string Identifier)> Found = new();

        public override void UpdatedState(CBCentralManager central)
        {
            PoweredOn = central.State == CBManagerState.PoweredOn;
        }

        public override void DiscoveredPeripheral(
            CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
        {
            // Ricava il nome: preferisci peripheral.Name, fallback a local name in adv
            var name = peripheral?.Name;
            if (string.IsNullOrWhiteSpace(name) && advertisementData != null)
            {
                var localNameKey = new NSString("kCBAdvDataLocalName");
                if (advertisementData.ContainsKey(localNameKey))
                    name = advertisementData[localNameKey]?.ToString();
            }

            if (string.IsNullOrWhiteSpace(name))
                return;

            // Filtra per Shimmer (puoi allargare se serve)
            if (!name.StartsWith("Shimmer", StringComparison.OrdinalIgnoreCase))
                return;

            var id = peripheral.Identifier?.AsString() ?? Guid.NewGuid().ToString();
            Found[id] = (name, id);
        }
    }

    /// <summary>
    /// Esegue una breve scansione BLE bloccante (timeout suggerito: 3–5s).
    /// </summary>
    public static List<(string Name, string Identifier)> Scan(TimeSpan timeout)
    {
        var del = new ScanDelegate();

        // Inizializza il central sul main thread/queue
        using var central = new CBCentralManager(del, DispatchQueue.MainQueue);

        var sw = Stopwatch.StartNew();

        // Attendi PowerOn (max 5s)
        while (!del.PoweredOn && sw.Elapsed < TimeSpan.FromSeconds(5))
            Thread.Sleep(50);

        if (!del.PoweredOn)
            return new(); // Bluetooth non attivo o non disponibile

        // Avvia la scansione (nessun filtro → tutti i servizi)
        central.ScanForPeripherals((CBUUID[])null);

        // Attendi timeout raccolta
        var waitMs = (int)Math.Max(0, timeout.TotalMilliseconds);
        Thread.Sleep(waitMs);

        // Stop scan
        central.StopScan();

        return del.Found.Values.Distinct().OrderBy(x => x.Name).ToList();
    }
}
#endif
