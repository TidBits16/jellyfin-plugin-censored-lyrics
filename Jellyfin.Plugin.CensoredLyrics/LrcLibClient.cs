using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.CensoredLyrics.Models;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Reads timed and plain lyrics from an LRCLIB-compatible API.
/// </summary>
public sealed class LrcLibClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static long _nextRequestAt;

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LrcLibClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public LrcLibClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Looks up one track by title, artist, album, and duration.
    /// </summary>
    /// <param name="baseUrl">API origin, for example https://lrclib.net.</param>
    /// <param name="trackName">Track title.</param>
    /// <param name="artistName">Artist name.</param>
    /// <param name="albumName">Album name.</param>
    /// <param name="durationSeconds">Duration in seconds, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching record, or null when LRCLIB has no match.</returns>
    public Task<LrcLibRecord?> GetExactAsync(
        string baseUrl,
        string trackName,
        string artistName,
        string? albumName,
        int? durationSeconds,
        CancellationToken cancellationToken)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("track_name", trackName),
            new("artist_name", artistName)
        };

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            query.Add(new KeyValuePair<string, string>("album_name", albumName));
        }

        if (durationSeconds is >= 1 and <= 3600)
        {
            query.Add(new KeyValuePair<string, string>("duration", durationSeconds.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return GetAsync<LrcLibRecord>(baseUrl, "/api/get", query, cancellationToken);
    }

    /// <summary>
    /// Searches LRCLIB when an exact signature match is not available.
    /// </summary>
    /// <param name="baseUrl">API origin.</param>
    /// <param name="trackName">Track title.</param>
    /// <param name="artistName">Artist name, or null to search by title only.</param>
    /// <param name="albumName">Album name, or null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching records. Empty when nothing matches.</returns>
    public async Task<IReadOnlyList<LrcLibRecord>> SearchAsync(
        string baseUrl,
        string trackName,
        string? artistName,
        string? albumName,
        CancellationToken cancellationToken)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("track_name", trackName)
        };

        if (!string.IsNullOrWhiteSpace(artistName))
        {
            query.Add(new KeyValuePair<string, string>("artist_name", artistName));
        }

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            query.Add(new KeyValuePair<string, string>("album_name", albumName));
        }

        var results = await GetAsync<List<LrcLibRecord>>(baseUrl, "/api/search", query, cancellationToken)
            .ConfigureAwait(false);
        return results ?? [];
    }

    /// <summary>
    /// Loads one record by its LRCLIB id.
    /// </summary>
    /// <param name="baseUrl">API origin.</param>
    /// <param name="id">LRCLIB record id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The record, or null if it no longer exists.</returns>
    public Task<LrcLibRecord?> GetByIdAsync(string baseUrl, long id, CancellationToken cancellationToken)
    {
        return GetAsync<LrcLibRecord>(baseUrl, $"/api/get/{id.ToString(CultureInfo.InvariantCulture)}", [], cancellationToken);
    }

    private async Task<T?> GetAsync<T>(
        string baseUrl,
        string path,
        IReadOnlyList<KeyValuePair<string, string>> query,
        CancellationToken cancellationToken)
    {
        var builder = new UriBuilder(baseUrl.TrimEnd('/'))
        {
            Path = path
        };

        if (query.Count > 0)
        {
            builder.Query = string.Join(
                "&",
                query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await WaitTurnAsync(cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
            using var response = await _httpClientFactory.CreateClient(CensoredLyricsPlugin.HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (attempt == 0
                && response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        return default;
    }

    private static async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = Environment.TickCount64;
            var delay = _nextRequestAt - now;
            if (delay > 0)
            {
                await Task.Delay((int)delay, cancellationToken).ConfigureAwait(false);
            }

            _nextRequestAt = Environment.TickCount64 + 250;
        }
        finally
        {
            Gate.Release();
        }
    }
}
