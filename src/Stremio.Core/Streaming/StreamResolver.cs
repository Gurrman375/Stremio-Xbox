using System;
using System.Collections.Generic;
using Stremio.Core.Addons;

namespace Stremio.Core.Streaming
{
    public class PlayableStream
    {
        public string Url { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public IList<Subtitle> Subtitles { get; set; } = new List<Subtitle>();
    }

    public static class StreamResolver
    {
        // True when the console can play the stream without help from other software.
        public static bool IsDirect(Stream stream)
        {
            return !string.IsNullOrEmpty(stream.Url);
        }

        // True when the stream can be played at all with the given settings.
        public static bool IsPlayable(Stream stream, string streamingServerUrl)
        {
            if (IsDirect(stream))
                return true;
            return !string.IsNullOrEmpty(stream.InfoHash) && !string.IsNullOrEmpty(streamingServerUrl);
        }

        // Returns null when the stream cannot be played (for example a torrent with no streaming server configured,
        // a YouTube id, or an external link).
        public static PlayableStream Resolve(Stream stream, string streamingServerUrl)
        {
            string url = null;
            if (IsDirect(stream))
            {
                url = stream.Url;
            }
            else if (!string.IsNullOrEmpty(stream.InfoHash) && !string.IsNullOrEmpty(streamingServerUrl))
            {
                // The Stremio streaming server exposes torrents as {server}/{infoHash}/{fileIdx}.
                var fileIdx = stream.FileIdx.HasValue ? stream.FileIdx.Value.ToString() : "-1";
                url = streamingServerUrl.TrimEnd('/') + "/" + stream.InfoHash.ToLowerInvariant() + "/" + fileIdx;
            }

            if (url == null)
                return null;

            return new PlayableStream
            {
                Url = url,
                Headers = stream.BehaviorHints?.ProxyHeaders?.Request,
                Subtitles = (IList<Subtitle>)stream.Subtitles ?? new List<Subtitle>(),
            };
        }
    }
}
