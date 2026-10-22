using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stremio.Core.Addons;
using Stremio.Core.Api;
using Stremio.Core.Settings;
using Stremio.Core.Streaming;
using Xunit;

namespace Stremio.Core.Tests
{
    public class CoreTests
    {
        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        }

        private static InstalledAddon Addon(string fixture, string url)
        {
            return new InstalledAddon(url, JsonConvert.DeserializeObject<Manifest>(Fixture(fixture)));
        }

        [Fact]
        public void BaseUrlStripsManifestAndStremioScheme()
        {
            var addon = new InstalledAddon("stremio://torrentio.strem.fun/sort=qualitysize/manifest.json", null);
            Assert.Equal("https://torrentio.strem.fun/sort=qualitysize", addon.BaseUrl);
        }

        [Fact]
        public void BuildUrlEncodesIdAndExtra()
        {
            var addon = new InstalledAddon("https://v3-cinemeta.strem.io/manifest.json", null);
            var url = AddonClient.BuildUrl(addon, "catalog", "movie", "top",
                new Dictionary<string, string> { ["search"] = "the matrix" });
            Assert.Equal("https://v3-cinemeta.strem.io/catalog/movie/top/search=the%20matrix.json", url);

            Assert.Equal("https://v3-cinemeta.strem.io/stream/series/tt0944947%3A1%3A2.json",
                AddonClient.BuildUrl(addon, "stream", "series", "tt0944947:1:2", null));
        }

        [Fact]
        public void SupportsHandlesStringAndObjectResources()
        {
            var cinemeta = Addon("cinemeta-manifest.json", "https://v3-cinemeta.strem.io/manifest.json");
            Assert.True(cinemeta.Supports("meta", "movie", "tt0133093"));
            Assert.False(cinemeta.Supports("meta", "movie", "kitsu:1"));
            Assert.False(cinemeta.Supports("stream", "movie", "tt0133093"));

            var torrentio = Addon("torrentio-manifest.json", "https://torrentio.strem.fun/manifest.json");
            Assert.True(torrentio.Supports("stream", "series", "tt0944947:1:2"));
            Assert.True(torrentio.Supports("stream", "anime", "kitsu:1"));
            Assert.False(torrentio.Supports("stream", "other", "tt1"));
            Assert.False(torrentio.Supports("meta", "movie", "tt1"));
        }

        [Fact]
        public void CatalogSourceFlags()
        {
            var cinemeta = Addon("cinemeta-manifest.json", "https://v3-cinemeta.strem.io/manifest.json");
            var sources = cinemeta.Manifest.Catalogs.Select(c => new CatalogSource { Addon = cinemeta, Catalog = c }).ToList();
            Assert.False(sources[0].RequiresExtra);
            Assert.True(sources[0].SupportsSearch);
            Assert.True(sources[1].RequiresExtra);
            Assert.Equal("Popular - Movies", sources[0].Title);
        }

        [Fact]
        public void ResolverHandlesDirectTorrentAndUnplayable()
        {
            var streams = JsonConvert.DeserializeObject<StreamsBody>(Fixture("streams.json")).Streams;

            var torrent = StreamResolver.Resolve(streams[0], "http://192.168.1.20:11470/");
            Assert.Equal("http://192.168.1.20:11470/abcdef0123456789abcdef0123456789abcdef01/2", torrent.Url);
            Assert.Null(StreamResolver.Resolve(streams[0], null));
            Assert.False(StreamResolver.IsPlayable(streams[0], null));

            var direct = StreamResolver.Resolve(streams[1], null);
            Assert.Equal("https://example.com/resolve/movie.mkv", direct.Url);
            Assert.Equal("Stremio", direct.Headers["User-Agent"]);

            Assert.Null(StreamResolver.Resolve(streams[2], "http://server:11470"));
        }

        [Fact]
        public async Task ManagerFansOutStreamsToSupportingAddons()
        {
            var handler = new FakeHandler(new Dictionary<string, string>
            {
                ["https://v3-cinemeta.strem.io/manifest.json"] = Fixture("cinemeta-manifest.json"),
                ["https://opensubtitles-v3.strem.io/manifest.json"] = "not json",
                ["https://torrentio.strem.fun/manifest.json"] = Fixture("torrentio-manifest.json"),
                ["https://torrentio.strem.fun/stream/movie/tt0133093.json"] = Fixture("streams.json"),
            });
            var http = new HttpClient(handler);
            var settings = new AppSettings(new MemoryStore())
            {
                ExtraAddonUrls = new List<string> { "https://torrentio.strem.fun/manifest.json" },
            };
            var manager = new AddonManager(http, settings);

            await manager.LoadAsync();
            Assert.Equal(new[] { "Cinemeta", "Torrentio" }, manager.Addons.Select(a => a.Manifest.Name));

            var streams = await manager.GetStreamsAsync("movie", "tt0133093");
            Assert.Equal(3, streams.Count);
            Assert.All(streams, s => Assert.Equal("Torrentio", s.AddonName));
            Assert.DoesNotContain(handler.Requested, u => u.StartsWith("https://v3-cinemeta.strem.io/stream"));
        }

        [Fact]
        public async Task ApiErrorBecomesException()
        {
            var handler = new FakeHandler(new Dictionary<string, string>
            {
                ["https://api.strem.io/api/login"] = "{\"error\":{\"message\":\"Wrong passphrase\",\"code\":3}}",
            });
            var api = new StremioApi(new HttpClient(handler));
            var ex = await Assert.ThrowsAsync<StremioApiException>(() => api.LoginAsync("a@b.c", "x"));
            Assert.Equal("Wrong passphrase", ex.Message);
        }

        [Fact]
        public async Task ApiParsesAddonCollection()
        {
            var collection = "{\"result\":{\"addons\":[{\"transportUrl\":\"https://v3-cinemeta.strem.io/manifest.json\",\"manifest\":"
                + Fixture("cinemeta-manifest.json") + "}]}}";
            var api = new StremioApi(new HttpClient(new FakeHandler(new Dictionary<string, string>
            {
                ["https://api.strem.io/api/addonCollectionGet"] = collection,
            })));
            var addons = await api.GetAddonCollectionAsync("key");
            Assert.Single(addons);
            Assert.Equal("Cinemeta", addons[0].Manifest.Name);
        }

        private class StreamsBody
        {
            [JsonProperty("streams")] public List<Stremio.Core.Addons.Stream> Streams { get; set; }
        }

        private class MemoryStore : ISettingsStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
            public void Set(string key, string value) => _values[key] = value;
        }

        private class FakeHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, string> _responses;
            public readonly List<string> Requested = new List<string>();

            public FakeHandler(Dictionary<string, string> responses)
            {
                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var url = request.RequestUri.AbsoluteUri;
                Requested.Add(url);
                var response = _responses.TryGetValue(url, out var body)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
                return Task.FromResult(response);
            }
        }
    }
}
