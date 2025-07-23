// Questa classe rappresenta la configurazione di un dispositivo Shimmer,
// includendo quali sensori (Accelerometro, GSR, PPG) sono abilitati,
// se il dispositivo è selezionato e la porta associata.
// Viene utilizzata per passare le impostazioni del sensore tra le pagine
// (ad esempio dalla selezione alla visualizzazione dei dati)
// e supporta il data binding tramite CommunityToolkit.Mvvm.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace ShimmerInterface.Models;

public partial class SensorConfiguration : ObservableObject
{
    [ObservableProperty] private string portName;

    [ObservableProperty] private bool isSelected;

    [ObservableProperty] private bool enableAccelerometer = true;
    [ObservableProperty] private bool enableWideRangeAccelerometer = true;
    [ObservableProperty] private bool enableGyroscope = true;
    [ObservableProperty] private bool enableMagnetometer = true;
    [ObservableProperty] private bool enableBattery = true;

    [ObservableProperty] private bool enableExtA6 = true;

    [ObservableProperty] private bool enableExtA7 = true;

    [ObservableProperty] private bool enableExtA15 = true;
    public string DisplayName => $"Shimmer on {PortName}";
}
