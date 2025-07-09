using ShimmerInterface.Views;

namespace ShimmerInterface;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Create NavigationPage with back button hidden
        var navigationPage = new NavigationPage(new MainPage());

        navigationPage.BarBackgroundColor = Color.FromArgb("#F0E5D8"); // Champagne
        navigationPage.BarTextColor = Color.FromArgb("#6D4C41"); // Marrone intenso


        MainPage = navigationPage;
    }
}