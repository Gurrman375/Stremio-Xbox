using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stremio.Core.Addons;

namespace Stremio.Core.Api
{
    // Client for the Stremio account API (https://api.strem.io/api/<method>).
    public class StremioApi
    {
        public const string DefaultEndpoint = "https://api.strem.io/api/";

        private readonly HttpClient _http;
        private readonly string _endpoint;

        public StremioApi(HttpClient http, string endpoint = DefaultEndpoint)
        {
            _http = http;
            _endpoint = endpoint;
        }

        public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default(CancellationToken))
        {
            var result = await CallAsync("login", new JObject
            {
                ["type"] = "Login",
                ["email"] = email,
                ["password"] = password,
                ["facebook"] = false,
            }, ct).ConfigureAwait(false);
            return result.ToObject<LoginResult>();
        }

        public Task LogoutAsync(string authKey, CancellationToken ct = default(CancellationToken))
        {
            return CallAsync("logout", new JObject { ["type"] = "Logout", ["authKey"] = authKey }, ct);
        }

        public async Task<List<InstalledAddon>> GetAddonCollectionAsync(string authKey, CancellationToken ct = default(CancellationToken))
        {
            var result = await CallAsync("addonCollectionGet", new JObject
            {
                ["type"] = "AddonCollectionGet",
                ["authKey"] = authKey,
                ["update"] = true,
            }, ct).ConfigureAwait(false);

            var addons = new List<InstalledAddon>();
            foreach (var item in (JArray)result["addons"] ?? new JArray())
            {
                var url = (string)item["transportUrl"];
                var manifest = item["manifest"]?.ToObject<Manifest>();
                if (url != null && manifest != null)
                    addons.Add(new InstalledAddon(url, manifest));
            }
            return addons;
        }

        public async Task<List<LibraryItem>> GetLibraryAsync(string authKey, CancellationToken ct = default(CancellationToken))
        {
            var result = await CallAsync("datastoreGet", new JObject
            {
                ["authKey"] = authKey,
                ["collection"] = "libraryItem",
                ["ids"] = new JArray(),
                ["all"] = true,
            }, ct).ConfigureAwait(false);
            return result.ToObject<List<LibraryItem>>() ?? new List<LibraryItem>();
        }

        private async Task<JToken> CallAsync(string method, JObject body, CancellationToken ct)
        {
            var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using (var response = await _http.PostAsync(_endpoint + method, content, ct).ConfigureAwait(false))
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(text);
                var error = json["error"];
                if (error != null && error.Type != JTokenType.Null)
                    throw new StremioApiException((string)error["message"] ?? error.ToString());
                return json["result"];
            }
        }
    }

    public class StremioApiException : Exception
    {
        public StremioApiException(string message) : base(message) { }
    }

    public class LoginResult
    {
        [JsonProperty("authKey")] public string AuthKey { get; set; }
        [JsonProperty("user")] public User User { get; set; }
    }

    public class User
    {
        [JsonProperty("_id")] public string Id { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
    }

    public class LibraryItem
    {
        [JsonProperty("_id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("poster")] public string Poster { get; set; }
        [JsonProperty("removed")] public bool Removed { get; set; }
        [JsonProperty("temp")] public bool Temp { get; set; }
        [JsonProperty("_mtime")] public DateTime? ModifiedTime { get; set; }
        [JsonProperty("state")] public LibraryItemState State { get; set; }
    }

    public class LibraryItemState
    {
        [JsonProperty("timeOffset")] public long TimeOffset { get; set; }
        [JsonProperty("duration")] public long Duration { get; set; }
        [JsonProperty("video_id")] public string VideoId { get; set; }
    }
}
