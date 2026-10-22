using System;
using StremioXbox.Services;
using StremioXbox.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace StremioXbox
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        public static Frame RootFrame
        {
            get { return Window.Current.Content as Frame; }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Draw edge to edge; pages apply TvSafeMargin themselves so backgrounds can bleed into overscan.
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);

            var rootFrame = RootFrame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                    rootFrame.Navigate(typeof(ShellPage), e.Arguments);
                Window.Current.Activate();
            }
        }

        // The controller's B button arrives here.
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            var root = RootFrame;
            if (root == null)
                return;

            if (root.Content is ShellPage shell && shell.TryGoBack())
            {
                e.Handled = true;
            }
            else if (root.CanGoBack)
            {
                root.GoBack();
                e.Handled = true;
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            DisplayModeService.RestoreDefault();
            deferral.Complete();
        }
    }
}
