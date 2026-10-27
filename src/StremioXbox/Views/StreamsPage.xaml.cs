using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stremio.Core.Addons;
using Stremio.Core.Streaming;
using StremioXbox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;

namespace StremioXbox.Views
{
    public sealed partial class StreamsPage : Page
    {
        private StreamsArgs _args;
        private Task<List<Subtitle>> _subtitles;

        private readonly List<Stream> _allStreams = new List<Stream>();

        private readonly ObservableCollection<Stream> _visibleStreams = new ObservableCollection<Stream>();

        private readonly ObservableCollection<string> _sources = new ObservableCollection<string>();

        private CancellationTokenSource _loadCts;

        public StreamsPage()
        {
            InitializeComponent();

            StreamList.ItemsSource = _visibleStreams;
            SourceFilter.ItemsSource = _sources;

            _sources.Add("All");
            SourceFilter.SelectedIndex = 0;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _args = (StreamsArgs)e.Parameter;
            Header.Text = _args.Title ?? "";

            SetImage(Backdrop, _args.Background);

            List<Stream> streams = new List<Stream>();
            try
            {
                await AppServices.EnsureAddonsLoadedAsync();

                _subtitles = AppServices.Addons.GetSubtitlesAsync(
                    _args.Type,
                    _args.VideoId);

                _allStreams.Clear();
                _visibleStreams.Clear();
                _sources.Clear();
                _sources.Add("All");

                _loadCts?.Cancel();
                _loadCts = new CancellationTokenSource();

                var progress = new Progress<List<Stream>>(batch =>
                {
                    foreach (var stream in batch)
                    {
                        _allStreams.Add(stream);

                        var source = string.IsNullOrWhiteSpace(stream.AddonName)
                            ? "Unknown"
                            : stream.AddonName;

                        if (!_sources.Contains(source))
                            _sources.Add(source);
                    }

                    ApplyStreamFilter();
                    UpdateStreamStatus();
                });

                streams = await AppServices.Addons.GetStreamsIncrementalAsync(
                    _args.Type,
                    _args.VideoId,
                    progress,
                    _loadCts.Token);
            }
            catch (Exception ex)
            {
                Status.Text = "Couldn't load streams: " + ex.Message;
            }
            Loading.IsActive = false;
            Loading.Visibility = Visibility.Collapsed;

            ApplyStreamFilter();
            UpdateStreamStatus();

            if (_visibleStreams.Count > 0)
            {
                StreamList.UpdateLayout();

                (StreamList.ContainerFromIndex(0) as Control)?
                    .Focus(FocusState.Programmatic);
            }
        }

        private async void StreamList_ItemClick(
    object sender,
    ItemClickEventArgs e)
        {
            var stream = (Stream)e.ClickedItem;

            var resolved = StreamResolver.Resolve(stream, AppServices.Settings.StreamingServerUrl);

            if (resolved == null)
            {
                if (Uri.TryCreate(
                    stream.ExternalUrl,
                    UriKind.Absolute,
                    out var externalUri))
                {
                    await Launcher.LaunchUriAsync(externalUri);
                }

                return;
            }


            var subtitles = new List<Subtitle>(resolved.Subtitles);
            try
            {
                if (_subtitles != null)
                    subtitles.AddRange(await _subtitles);
            }
            catch (Exception)
            {
                // Play without addon subtitles.
            }

            var text = ((stream.Name ?? "") + " " + (stream.Title ?? "") + " " + (stream.Description ?? "")).ToUpperInvariant();
            ShellPage.ShowPlayer(new PlayerArgs
            {
                Url = resolved.Url,
                Headers = resolved.Headers,
                Title = _args.Title,
                Subtitles = subtitles,
                LooksHdr = text.Contains("HDR") || text.Contains("DOLBY VISION") || text.Contains(" DV"),
            });
        }
        private static int GetQualityRank(Stream stream)
        {
            var text = string.Join(" ",
                stream.Name ?? "",
                stream.Title ?? "",
                stream.Description ?? "",
                stream.BehaviorHints?.Filename ?? "")
                .ToUpperInvariant();

            if (text.Contains("2160") ||
                text.Contains("4K") ||
                text.Contains("UHD"))
                return 4;

            if (text.Contains("1080"))
                return 3;

            if (text.Contains("720"))
                return 2;

            if (text.Contains("480"))
                return 1;

            return 0;
        }

        private void SourceFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
                {
                    ApplyStreamFilter();
                }

        private void ApplyStreamFilter()
        {
            var selectedSource = SourceFilter.SelectedItem as string;

            var streams = _allStreams.AsEnumerable();

            if (!string.IsNullOrEmpty(selectedSource) &&
                selectedSource != "All")
            {
                streams = streams.Where(s =>
                    string.Equals(
                        s.AddonName,
                        selectedSource,
                        StringComparison.OrdinalIgnoreCase));
            }

            var sorted = streams
                .OrderByDescending(s =>
                    StreamResolver.IsPlayable(
                        s,
                        AppServices.Settings.StreamingServerUrl))
                .ThenBy(s => s.AddonName ?? "Unknown",
                    StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(GetQualityRank)
                .ThenBy(s => s.Label ?? "",
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            _visibleStreams.Clear();

            foreach (var stream in sorted)
                _visibleStreams.Add(stream);
        }

        private void UpdateStreamStatus()
        {
            var server = AppServices.Settings.StreamingServerUrl;

            var playable = _allStreams.Count(s =>
                StreamResolver.IsPlayable(s, server));

            var informational = _allStreams.Count - playable;

            if (_allStreams.Count == 0)
            {
                Status.Text = "No streams found.";
                return;
            }

            Status.Text =
                playable + " playable stream(s)";

            if (informational > 0)
            {
                Status.Text +=
                    " • " + informational +
                    " addon information/link item(s)";
            }
        }
        private static void SetImage(Image image, string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                image.Source = new BitmapImage(uri);
        }
    }
}
