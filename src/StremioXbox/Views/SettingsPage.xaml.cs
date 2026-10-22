using System;
using System.Linq;
using Stremio.Core.Api;
using StremioXbox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StremioXbox.Views
{
    public sealed partial class SettingsPage : Page
    {
        // ISO 639-2 codes, as used by the OpenSubtitles addon.
        private static readonly string[] Languages =
        {
            "eng", "spa", "por", "fre", "ger", "ita", "dut", "pol", "rus", "tur", "ara", "hin", "jpn", "kor", "chi",
        };

        private bool _initializing;

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var settings = AppServices.Settings;
            _initializing = true;
            ServerUrl.Text = settings.StreamingServerUrl ?? "";
            SubtitleLang.ItemsSource = Languages;
            SubtitleLang.SelectedItem = Languages.Contains(settings.PreferredSubtitleLanguage) ? settings.PreferredSubtitleLanguage : "eng";
            Passthrough.IsOn = settings.AudioPassthrough;
            MatchDisplay.IsOn = settings.MatchDisplayMode;
            _initializing = false;

            UpdateAccount();
            await AppServices.EnsureAddonsLoadedAsync();
            RefreshAddons();
        }

        private void UpdateAccount()
        {
            var loggedIn = AppServices.Settings.IsLoggedIn;
            LoginPanel.Visibility = loggedIn ? Visibility.Collapsed : Visibility.Visible;
            AccountPanel.Visibility = loggedIn ? Visibility.Visible : Visibility.Collapsed;
            AccountText.Text = "Logged in as " + AppServices.Settings.Email;
        }

        private void RefreshAddons()
        {
            AddonList.ItemsSource = AppServices.Addons.Addons.Select(a => a.Manifest).ToList();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            AccountStatus.Text = "Logging in…";
            try
            {
                var result = await AppServices.Api.LoginAsync(Email.Text.Trim(), Password.Password);
                AppServices.Settings.AuthKey = result.AuthKey;
                AppServices.Settings.Email = result.User?.Email ?? Email.Text.Trim();
                Password.Password = "";
                AccountStatus.Text = "Loading your addons…";
                await AppServices.ReloadAddonsAsync();
                AccountStatus.Text = AppServices.Addons.Addons.Count + " addons loaded from your account.";
                RefreshAddons();
            }
            catch (StremioApiException ex)
            {
                AccountStatus.Text = "Login failed: " + ex.Message;
            }
            catch (Exception ex)
            {
                AccountStatus.Text = "Couldn't reach Stremio: " + ex.Message;
            }
            finally
            {
                LoginButton.IsEnabled = true;
                UpdateAccount();
            }
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            var key = AppServices.Settings.AuthKey;
            AppServices.Settings.AuthKey = null;
            AppServices.Settings.Email = null;
            UpdateAccount();
            AccountStatus.Text = "";
            try { await AppServices.Api.LogoutAsync(key); } catch (Exception) { }
            await AppServices.ReloadAddonsAsync();
            RefreshAddons();
        }

        private void ServerUrl_LostFocus(object sender, RoutedEventArgs e)
        {
            AppServices.Settings.StreamingServerUrl = ServerUrl.Text;
        }

        private void SubtitleLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initializing && SubtitleLang.SelectedItem is string lang)
                AppServices.Settings.PreferredSubtitleLanguage = lang;
        }

        private void Passthrough_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_initializing)
                AppServices.Settings.AudioPassthrough = Passthrough.IsOn;
        }

        private void MatchDisplay_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_initializing)
                AppServices.Settings.MatchDisplayMode = MatchDisplay.IsOn;
        }

        private async void AddAddon_Click(object sender, RoutedEventArgs e)
        {
            var url = AddonUrl.Text.Trim();
            if (url.Length == 0)
                return;
            AddonStatus.Text = "Checking addon…";
            try
            {
                var manifest = await AppServices.Addons.Client.GetManifestAsync(url);
                if (manifest == null)
                {
                    AddonStatus.Text = "That URL didn't return an addon manifest.";
                    return;
                }
                var urls = AppServices.Settings.ExtraAddonUrls;
                urls.Add(url);
                AppServices.Settings.ExtraAddonUrls = urls;
                await AppServices.ReloadAddonsAsync();
                RefreshAddons();
                AddonUrl.Text = "";
                AddonStatus.Text = "Added " + manifest.Name + ".";
            }
            catch (Exception ex)
            {
                AddonStatus.Text = "Couldn't add addon: " + ex.Message;
            }
        }
    }
}
