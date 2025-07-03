using XR2Learn_ShimmerAPI;

namespace ShimmerInterface
{
    public partial class LoadingPage : ContentPage
    {

        private readonly bool enableAccelerometer;
        private readonly bool enableGSR;
        private readonly bool enablePPG;

        public LoadingPage(bool enableAccelerometer, bool enableGSR, bool enablePPG)
        {
            InitializeComponent();
            this.enableAccelerometer = enableAccelerometer;
            this.enableGSR = enableGSR;
            this.enablePPG = enablePPG;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Small delay to show spinner
            await Task.Delay(500);

            // Start connecting
            await ConnectToDevice();
        }

        private async Task ConnectToDevice()
        {
            try
            {
                string[] ports = XR2Learn_SerialPortsManager.GetAvailableSerialPortsNames();
                if (ports.Length == 0)
                {
                    await DisplayAlert("Error", "No COM ports found", "OK");

                    // Go back if no ports
                    await Navigation.PopAsync();
                    return;
                }

                string selectedPort = ports[0];

                var shimmer = new XR2Learn_ShimmerGSR();

                // Configure enabled sensors
                shimmer.EnableAccelerator = enableAccelerometer;
                shimmer.EnableGSR = enableGSR;
                shimmer.EnablePPG = enablePPG;

                shimmer.Configure("Shimmer3", selectedPort);

                shimmer.Connect();

                if (shimmer.IsConnected())
                {
                    shimmer.StartStreaming();

                    // Move to DataPage
                    await Navigation.PushAsync(new DataPage(shimmer));

                    // Remove LoadingPage from stack
                    Navigation.RemovePage(this);
                }
                else
                {
                    await DisplayAlert("Error", "Connection failed", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Exception", ex.Message, "OK");
                await Navigation.PopAsync();
            }
        }
    }
}
