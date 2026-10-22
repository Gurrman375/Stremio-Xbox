using System;
using System.Linq;
using Stremio.Core.Api;
using StremioXbox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StremioXbox.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!AppServices.Settings.IsLoggedIn)
            {
                ShowMessage("Log in under Settings to see your Stremio library.");
                return;
            }

            try
            {
                var items = await AppServices.Api.GetLibraryAsync(AppServices.Settings.AuthKey);
                var visible = items.Where(i => !i.Removed && !i.Temp)
                    .OrderByDescending(i => i.ModifiedTime ?? DateTime.MinValue)
                    .ToList();
                Items.ItemsSource = visible;
                if (visible.Count == 0)
                    ShowMessage("Your library is empty.");
            }
            catch (Exception ex)
            {
                ShowMessage("Couldn't load your library: " + ex.Message);
            }
            finally
            {
                Loading.IsActive = false;
                Loading.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMessage(string text)
        {
            Loading.IsActive = false;
            Message.Text = text;
            Message.Visibility = Visibility.Visible;
        }

        private void Items_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = (LibraryItem)e.ClickedItem;
            Frame.Navigate(typeof(DetailPage), new MetaArgs { Type = item.Type, Id = item.Id });
        }
    }
}
