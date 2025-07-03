using XR2Learn_ShimmerAPI;
using System.Threading;

namespace ShimmerInterface;

// Page that shows sensor data
public partial class DataPage : ContentPage
{
    // Shimmer device
    private XR2Learn_ShimmerGSR shimmer;

    // Timer for periodic updates
    private Timer? timer;

    public DataPage(XR2Learn_ShimmerGSR shimmerDevice)
    {
        InitializeComponent();
        shimmer = shimmerDevice;

        // Start reading data
        StartReceivingData();
    }

    private void StartReceivingData()
    {
        // 1 second update
        var period = TimeSpan.FromMilliseconds(1000);

        timer = new Timer((e) =>
        {
            var data = shimmer.LatestData;
            if (data == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update the label with latest sensor data
                DataLabel.Text = $"[{data.TimeStamp.Data}] {data.AcceleratorX.Data} [{data.AcceleratorX.Unit}] | " +
                                 $"{data.AcceleratorY.Data} [{data.AcceleratorY.Unit}] | {data.AcceleratorZ.Data} [{data.AcceleratorZ.Unit}]\n" +
                                 $"{data.GalvanicSkinResponse.Data} [{data.GalvanicSkinResponse.Unit}] | " +
                                 $"{data.PhotoPlethysmoGram.Data} [{data.PhotoPlethysmoGram.Unit}] | " +
                                 $"{data.HeartRate} [BPM]";
            });
        }, null, TimeSpan.Zero, period);
    }
}
