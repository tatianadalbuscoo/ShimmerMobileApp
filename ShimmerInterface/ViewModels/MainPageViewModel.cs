using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI.IMU;
using XR2Learn_ShimmerAPI;
using System.Diagnostics;

#if WINDOWS
using System.Management;
#endif

namespace ShimmerInterface.ViewModels;

/// <summary>
/// ViewModel for the main page. Handles Shimmer device selection and connection
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    /// List of all available Shimmer devices detected on serial ports
    public ObservableCollection<ShimmerDevice> AvailableDevices { get; } = new();

    // Command to connect to selected Shimmer devices
    public IRelayCommand<INavigation> ConnectCommand { get; }

    // Command to refresh the list of available devices.
    public IRelayCommand RefreshDevicesCommand { get; }

    // Internal list to keep track of connected Shimmer instances
    private List<(XR2Learn_ShimmerIMU shimmer, ShimmerDevice device)> connectedShimmers = new();

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
    private void LoadDevices()
    {
        AvailableDevices.Clear();

        // Get all available serial ports
        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        // Get Shimmer names from WMI (Windows only)
        var shimmerNames = GetShimmerNamesFromWMI();

        foreach (var port in ports)
        {
            // Try to get the Shimmer name for this port
            if (shimmerNames.ContainsKey(port))
            {
                string shimmerName = shimmerNames[port];

                // Only add devices with known Shimmer names (skip "Unknown")
                if (shimmerName != "Unknown")
                {
                    string displayName = $"Shimmer {shimmerName}";

                    AvailableDevices.Add(new ShimmerDevice
                    {
                        DisplayName = displayName,
                        Port1 = port,
                        IsSelected = false,
                        ShimmerName = shimmerName
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extracts Shimmer names from Windows WMI for Bluetooth COM ports.
    /// Returns a dictionary mapping COM port to Shimmer name.
    /// </summary>
    private Dictionary<string, string> GetShimmerNamesFromWMI()
    {
        var shimmerNames = new Dictionary<string, string>();

#if WINDOWS
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString();
                    string deviceId = obj["DeviceID"]?.ToString();

                    if (name == null || deviceId == null) continue;
                    if (!name.Contains("Bluetooth") || !name.Contains("COM")) continue;

                    // Extract COM port (e.g., "COM4")
                    int start = name.LastIndexOf("(COM");
                    int end = name.IndexOf(")", start);
                    if (start < 0 || end <= start) continue;

                    string comPort = name.Substring(start + 1, end - start - 1);

                    // Extract Shimmer name
                    string shimmerName = ExtractShimmerName(deviceId, name);
                    
                    if (shimmerName != "Unknown")
                    {
                        shimmerNames[comPort] = shimmerName;
                        Debug.WriteLine($"Found Shimmer: {comPort} => {shimmerName}");
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
    /// Extracts the Shimmer name from DeviceID or friendly name
    /// </summary>
    private string ExtractShimmerName(string deviceId, string friendlyName)
    {
        // Try to find "Shimmer3-XXXX" pattern in friendly name
        if (!string.IsNullOrEmpty(friendlyName))
        {
            var shimmerIndex = friendlyName.IndexOf("Shimmer3-");
            if (shimmerIndex >= 0)
            {
                var startIndex = shimmerIndex + "Shimmer3-".Length;
                if (friendlyName.Length >= startIndex + 4)
                {
                    var candidate = friendlyName.Substring(startIndex, 4);
                    if (IsHexString(candidate))
                    {
                        return candidate.ToUpper();
                    }
                }
            }
        }

        // Fallback: extract from DeviceID using regex pattern
        if (!string.IsNullOrEmpty(deviceId))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                deviceId,
                @"&00066680([A-F0-9]{4})_",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.ToUpper();
            }
        }

        return "Unknown";
    }

    /// <summary>
    /// Checks if a string contains only hexadecimal characters
    /// </summary>
    private bool IsHexString(string str)
    {
        foreach (char c in str)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Connects to all selected devices and creates a tabbed interface
    /// </summary>
    private async Task Connect(INavigation nav)
    {
        var selectedDevices = AvailableDevices.Where(d => d.IsSelected).ToList();
        if (!selectedDevices.Any())
        {
            await App.Current.MainPage.DisplayAlert("Error", "Please select at least one Shimmer device", "OK");
            return;
        }

        connectedShimmers.Clear();

        foreach (var device in selectedDevices)
        {
            var tcs = new TaskCompletionSource<XR2Learn_ShimmerIMU>();
            var loadingPage = new LoadingPage(device, tcs);

            await Application.Current.MainPage.Navigation.PushModalAsync(loadingPage);
            var shimmer = await tcs.Task;
            await Application.Current.MainPage.Navigation.PopModalAsync();

            if (shimmer != null)
            {
                connectedShimmers.Add((shimmer, device));
            }
        }

        // Create tabbed interface for connected devices
        if (connectedShimmers.Any())
        {
            CreateTabbedPage();
        }
    }

    /// <summary>
    /// Creates a tabbed page with one tab per connected Shimmer device
    /// </summary>
    private void CreateTabbedPage()
    {
        var tabbedPage = new TabbedPage();

        foreach (var (shimmer, device) in connectedShimmers)
        {
            var sensorConfig = new SensorConfiguration
            {
                EnableLowNoiseAccelerometer = device?.EnableLowNoiseAccelerometer ?? true,
                EnableWideRangeAccelerometer = device?.EnableWideRangeAccelerometer ?? true,
                EnableGyroscope = device?.EnableGyroscope ?? true,
                EnableMagnetometer = device?.EnableMagnetometer ?? true,
                EnableBattery = device?.EnableBattery ?? true,
                EnablePressureTemperature = device?.EnablePressureTemperature ?? true,
                EnableExtA6 = device?.EnableExtA6 ?? true,
                EnableExtA7 = device?.EnableExtA7 ?? true,
                EnableExtA15 = device?.EnableExtA15 ?? true
            };

            var dataPage = new DataPage(shimmer, sensorConfig);

            string tabTitle = !string.IsNullOrEmpty(device?.ShimmerName) && device.ShimmerName != "Unknown"
                ? $"Shimmer {device.ShimmerName}"
                : $"Shimmer {connectedShimmers.IndexOf((shimmer, device)) + 1}";

            dataPage.Title = tabTitle;
            tabbedPage.Children.Add(dataPage);
        }

        Application.Current.MainPage = new NavigationPage(tabbedPage);
    }
}