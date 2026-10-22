using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Stremio.Core.Addons;
using StremioXbox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace StremioXbox.Views
{
    public class CatalogRow
    {
        public string Title { get; set; }
        public ObservableCollection<MetaPreview> Items { get; } = new ObservableCollection<MetaPreview>();
    }

    public sealed partial class BoardPage : Page
    {
        private readonly ObservableCollection<CatalogRow> _rows = new ObservableCollection<CatalogRow>();
        private bool _loaded;

        public BoardPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            Rows.ItemsSource = _rows;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_loaded)
                return;
            _loaded = true;
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                await AppServices.EnsureAddonsLoadedAsync();
                var sources = AppServices.Addons.GetCatalogs().Where(c => !c.RequiresExtra).ToList();

                // Create rows up front so order matches the addon order, then fill them as responses arrive.
                var rows = sources.Select(s => new CatalogRow { Title = s.Title }).ToList();
                var tasks = sources.Select(async (source, i) =>
                {
                    try
                    {
                        var metas = await AppServices.Addons.Client.GetCatalogAsync(source.Addon, source.Catalog.Type, source.Catalog.Id);
                        foreach (var meta in metas.Take(30))
                        {
                            meta.Type = meta.Type ?? source.Catalog.Type;
                            rows[i].Items.Add(meta);
                        }
                        if (rows[i].Items.Count > 0 && !_rows.Contains(rows[i]))
                            _rows.Insert(_rows.Count(r => rows.IndexOf(r) < i), rows[i]);
                    }
                    catch (Exception)
                    {
                        // A broken addon shouldn't blank the whole board.
                    }
                }).ToList();
                await Task.WhenAll(tasks);

                if (_rows.Count == 0)
                    ShowMessage("No catalogs loaded. Check your connection, or log in under Settings to load your addons.");
            }
            catch (Exception ex)
            {
                ShowMessage("Couldn't load addons: " + ex.Message);
            }
            finally
            {
                Loading.IsActive = false;
                Loading.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMessage(string text)
        {
            Message.Text = text;
            Message.Visibility = Visibility.Visible;
        }

        private void Poster_ItemClick(object sender, ItemClickEventArgs e)
        {
            var meta = (MetaPreview)e.ClickedItem;
            Frame.Navigate(typeof(DetailPage), new MetaArgs { Type = meta.Type, Id = meta.Id });
        }
    }
}
