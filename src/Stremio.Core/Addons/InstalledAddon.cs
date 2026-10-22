using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Stremio.Core.Addons
{
    public class InstalledAddon
    {
        public InstalledAddon(string transportUrl, Manifest manifest)
        {
            TransportUrl = transportUrl;
            Manifest = manifest;
        }

        public string TransportUrl { get; }
        public Manifest Manifest { get; }

        // "https://host/path/manifest.json" -> "https://host/path"
        public string BaseUrl
        {
            get
            {
                var url = TransportUrl.Trim();
                if (url.StartsWith("stremio://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url.Substring("stremio://".Length);
                const string suffix = "/manifest.json";
                if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    url = url.Substring(0, url.Length - suffix.Length);
                return url.TrimEnd('/');
            }
        }

        public bool Supports(string resource, string type, string id)
        {
            foreach (var entry in Manifest.Resources)
            {
                if (entry.Type == JTokenType.String)
                {
                    if ((string)entry == resource)
                        return Matches(Manifest.Types, Manifest.IdPrefixes, type, id);
                }
                else if (entry is JObject obj && (string)obj["name"] == resource)
                {
                    var types = obj["types"]?.ToObject<string[]>() ?? Manifest.Types?.ToArray();
                    var prefixes = obj["idPrefixes"]?.ToObject<string[]>() ?? Manifest.IdPrefixes?.ToArray();
                    return Matches(types, prefixes, type, id);
                }
            }
            return false;
        }

        private static bool Matches(System.Collections.Generic.IEnumerable<string> types,
            System.Collections.Generic.IEnumerable<string> prefixes, string type, string id)
        {
            if (types != null && !types.Contains(type))
                return false;
            if (prefixes == null || id == null)
                return true;
            return prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));
        }
    }
}
