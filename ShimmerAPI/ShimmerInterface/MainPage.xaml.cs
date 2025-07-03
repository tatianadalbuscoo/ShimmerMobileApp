namespace ShimmerInterface
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnConnectAndStartClicked(object sender, EventArgs e)
        {
            // Read user choices for sensors
            bool enableAccelerometer = AccelerometerSwitch.IsToggled;
            bool enableGSR = GSRSwitch.IsToggled;
            bool enablePPG = PPGSwitch.IsToggled;

            // Go to LoadingPage, passing sensor settings
            await Navigation.PushAsync(new LoadingPage(enableAccelerometer, enableGSR, enablePPG));
        }
    }
}

