using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class ArtworkService : IDisposable
{
    private static readonly string[] DefaultArtworkSources = ["musicbrainz", "itunes", "lastfm"];

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ArtworkService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowRPC/0.1 (+https://github.com/ios7jbpro/WindowRPC)");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<string?> ResolveArtworkUrlAsync(OverrideEntry entry, MediaSnapshot media, CancellationToken cancellationToken = default)
    {
        if (!media.IsActive || string.IsNullOrWhiteSpace(media.Album) || string.IsNullOrWhiteSpace(media.Artist))
        {
            return null;
        }

        var sources = entry.ArtworkSources is { Length: > 0 }
            ? entry.ArtworkSources
            : !string.IsNullOrWhiteSpace(entry.Artwork)
                ? DefaultArtworkSources
                : Array.Empty<string>();

        if (sources.Length == 0)
        {
            return null;
        }

        var cacheKey = $"{media.Artist}::{media.Album}::{string.Join('|', sources)}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Url;
        }

        string? resolved = null;
        foreach (var source in sources)
        {
            resolved = await ResolveFromSourceAsync(source, entry, media, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                break;
            }
        }

        _cache[cacheKey] = new CacheEntry(resolved, DateTimeOffset.UtcNow.AddMinutes(30));
        return resolved;
    }

    private async Task<string?> ResolveFromSourceAsync(
        string source,
        OverrideEntry entry,
        MediaSnapshot media,
        CancellationToken cancellationToken)
    {
        return source.Trim().ToLowerInvariant() switch
        {
            "musicbrainz" => await ResolveFromMusicBrainzAsync(media, cancellationToken).ConfigureAwait(false),
            "itunes" => await ResolveFromItunesAsync(media, cancellationToken).ConfigureAwait(false),
            "lastfm" => await ResolveFromLastFmAsync(entry.Artwork, media, cancellationToken).ConfigureAwait(false),
            _ => null
        };
    }

    private async Task<string?> ResolveFromMusicBrainzAsync(MediaSnapshot media, CancellationToken cancellationToken)
    {
        var query = $"artist:\"{media.Artist}\" AND release:\"{media.Album}\"";
        var url = $"https://musicbrainz.org/ws/2/release/?query={Uri.EscapeDataString(query)}&fmt=json&limit=3";
        var search = await _httpClient.GetFromJsonAsync<MusicBrainzReleaseSearchResponse>(url, cancellationToken).ConfigureAwait(false);
        if (search?.Releases is null || search.Releases.Length == 0)
        {
            return null;
        }

        foreach (var release in search.Releases)
        {
            if (string.IsNullOrWhiteSpace(release.Id))
            {
                continue;
            }

            var artUrl = $"https://coverartarchive.org/release/{release.Id}/front-500";
            if (await UrlExistsAsync(artUrl, cancellationToken).ConfigureAwait(false))
            {
                return artUrl;
            }
        }

        return null;
    }

    private async Task<string?> ResolveFromItunesAsync(MediaSnapshot media, CancellationToken cancellationToken)
    {
        var term = $"{media.Artist} {media.Album}";
        var url =
            $"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&media=music&entity=album&limit=5";

        var response = await _httpClient.GetFromJsonAsync<ItunesSearchResponse>(url, cancellationToken).ConfigureAwait(false);
        if (response?.Results is null)
        {
            return null;
        }

        var match = response.Results.FirstOrDefault(result =>
            string.Equals(result.CollectionName, media.Album, StringComparison.OrdinalIgnoreCase)
            && string.Equals(result.ArtistName, media.Artist, StringComparison.OrdinalIgnoreCase));

        match ??= response.Results.FirstOrDefault(result =>
            result.CollectionName.Contains(media.Album, StringComparison.OrdinalIgnoreCase)
            && result.ArtistName.Contains(media.Artist, StringComparison.OrdinalIgnoreCase));

        if (match?.ArtworkUrl100 is null)
        {
            return null;
        }

        return match.ArtworkUrl100.Replace("100x100bb", "512x512bb", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> ResolveFromLastFmAsync(
        string? apiKey,
        MediaSnapshot media,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var url =
            $"https://ws.audioscrobbler.com/2.0/?method=album.getinfo&api_key={Uri.EscapeDataString(apiKey)}&artist={Uri.EscapeDataString(media.Artist)}&album={Uri.EscapeDataString(media.Album)}&format=json";

        var response = await _httpClient.GetFromJsonAsync<LastFmAlbumResponse>(url, cancellationToken).ConfigureAwait(false);
        if (response?.Album?.Image is null)
        {
            return null;
        }

        return response.Album.Image
            .Select(image => image.Url)
            .LastOrDefault(urlValue => !string.IsNullOrWhiteSpace(urlValue));
    }

    private async Task<bool> UrlExistsAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.Redirect)
            {
                return true;
            }
        }
        catch
        {
            // Fall through to GET.
        }

        try
        {
            using var fallback = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return fallback.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed record CacheEntry(string? Url, DateTimeOffset ExpiresAt);

    private sealed class MusicBrainzReleaseSearchResponse
    {
        [JsonPropertyName("releases")]
        public MusicBrainzRelease[]? Releases { get; init; }
    }

    private sealed class MusicBrainzRelease
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }

    private sealed class ItunesSearchResponse
    {
        [JsonPropertyName("results")]
        public ItunesAlbumResult[]? Results { get; init; }
    }

    private sealed class ItunesAlbumResult
    {
        [JsonPropertyName("artistName")]
        public string ArtistName { get; init; } = string.Empty;

        [JsonPropertyName("collectionName")]
        public string CollectionName { get; init; } = string.Empty;

        [JsonPropertyName("artworkUrl100")]
        public string? ArtworkUrl100 { get; init; }
    }

    private sealed class LastFmAlbumResponse
    {
        [JsonPropertyName("album")]
        public LastFmAlbum? Album { get; init; }
    }

    private sealed class LastFmAlbum
    {
        [JsonPropertyName("image")]
        public LastFmImage[]? Image { get; init; }
    }

    private sealed class LastFmImage
    {
        [JsonPropertyName("#text")]
        public string? Url { get; init; }
    }
}
