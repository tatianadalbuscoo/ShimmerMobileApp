using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShimmerInterface.Models;
using ShimmerInterface.Views;
using XR2Learn_ShimmerAPI.IMU;
using XR2Learn_ShimmerAPI;

#if WINDOWS
using System.Management;
#endif

namespace ShimmerInterface.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    public ObservableCollection<ShimmerDevice> AvailableDevices { get; } = new();
    public IRelayCommand<INavigation> ConnectCommand { get; }
    public IRelayCommand RefreshDevicesCommand { get; }

    // Lista per tenere traccia degli Shimmer connessi
    private List<(XR2Learn_ShimmerIMU shimmer, ShimmerDevice device)> connectedShimmers = new();


    // Costruttore: inizializza i comandi e carica i dispositivi disponibili
    public MainPageViewModel()
    {
        ConnectCommand = new AsyncRelayCommand<INavigation>(Connect);
        RefreshDevicesCommand = new RelayCommand(LoadDevices);
        LoadDevices();
    }

    // Classe per rappresentare un dispositivo Shimmer Bluetooth scoperto
    public class DiscoveredShimmerDevice
    {
        public string ComPort { get; set; }
        public string FriendlyName { get; set; }
        public string ShimmerName { get; set; }
        public string BluetoothAddress { get; set; }
    }

    // Carica i dispositivi Shimmer disponibili via Bluetooth
    private void LoadDevices()
    {
        AvailableDevices.Clear();

        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        var shimmerDevices = GetShimmerBluetoothDevices();

        var deviceMap = shimmerDevices
            .GroupBy(d => d.ComPort)
            .ToDictionary(g => g.Key, g => g.First());

        for (int i = 0; i < ports.Count - 1; i += 2)
        {
            var port1 = ports[i];
            var port2 = ports[i + 1];

            var name1 = deviceMap.ContainsKey(port1) ? deviceMap[port1].ShimmerName : "Unknown";
            var name2 = deviceMap.ContainsKey(port2) ? deviceMap[port2].ShimmerName : "Unknown";

            string shimmerName;

            if (name1 != "Unknown" && name1 == name2)
                shimmerName = name1;
            else if (name1 != "Unknown" && name2 != "Unknown" && name1 != name2)
                shimmerName = $"{name1}/{name2}";
            else if (name1 != "Unknown")
                shimmerName = name1;
            else if (name2 != "Unknown")
                shimmerName = name2;
            else
                shimmerName = "Unknown";

            string displayName = $"Shimmer {shimmerName} ({port1} + {port2})";

            string btAddress = deviceMap.ContainsKey(port1) ? deviceMap[port1].BluetoothAddress :
                               deviceMap.ContainsKey(port2) ? deviceMap[port2].BluetoothAddress : null;

            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = displayName,
                Port1 = port1,
                Port2 = port2,
                IsSelected = false,
                ShimmerName = shimmerName,
                BluetoothAddress = btAddress
            });

        }

        // Porta dispari alla fine
        if (ports.Count % 2 != 0)
        {
            var lastPort = ports.Last();
            var name = deviceMap.ContainsKey(lastPort) ? deviceMap[lastPort].ShimmerName : "Unknown";
            var bt = deviceMap.ContainsKey(lastPort) ? deviceMap[lastPort].BluetoothAddress : null;

            string displayName = $"Shimmer {name} ({lastPort})";

            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = displayName,
                Port1 = lastPort,
                Port2 = null,
                IsSelected = false,
                ShimmerName = name,
                BluetoothAddress = bt
            });
        }
    }



    // Metodo legacy per compatibilità - accoppia le porte seriali due a due
    private void LoadDevicesLegacy()
    {
        AvailableDevices.Clear();

        var ports = XR2Learn_SerialPortsManager
            .GetAvailableSerialPortsNames()
            .OrderBy(p => p)
            .ToList();

        int deviceIndex = 1;

        for (int i = 0; i < ports.Count - 1; i += 2)
        {
            var port1 = ports[i];
            var port2 = ports[i + 1];

            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = $"Shimmer Device {deviceIndex} ({port1} + {port2})",
                Port1 = port1,
                Port2 = port2,
                IsSelected = false,
                ShimmerName = $"D{deviceIndex}",
                BluetoothAddress = null
            });

            deviceIndex++;
        }

        // Gestisci la porta singola non accoppiata se il numero è dispari
        if (ports.Count % 2 != 0)
        {
            var lastPort = ports.Last();
            AvailableDevices.Add(new ShimmerDevice
            {
                DisplayName = $"Shimmer Device {deviceIndex} ({lastPort})",
                Port1 = lastPort,
                Port2 = null,
                IsSelected = false,
                ShimmerName = $"D{deviceIndex}",
                BluetoothAddress = null
            });
        }
    }



    /// <summary>
    /// Migliora i nomi delle porte seriali usando metodi alternativi quando WMI non è disponibile
    /// </summary>
    private List<DiscoveredShimmerDevice> EnhancePortsWithNames(List<string> ports)
    {
        var devices = new List<DiscoveredShimmerDevice>();

        foreach (var port in ports)
        {
            var device = new DiscoveredShimmerDevice
            {
                ComPort = port,
                FriendlyName = port,
                ShimmerName = TryExtractShimmerNameFromPort(port),
                BluetoothAddress = null
            };

            devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// Prova a estrarre il nome Shimmer da una porta seriale usando pattern comuni
    /// </summary>
    private string TryExtractShimmerNameFromPort(string port)
    {
        // Per ora ritorna "Unknown", ma qui potresti implementare
        // logica per estrarre nomi da registry o altre fonti
        // basandoti sul numero di porta COM
        return "Unknown";
    }

    /// <summary>
    /// Trova tutti i dispositivi Shimmer Bluetooth con i loro nomi estratti.
    /// Funziona solo su Windows con WMI.
    /// </summary>
    private List<DiscoveredShimmerDevice> GetShimmerBluetoothDevices()
    {
        var devices = new List<DiscoveredShimmerDevice>();

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

                if (name.Contains("Bluetooth") && name.Contains("COM"))
                {
                    int start = name.LastIndexOf("(COM");
                    int end = name.IndexOf(")", start);
                    if (start > 0 && end > start)
                    {
                        string comPort = name.Substring(start + 1, end - start - 1); // COM9
                        string shimmerName = ExtractShimmerName(deviceId, name);
                        string btAddress = ExtractBluetoothAddress(deviceId);

                        devices.Add(new DiscoveredShimmerDevice
                        {
                            ComPort = comPort,
                            FriendlyName = name,
                            ShimmerName = shimmerName,
                            BluetoothAddress = btAddress
                        });

                        System.Diagnostics.Debug.WriteLine($"🟢 COM {comPort} => Shimmer {shimmerName}  [DeviceID: {deviceId}]");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Errore WMI: {ex.Message}");
    }
#endif

        return devices;
    }


    /// <summary>
    /// Estrae il nome Shimmer dal DeviceID o dal nome del dispositivo
    /// </summary>
    private string ExtractShimmerName(string deviceId, string friendlyName)
    {
        // 1. Prova con "Shimmer3-XXXX"
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
                        System.Diagnostics.Debug.WriteLine($" ShimmerName from FriendlyName: {candidate}");
                        return candidate.ToUpper();
                    }
                }
            }
        }

        // 2. Fallback: cerca "XXXX" finale tipo DDCE o E123 dal DeviceID
        if (!string.IsNullOrEmpty(deviceId))
        {
            var match = System.Text.RegularExpressions.Regex.Match(deviceId, @"&00066680([A-F0-9]{4})_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var id = match.Groups[1].Value;
                System.Diagnostics.Debug.WriteLine($" ShimmerName from DeviceID fallback: {id}");
                return id.ToUpper();
            }
        }

        System.Diagnostics.Debug.WriteLine($" Failed to extract ShimmerName from: {deviceId} / {friendlyName}");
        return "Unknown";
    }






    /// <summary>
    /// Estrae l'indirizzo Bluetooth dal DeviceID
    /// </summary>
    private string ExtractBluetoothAddress(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return null;

        var devIndex = deviceId.IndexOf("DEV_");
        if (devIndex >= 0)
        {
            var macAddress = deviceId.Substring(devIndex + 4);
            var nextUnderscore = macAddress.IndexOf("\\");
            if (nextUnderscore > 0)
            {
                macAddress = macAddress.Substring(0, nextUnderscore);
            }

            if (macAddress.Length >= 12)
            {
                // Formatta come indirizzo MAC (XX:XX:XX:XX:XX:XX)
                return string.Join(":", Enumerable.Range(0, 6)
                    .Select(i => macAddress.Substring(i * 2, 2))
                    .ToArray());
            }
        }

        return null;
    }

    /// <summary>
    /// Verifica se una stringa è esadecimale
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

    // Connette tutti i dispositivi selezionati mostrando una schermata di caricamento per ciascuno
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

        // Dopo aver connesso tutti i dispositivi, crea la TabbedPage
        if (connectedShimmers.Any())
        {
            CreateTabbedPage();
        }
    }

    // Crea una TabbedPage in cui ogni scheda rappresenta un dispositivo Shimmer connesso
    // Crea una nuova interfaccia a schede (TabbedPage) dove ogni tab rappresenta un dispositivo Shimmer connesso.
    // Per ciascun dispositivo:
    // - Trova la configurazione selezionata dall'utente (accelerometro, GSR, PPG)
    // - Crea una pagina DataPage per visualizzare i dati del sensore
    // - Imposta un titolo personalizzato basato sul nome Shimmer se disponibile
    // Infine, imposta la TabbedPage come nuova MainPage per mostrare subito i grafici live.

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

        // Questa riga è essenziale per vedere la tabbed page!
        Application.Current.MainPage = new NavigationPage(tabbedPage);
    }



}