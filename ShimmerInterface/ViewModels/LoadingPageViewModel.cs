using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XR2Learn_ShimmerAPI;
using XR2Learn_ShimmerAPI.IMU;
using ShimmerInterface.Models;

namespace ShimmerInterface.ViewModels;

public partial class LoadingPageViewModel : ObservableObject
{
    private readonly ShimmerDevice device;

    // Proprietà che indicano se i sensori sono abilitati
    public bool EnableLowNoiseAccelerometer => device.EnableLowNoiseAccelerometer;
    public bool EnableWideRangeAccelerometer => device.EnableWideRangeAccelerometer;
    public bool EnableGyroscope => device.EnableGyroscope;
    public bool EnableMagnetometer => device.EnableMagnetometer;
    public bool EnablePressureTemperature => device.EnablePressureTemperature;
    public bool EnableBattery => device.EnableBattery;
    public bool EnableExtA6 => device.EnableExtA6;
    public bool EnableExtA7 => device.EnableExtA7;
    public bool EnableExtA15 => device.EnableExtA15;

    // Costruttore: riceve il dispositivo selezionato
    public LoadingPageViewModel(ShimmerDevice device)
    {
        this.device = device;
    }

    // Metodo asincrono che prova a connettersi al dispositivo Shimmer specificato.
    // Usa la porta specifica del dispositivo selezionato.
    // Ritorna l'istanza se la connessione ha successo, altrimenti null.
    public async Task<XR2Learn_ShimmerIMU?> ConnectAsync()
    {
        try
        {
            var shimmer = new XR2Learn_ShimmerIMU
            {
                EnableLowNoiseAccelerometer = EnableLowNoiseAccelerometer,
                EnableWideRangeAccelerometer = EnableWideRangeAccelerometer,
                EnableGyroscope = EnableGyroscope,
                EnableMagnetometer = EnableMagnetometer,
                EnablePressureTemperature = EnablePressureTemperature,
                EnableBattery = EnableBattery,
                EnableExtA6 = EnableExtA6,
                EnableExtA7 = EnableExtA7,
                EnableExtA15 = EnableExtA15
            };

            shimmer.Configure("Shimmer3", device.Port1,
                EnableLowNoiseAccelerometer,
                EnableWideRangeAccelerometer,
                EnableGyroscope,
                EnableMagnetometer,
                EnablePressureTemperature,
                EnableBattery,
                EnableExtA6,
                EnableExtA7,
                EnableExtA15);

            shimmer.Connect();

            if (shimmer.IsConnected())
            {
                shimmer.StartStreaming();
                return shimmer;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SHIMMER ERROR] on {device.Port1}: {ex.Message}");
        }

        return null;
    }
}