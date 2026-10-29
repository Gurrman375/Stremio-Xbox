using StremioXbox.Services;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StremioXbox.Views
{
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDisplayMargins();

            if (Nav.SelectedItem == null)
                Nav.SelectedItem = Nav.MenuItems[0];
        }

        // Back from a detail page returns within the content frame before leaving the shell.
        public bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;
            ContentFrame.GoBack();
            return true;
        }

        private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
            Type page;
            switch (tag)
            {
                case "search": page = typeof(SearchPage); break;
                case "library": page = typeof(LibraryPage); break;
                case "settings": page = typeof(SettingsPage); break;
                default: page = typeof(BoardPage); break;
            }

            if (ContentFrame.CurrentSourcePageType != page)
            {
                ContentFrame.Navigate(page);
                // Top-level tabs don't stack; back should leave detail pages only.
                ContentFrame.BackStack.Clear();
            }
        }

        public static void ShowPlayer(PlayerArgs args)
        {
            App.RootFrame.Navigate(typeof(PlayerPage), args);
        }

        public void ApplyDisplayMargins()
        {
            Nav.Margin = AppServices.Settings.UseTvSafeMargin
                ? new Thickness(48, 27, 48, 27)
                : new Thickness(0);
        }
    }
}
