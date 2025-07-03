using ShimmerInterface.ViewModels;

namespace ShimmerInterface.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainPageViewModel();
    }
}
