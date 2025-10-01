/* 
 * LoadingPage code-behind:
 * initializes the UI; binds to the ViewModel in the constructor;
 * starts the connection in OnAppearing and handles alerts via PropertyChanged.
 */


using ShimmerInterface.Models;
using ShimmerInterface.ViewModels;


namespace ShimmerInterface.Views;


/// <summary>
/// Code-behind for the LoadingPage XAML view.
/// This page is responsible for initiating the connection process to a Shimmer device.
/// </summary>
public partial class LoadingPage : ContentPage
{
    /// <summary>
    /// Reference to the ViewModel associated with this view.
    /// The ViewModel encapsulates all logic related to device connection,
    /// user feedback, and interaction state.
    /// </summary>
    private readonly LoadingPageViewModel viewModel;

    /// <summary>
    /// Initializes the LoadingPage and establishes the data binding context with the associated ViewModel.
    /// Also subscribes to property change notifications to reactively respond to state transitions such as alerts.
    /// </summary>
    /// <param name="device">The Shimmer device selected by the user for connection.</param>
    /// <param name="completion">A TaskCompletionSource used to return the connected device instance (IMU or EXG) asynchronously to the caller.</param>
    public LoadingPage(ShimmerDevice device, TaskCompletionSource<object?> completion)
    {
        InitializeComponent();

        // Instantiate and bind the ViewModel to this page
        viewModel = new LoadingPageViewModel(device, completion);
        BindingContext = viewModel;

        // Subscribe to ViewModel property changes to react to state changes 
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Called automatically by the framework when the page becomes visible.
    /// Triggers the asynchronous connection process by executing the command exposed by the ViewModel.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Initiate device connection via the ViewModel command
        await viewModel.StartConnectionCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Reacts to property changes signaled by the ViewModel.
    /// Specifically handles the case where an alert needs to be shown to the user upon connection success or failure.
    /// The logic of when and what to show is controlled entirely by the ViewModel.
    /// </summary>
    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(viewModel.ShowAlert) && viewModel.ShowAlert)
        {
            // Display the alert as configured by the ViewModel
            await DisplayAlert(viewModel.AlertTitle, viewModel.AlertMessage, "OK");

            // Notify the ViewModel that the user has dismissed the alert
            viewModel.DismissAlertCommand.Execute(null);
        }
    }

    /// <summary>
    /// Called automatically when the page is about to disappear from view.
    /// Responsible for detaching event subscriptions to prevent memory leaks and ensure clean navigation transitions.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (viewModel != null)
        {
            // Cleanly unsubscribe from the ViewModel to avoid dangling references
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
