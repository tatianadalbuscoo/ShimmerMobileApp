using CommunityToolkit.Mvvm.ComponentModel;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.ViewModels;

public partial class DataPageViewModel : ObservableObject
{
    private readonly XR2Learn_ShimmerGSR shimmer;

    // Usa il tipo completamente qualificato per evitare ambiguità
    private readonly System.Timers.Timer timer = new(1000);

    [ObservableProperty]
    private string sensorText = "Waiting for data...";

    public DataPageViewModel(XR2Learn_ShimmerGSR shimmerDevice)
    {
        shimmer = shimmerDevice;
        StartTimer();
    }

    private void StartTimer()
    {
        timer.Elapsed += (s, e) =>
        {
            var data = shimmer.LatestData;
            if (data == null) return;

            SensorText = $"[{data.TimeStamp.Data}] {data.AcceleratorX.Data} [{data.AcceleratorX.Unit}] | " +
                         $"{data.AcceleratorY.Data} [{data.AcceleratorY.Unit}] | {data.AcceleratorZ.Data} [{data.AcceleratorZ.Unit}]\n" +
                         $"{data.GalvanicSkinResponse.Data} [{data.GalvanicSkinResponse.Unit}] | " +
                         $"{data.PhotoPlethysmoGram.Data} [{data.PhotoPlethysmoGram.Unit}] | {data.HeartRate} [BPM]";
        };

        timer.Start();
    }
}
