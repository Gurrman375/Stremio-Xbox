using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Stremio.Core.Addons;
using StremioXbox.Services;
using Windows.Gaming.Input;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace StremioXbox.Views
{
    public class CatalogRow
    {
        public int Index { get; set; }
        public string Title { get; set; }

        public ObservableCollection<MetaPreview> Items { get; } =
            new ObservableCollection<MetaPreview>();
    }

    public sealed partial class BoardPage : Page
    {
        private readonly ObservableCollection<CatalogRow> _catalogs =
            new ObservableCollection<CatalogRow>();

        private readonly Dictionary<int, int> _titleIndexes =
            new Dictionary<int, int>();

        private readonly DispatcherTimer _gamepadTimer;

        private bool _loaded;
        private bool _verticalInputHeld;
        private int _topCatalogIndex;
        private int _activeVisibleRow;

        private static readonly SolidColorBrush SelectedPosterBrush =
            new SolidColorBrush(
                Color.FromArgb(255, 192, 32, 255));

        private static readonly SolidColorBrush TransparentBrush =
            new SolidColorBrush(Colors.Transparent);

        public BoardPage()
        {
            InitializeComponent();

            NavigationCacheMode =
                NavigationCacheMode.Enabled;

            _gamepadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            _gamepadTimer.Tick += GamepadTimer_Tick;
        }

        protected override async void OnNavigatedTo(
            NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _verticalInputHeld = false;
            _gamepadTimer.Start();

            if (_loaded)
            {
                RestoreCurrentSelection();
                return;
            }

            _loaded = true;
            await LoadAsync();
        }

        protected override void OnNavigatedFrom(
            NavigationEventArgs e)
        {
            _gamepadTimer.Stop();
            _verticalInputHeld = false;

            base.OnNavigatedFrom(e);
        }

        private void GamepadTimer_Tick(
            object sender,
            object e)
        {
            var gamepad =
                Gamepad.Gamepads.FirstOrDefault();

            if (gamepad == null)
            {
                _verticalInputHeld = false;
                return;
            }

            var reading =
                gamepad.GetCurrentReading();

            var upPressed =
                (reading.Buttons &
                    GamepadButtons.DPadUp) != 0 ||
                reading.LeftThumbstickY > 0.65;

            var downPressed =
                (reading.Buttons &
                    GamepadButtons.DPadDown) != 0 ||
                reading.LeftThumbstickY < -0.65;

            var verticalPressed =
                upPressed || downPressed;

            if (verticalPressed &&
                !_verticalInputHeld)
            {
                MoveCatalog(
                    upPressed ? -1 : 1);
            }

            _verticalInputHeld =
                verticalPressed;
        }

        private async Task LoadAsync()
        {
            try
            {
                await AppServices.EnsureAddonsLoadedAsync();

                var sources = AppServices.Addons
                    .GetCatalogs()
                    .Where(c => !c.RequiresExtra)
                    .ToList();

                var tasks = sources.Select(async (source, index) =>
                {
                    var catalog = new CatalogRow
                    {
                        Index = index,
                        Title = source.Title
                    };

                    try
                    {
                        var metas =
                            await AppServices.Addons.Client.GetCatalogAsync(
                                source.Addon,
                                source.Catalog.Type,
                                source.Catalog.Id);

                        foreach (var meta in metas.Take(30))
                        {
                            meta.Type =
                                meta.Type ?? source.Catalog.Type;

                            catalog.Items.Add(meta);
                        }

                        if (catalog.Items.Count > 0)
                        {
                            await Dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal,
                                () => AddCatalog(catalog));
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore broken addons.
                    }
                }).ToList();

                await Task.WhenAll(tasks);

                if (_catalogs.Count == 0)
                {
                    ShowMessage(
                        "No catalogs loaded. Check your connection, " +
                        "or log in under Settings to load your addons.");
                }
                else
                {
                    ShowVisibleCatalogs();
                }
            }
            catch (Exception ex)
            {
                ShowMessage(
                    "Couldn't load addons: " + ex.Message);
            }
            finally
            {
                Loading.IsActive = false;
                Loading.Visibility = Visibility.Collapsed;
            }
        }

        private void AddCatalog(
            CatalogRow catalog)
        {
            if (catalog == null ||
                catalog.Items.Count == 0 ||
                _catalogs.Any(c =>
                    c.Index == catalog.Index))
            {
                return;
            }

            var insertIndex =
                _catalogs.Count(c =>
                    c.Index < catalog.Index);

            _catalogs.Insert(
                insertIndex,
                catalog);

            if (_catalogs.Count == 1)
            {
                _topCatalogIndex = 0;
                _activeVisibleRow = 0;
                ShowVisibleCatalogs();
            }
        }

        private void ShowVisibleCatalogs()
        {
            if (_catalogs.Count == 0)
                return;

            ClearAllPosterHighlights();

            if (_topCatalogIndex >= _catalogs.Count)
                _topCatalogIndex = 0;

            var topCatalog =
                _catalogs[_topCatalogIndex];

            TopCatalogTitle.Text =
                topCatalog.Title ?? "Catalog";

            TopCatalogList.ItemsSource =
                topCatalog.Items;

            var bottomIndex =
                _topCatalogIndex + 1;

            if (bottomIndex < _catalogs.Count)
            {
                var bottomCatalog =
                    _catalogs[bottomIndex];

                BottomCatalogTitle.Text =
                    bottomCatalog.Title ?? "Catalog";

                BottomCatalogList.ItemsSource =
                    bottomCatalog.Items;

                BottomCatalogList.Visibility =
                    Visibility.Visible;
            }
            else
            {
                BottomCatalogTitle.Text =
                    "No more catalogs";

                BottomCatalogList.ItemsSource =
                    null;

                BottomCatalogList.Visibility =
                    Visibility.Collapsed;
            }

            CatalogPosition.Text =
                (_topCatalogIndex + 1) +
                " / " +
                _catalogs.Count;

            Dispatcher.RunAsync(
                CoreDispatcherPriority.Low,
                RestoreCurrentSelection);
        }

        private void MoveCatalog(
            int amount)
        {
            if (_catalogs.Count == 0)
                return;

            UpdateFocusedTitleIndex();

            var currentIndex =
                _topCatalogIndex +
                _activeVisibleRow;

            if (currentIndex >= _catalogs.Count)
                currentIndex =
                    _catalogs.Count - 1;

            var targetIndex =
                currentIndex + amount;

            if (targetIndex < 0)
                targetIndex =
                    _catalogs.Count - 1;

            if (targetIndex >= _catalogs.Count)
                targetIndex = 0;

            if (amount > 0)
            {
                if (_activeVisibleRow == 0 &&
                    targetIndex ==
                    _topCatalogIndex + 1 &&
                    targetIndex < _catalogs.Count)
                {
                    _activeVisibleRow = 1;
                }
                else if (targetIndex == 0)
                {
                    _topCatalogIndex = 0;
                    _activeVisibleRow = 0;
                }
                else
                {
                    _topCatalogIndex =
                        targetIndex - 1;

                    _activeVisibleRow = 1;
                }
            }
            else
            {
                if (_activeVisibleRow == 1 &&
                    targetIndex == _topCatalogIndex)
                {
                    _activeVisibleRow = 0;
                }
                else if (targetIndex ==
                         _catalogs.Count - 1)
                {
                    _topCatalogIndex =
                        Math.Max(
                            0,
                            _catalogs.Count - 2);

                    _activeVisibleRow =
                        _catalogs.Count > 1
                            ? 1
                            : 0;
                }
                else
                {
                    _topCatalogIndex =
                        targetIndex;

                    _activeVisibleRow = 0;
                }
            }

            ShowVisibleCatalogs();
        }

        private void RestoreCurrentSelection()
        {
            if (_catalogs.Count == 0)
                return;

            var catalogIndex =
                _topCatalogIndex +
                _activeVisibleRow;

            if (catalogIndex >= _catalogs.Count)
                catalogIndex =
                    _catalogs.Count - 1;

            var catalog =
                _catalogs[catalogIndex];

            var list =
                _activeVisibleRow == 0
                    ? TopCatalogList
                    : BottomCatalogList;

            if (list == null ||
                catalog.Items.Count == 0)
            {
                return;
            }

            var titleIndex =
                GetTitleIndex(catalog);

            titleIndex = Math.Max(
                0,
                Math.Min(
                    titleIndex,
                    catalog.Items.Count - 1));

            list.UpdateLayout();

            var item =
                list.ContainerFromIndex(
                    titleIndex) as Control;

            if (item == null)
            {
                list.ScrollIntoView(
                    catalog.Items[titleIndex]);

                list.UpdateLayout();

                item =
                    list.ContainerFromIndex(
                        titleIndex) as Control;
            }

            item?.Focus(
                FocusState.Keyboard);
        }

        private void CatalogList_ContainerContentChanging(
            ListViewBase sender,
            ContainerContentChangingEventArgs args)
        {
            var item =
                args.ItemContainer as ListViewItem;

            if (item == null)
                return;

            // Keep Up/Down from using automatic XY navigation.
            // The Gamepad polling controls catalog changes.
            item.XYFocusUp = item;
            item.XYFocusDown = item;

            item.GotFocus -= PosterItem_GotFocus;
            item.LostFocus -= PosterItem_LostFocus;

            item.GotFocus += PosterItem_GotFocus;
            item.LostFocus += PosterItem_LostFocus;

            if (args.InRecycleQueue)
                ClearPosterHighlight(item);
        }

        private void PosterItem_GotFocus(
            object sender,
            RoutedEventArgs e)
        {
            var item =
                sender as ListViewItem;

            if (item == null)
                return;

            var list =
                FindParent<ListView>(item);

            if (list == null)
                return;

            var visibleRow =
                list == TopCatalogList
                    ? 0
                    : 1;

            var catalogIndex =
                _topCatalogIndex +
                visibleRow;

            if (catalogIndex >= _catalogs.Count)
                return;

            var catalog =
                _catalogs[catalogIndex];

            var index =
                list.IndexFromContainer(item);

            if (index >= 0)
            {
                _titleIndexes[catalog.Index] =
                    index;
            }

            ClearAllPosterHighlights();
            SetPosterHighlight(item, true);
        }

        private void PosterItem_LostFocus(
            object sender,
            RoutedEventArgs e)
        {
            var item =
                sender as ListViewItem;

            if (item != null)
                ClearPosterHighlight(item);
        }

        private void SetPosterHighlight(
            ListViewItem item,
            bool selected)
        {
            var focusRing =
                FindDescendants<Border>(item)
                    .FirstOrDefault(x =>
                        x.Name == "PosterFocusRing");

            if (focusRing == null)
                return;

            focusRing.BorderBrush =
                selected
                    ? SelectedPosterBrush
                    : TransparentBrush;

            focusRing.BorderThickness =
                selected
                    ? new Thickness(3)
                    : new Thickness(0);
        }

        private void ClearPosterHighlight(
            ListViewItem item)
        {
            SetPosterHighlight(
                item,
                false);
        }

        private void ClearAllPosterHighlights()
        {
            foreach (var item in
                FindDescendants<ListViewItem>(this))
            {
                ClearPosterHighlight(item);
            }
        }

        private void UpdateFocusedTitleIndex()
        {
            var focused =
                FocusManager.GetFocusedElement()
                as DependencyObject;

            var item =
                FindParent<ListViewItem>(focused);

            var list =
                FindParent<ListView>(focused);

            if (item == null ||
                list == null)
            {
                return;
            }

            var visibleRow =
                list == TopCatalogList
                    ? 0
                    : 1;

            var catalogIndex =
                _topCatalogIndex +
                visibleRow;

            if (catalogIndex >= _catalogs.Count)
                return;

            var catalog =
                _catalogs[catalogIndex];

            var index =
                list.IndexFromContainer(item);

            if (index >= 0)
            {
                _titleIndexes[catalog.Index] =
                    index;
            }
        }

        private int GetTitleIndex(
            CatalogRow catalog)
        {
            return _titleIndexes.TryGetValue(
                catalog.Index,
                out var index)
                ? index
                : 0;
        }

        private void BoardPage_KeyDown(
            object sender,
            KeyRoutedEventArgs e)
        {
            if (IsVerticalNavigationKey(e.Key))
                e.Handled = true;
        }

        private void CatalogList_KeyDown(
            object sender,
            KeyRoutedEventArgs e)
        {
            BoardPage_KeyDown(
                sender,
                e);
        }

        private static bool IsVerticalNavigationKey(
            VirtualKey key)
        {
            return key == VirtualKey.Up ||
                   key == VirtualKey.Down ||
                   key == VirtualKey.GamepadDPadUp ||
                   key == VirtualKey.GamepadDPadDown ||
                   key == VirtualKey.GamepadLeftThumbstickUp ||
                   key == VirtualKey.GamepadLeftThumbstickDown;
        }

        private void Poster_ItemClick(
            object sender,
            ItemClickEventArgs e)
        {
            var meta =
                e.ClickedItem as MetaPreview;

            if (meta == null)
                return;

            UpdateFocusedTitleIndex();

            Frame.Navigate(
                typeof(DetailPage),
                new MetaArgs
                {
                    Type = meta.Type,
                    Id = meta.Id
                });
        }

        private void ShowMessage(
            string text)
        {
            Message.Text = text;
            Message.Visibility =
                Visibility.Visible;
        }

        private static T FindParent<T>(
            DependencyObject child)
            where T : DependencyObject
        {
            var parent =
                VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T match)
                    return match;

                parent =
                    VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private static IEnumerable<T>
            FindDescendants<T>(
                DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
                yield break;

            for (var i = 0;
                 i < VisualTreeHelper.GetChildrenCount(root);
                 i++)
            {
                var child =
                    VisualTreeHelper.GetChild(root, i);

                if (child is T match)
                    yield return match;

                foreach (var descendant in
                    FindDescendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}