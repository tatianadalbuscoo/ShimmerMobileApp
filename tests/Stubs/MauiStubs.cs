/* 
 * MauiStubs.cs
 * Purpose: Cross-OS, no-UI stubs that mimic a minimal subset of .NET MAUI
 *          types to drive unit tests without bringing in the real MAUI framework.
 */


using System.Collections.Generic;
using System.Threading.Tasks;


namespace Microsoft.Maui.Controls
{

    /// <summary>
    /// Minimal navigation contract exposing modal push/pop operations.
    /// </summary>
    public interface INavigation
    {

        /// <summary>
        /// Pushes a page modally onto the navigation stack.
        /// </summary>
        /// <param name="page">The page to present modally.</param>
        /// <returns>A task that completes when the operation is recorded.</returns>
        Task PushModalAsync(Page page);


        /// <summary>
        /// Pops the top-most modal page from the navigation stack.
        /// </summary>
        /// <returns>A task producing the popped <see cref="Page"/>.</returns>
        Task<Page> PopModalAsync();
    }


    /// <summary>
    /// Lightweight stand-in for <c>Microsoft.Maui.Controls.Page</c>.
    /// Provides a navigation property and a test-friendly alert method.
    /// </summary>
    public class Page
    {

        /// <summary>
        /// Navigation adapter for modal operations. Defaults to <see cref="StubNavigation"/>.
        /// </summary>
        public INavigation Navigation { get; set; } = new StubNavigation();


        /// <summary>
        /// Displays an alert to the user.
        /// Test stub implementation completes immediately without UI.
        /// </summary>
        /// <param name="title">Alert title.</param>
        /// <param name="message">Alert message.</param>
        /// <param name="cancel">Cancel button label.</param>
        /// <returns>A completed task.</returns>
        public virtual Task DisplayAlert(string title, string message, string cancel) => Task.CompletedTask;


        /// <summary>
        /// Page title (not used by the stub runtime, but useful in assertions).
        /// </summary>
        public string? Title { get; set; }
    }


    /// <summary>
    /// Minimal tabbed container that collects child <see cref="Page"/> instances.
    /// </summary>
    public class TabbedPage : Page
    {

        /// <summary>
        /// The collection of child pages displayed in tabs.
        /// </summary>
        public List<Page> Children { get; } = new();
    }


    /// <summary>
    /// Minimal navigation page that wraps a single root <see cref="Page"/>.
    /// </summary>
    public class NavigationPage : Page
    {

        /// <summary>
        /// Initializes a new <see cref="NavigationPage"/> with the given root.
        /// </summary>
        /// <param name="root">The root page.</param>
        public NavigationPage(Page root) { Root = root; }


        /// <summary>
        /// The root page contained by this navigation wrapper.
        /// </summary>
        public Page Root { get; }

    }


    /// <summary>
    /// Application singleton placeholder with a <see cref="MainPage"/>.
    /// </summary>
    public class Application
    {

        /// <summary>
        /// Gets or sets the current application instance.
        /// Tests can set this to simulate MAUI's global <c>Application.Current</c>.
        /// </summary>
        public static Application? Current { get; set; }


        /// <summary>
        /// Gets or sets the logical main page of the application.
        /// </summary>
        public Page? MainPage { get; set; }
    }


    /// <summary>
    /// Trivial application subclass for test scenarios (mirrors <c>App : Application</c>).
    /// </summary>
    public class App : Application { }


    /// <summary>
    /// In-memory navigation stack used in tests to track modal push/pop calls.
    /// </summary>
    public sealed class StubNavigation : INavigation
    {

        private readonly Stack<Page> _stack = new();


        /// <summary>
        /// List of pages that have been pushed modally, in push order.
        /// </summary>

        public List<Page> Pushed { get; } = new();


        /// <summary>
        /// Total number of modal pop operations performed.
        /// </summary>
        public int PopCount { get; private set; }


        /// <summary>
        /// Records a modal push by adding <paramref name="page"/> to the stack.
        /// </summary>
        /// <param name="page">The page to push.</param>
        /// <returns>A completed task.</returns>
        public Task PushModalAsync(Page page)
        {
            Pushed.Add(page);
            _stack.Push(page);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Records a modal pop and returns the popped page.
        /// </summary>
        /// <returns>
        /// A task producing the popped <see cref="Page"/>.
        /// </returns>
        public Task<Page> PopModalAsync()
        {
            PopCount++;
            return Task.FromResult(_stack.Pop());
        }
    }


    /// <summary>
    /// Page subclass that captures the most recent alert for assertions.
    /// </summary>
    public sealed class TestMainPage : Page
    {

        /// <summary>
        /// Tuple holding the last alert's title, message, and cancel label; <c>null</c> if none shown.
        /// </summary>
        public (string title, string message, string cancel)? LastAlert { get; private set; }


        /// <summary>
        /// Overrides alert display to record parameters without UI.
        /// </summary>
        /// <param name="title">Alert title.</param>
        /// <param name="message">Alert message.</param>
        /// <param name="cancel">Cancel button label.</param>
        /// <returns>A completed task.</returns>
        public override Task DisplayAlert(string title, string message, string cancel)
        {
            LastAlert = (title, message, cancel);
            return Task.CompletedTask;
        }
    }
}
