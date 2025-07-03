using ShimmerInterface.ViewModels;
using XR2Learn_ShimmerAPI;

namespace ShimmerInterface.Views;

public partial class DataPage : ContentPage
{
    public DataPage(XR2Learn_ShimmerGSR shimmer)
    {
        InitializeComponent();
        BindingContext = new DataPageViewModel(shimmer);
    }
}
