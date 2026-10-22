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

namespace StremioXbox.Views
{
    public sealed partial class StreamsPage : Page
    {
        private StreamsArgs _args;
        private Task<List<Subtitle>> _subtitles;

        public StreamsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _args = (StreamsArgs)e.Parameter;
            Header.Text = _args.Title ?? "";

            List<Stream> streams;
            try
            {
                await AppServices.EnsureAddonsLoadedAsync();
                _subtitles = AppServices.Addons.GetSubtitlesAsync(_args.Type, _args.VideoId);
                streams = await AppServices.Addons.GetStreamsAsync(_args.Type, _args.VideoId);
            }
            catch (Exception ex)
            {
                streams = new List<Stream>();
                Status.Text = "Couldn't load streams: " + ex.Message;
            }

            Loading.IsActive = false;
            Loading.Visibility = Visibility.Collapsed;

            var server = AppServices.Settings.StreamingServerUrl;
            var playable = streams.Where(s => StreamResolver.IsPlayable(s, server)).ToList();
            var hidden = streams.Count - playable.Count;

            if (streams.Count == 0 && string.IsNullOrEmpty(Status.Text))
                Status.Text = "No streams found. Stream addons (for example Torrentio with a debrid service) come from your Stremio account: log in under Settings.";
            else if (hidden > 0)
                Status.Text = hidden + " torrent stream(s) hidden: set a streaming server under Settings to play them.";
            else
                Status.Text = playable.Count + " stream(s)";

            StreamList.ItemsSource = playable;
            if (playable.Count > 0)
            {
                StreamList.UpdateLayout();
                (StreamList.ContainerFromIndex(0) as Control)?.Focus(FocusState.Programmatic);
            }
        }

        private async void StreamList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var stream = (Stream)e.ClickedItem;
            var resolved = StreamResolver.Resolve(stream, AppServices.Settings.StreamingServerUrl);
            if (resolved == null)
                return;

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
    }
}
