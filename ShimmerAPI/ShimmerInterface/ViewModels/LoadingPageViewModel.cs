using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XR2Learn_ShimmerAPI;
using ShimmerInterface.Models;

namespace ShimmerInterface.ViewModels;

public partial class LoadingPageViewModel : ObservableObject
{
    // Proprietà che indicano se i sensori sono abilitati (Accelerometro, GSR, PPG)
    public bool EnableAccelerometer { get; }
    public bool EnableGSR { get; }
    public bool EnablePPG { get; }

    // Costruttore: salva le impostazioni dei sensori da abilitare
    public LoadingPageViewModel(bool accel, bool gsr, bool ppg)
    {
        EnableAccelerometer = accel;
        EnableGSR = gsr;
        EnablePPG = ppg;
    }

    // Metodo asincrono che prova a connettersi a un dispositivo Shimmer.
    // Se trova almeno una porta disponibile, configura e connette il dispositivo.
    // Ritorna l’istanza se la connessione ha successo, altrimenti null.
    public async Task<XR2Learn_ShimmerGSR?> ConnectAsync()
    {
        string[] ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();
        if (ports.Length == 0)
            return null;

        var shimmer = new XR2Learn_ShimmerGSR
        {
            EnableAccelerator = EnableAccelerometer,
            EnableGSR = EnableGSR,
            EnablePPG = EnablePPG
        };

        shimmer.Configure("Shimmer3", ports[0]);
        shimmer.Connect();

        if (shimmer.IsConnected())
        {
            shimmer.StartStreaming();
            return shimmer;
        }

        return null;
    }
}
