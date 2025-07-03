using ShimmerInterface.Views;

namespace ShimmerInterface
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Set MainPage inside a NavigationPage to allow page navigation
            MainPage = new NavigationPage(new MainPage());
        }
    }
}
