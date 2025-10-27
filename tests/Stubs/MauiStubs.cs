// tests/Stubs/MauiStubs.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Maui.Controls
{
    public interface INavigation
    {
        Task PushModalAsync(Page page);
        Task<Page> PopModalAsync();
    }

    public class Page
    {
        public INavigation Navigation { get; set; } = new StubNavigation();
        public virtual Task DisplayAlert(string title, string message, string cancel) => Task.CompletedTask;
        public string? Title { get; set; }
    }

    public class TabbedPage : Page
    {
        public List<Page> Children { get; } = new();
    }

    public class NavigationPage : Page
    {
        public Page Root { get; }
        public NavigationPage(Page root) { Root = root; }
    }

    public class Application
    {
        public static Application? Current { get; set; }
        public Page? MainPage { get; set; }
    }

    // App fittizia (come in MAUI: App : Application)
    public class App : Application { }

    // Navigazione finta per i test
    public sealed class StubNavigation : INavigation
    {
        private readonly Stack<Page> _stack = new();
        public List<Page> Pushed { get; } = new();
        public int PopCount { get; private set; }

        public Task PushModalAsync(Page page)
        {
            Pushed.Add(page);
            _stack.Push(page);
            return Task.CompletedTask;
        }

        public Task<Page> PopModalAsync()
        {
            PopCount++;
            return Task.FromResult(_stack.Pop());
        }
    }

    // MainPage di test che cattura le alert
    public sealed class TestMainPage : Page
    {
        public (string title, string message, string cancel)? LastAlert { get; private set; }
        public override Task DisplayAlert(string title, string message, string cancel)
        {
            LastAlert = (title, message, cancel);
            return Task.CompletedTask;
        }
    }
}
