using System.Net.Http;
using System.Text.Json;
using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using Jellyfin.Plugin.CensoredLyrics.Models;
using MediaBrowser.Model.Lyrics;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Finds the original LRCLIB lyrics for a track. The text is not censored.
/// </summary>
public sealed class LyricLookup
{
    private readonly LrcLibClient _client;
    private readonly ILogger<LyricLookup> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LyricLookup"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public LyricLookup(IHttpClientFactory httpClientFactory, ILogger<LyricLookup> logger)
    {
        _client = new LrcLibClient(httpClientFactory);
        _logger = logger;
    }

    /// <summary>
    /// Searches LRCLIB. <see cref="LookupResult.Retrieved"/> is false when the request failed and should be retried.
    /// </summary>
    /// <param name="configuration">Plugin configuration.</param>
    /// <param name="title">Track title.</param>
    /// <param name="artist">Artist name.</param>
    /// <param name="album">Album name.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The best lyrics, or an empty result when nothing usable was found.</returns>
    public async Task<LookupResult> FindAsync(
        PluginConfiguration configuration,
        string? title,
        string? artist,
        string? album,
        int? durationSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return LookupResult.Empty;
        }

        try
        {
            var records = await SearchRecordsAsync(configuration, title, artist, album, durationSeconds, cancellationToken)
                .ConfigureAwait(false);
            var best = records.FirstOrDefault(HasLyrics);
            if (best is null)
            {
                return LookupResult.Empty;
            }

            return new LookupResult(true, best.SyncedLyrics, best.PlainLyrics);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LRCLIB timed out while searching for {Artist} - {Song}", artist, title);
            return LookupResult.Failed;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "LRCLIB search failed for {Artist} - {Song}", artist, title);
            return LookupResult.Failed;
        }
        catch (Exception exception) when (exception is JsonException or UriFormatException)
        {
            _logger.LogWarning(exception, "LRCLIB returned an unreadable response for {Artist} - {Song}", artist, title);
            return LookupResult.Failed;
        }
    }

    /// <summary>
    /// Returns ranked LRCLIB records for the manual lyric search.
    /// </summary>
    /// <param name="request">Jellyfin search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Usable records, best match first. Empty when the request fails.</returns>
    public async Task<IReadOnlyList<LrcLibRecord>> SearchAsync(LyricSearchRequest request, CancellationToken cancellationToken)
    {
        var configuration = CensoredLyricsPlugin.Instance?.Configuration ?? new PluginConfiguration();
        var artist = FirstName(request.ArtistNames) ?? FirstName(request.AlbumArtistsNames);
        var result = await FindRecordsAsync(configuration, request.SongName, artist, request.AlbumName, ToDurationSeconds(request.Duration), cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private async Task<IReadOnlyList<LrcLibRecord>> SearchRecordsAsync(
        PluginConfiguration configuration,
        string title,
        string? artist,
        string? album,
        int? durationSeconds,
        CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl(configuration);
        if (!string.IsNullOrWhiteSpace(artist))
        {
            var exact = await _client.GetExactAsync(baseUrl, title, artist, album, durationSeconds, cancellationToken)
                .ConfigureAwait(false);
            if (exact is not null && HasLyrics(exact))
            {
                return [exact];
            }
        }

        var matches = await _client.SearchAsync(baseUrl, title, artist, album, cancellationToken).ConfigureAwait(false);
        return matches
            .Where(HasLyrics)
            .Select(record => (Record: record, Score: Score(record, title, artist, album, durationSeconds)))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => LyricText.IsUsable(item.Record.SyncedLyrics))
            .Take(5)
            .Select(item => item.Record)
            .ToList();
    }

    private async Task<IReadOnlyList<LrcLibRecord>> FindRecordsAsync(
        PluginConfiguration configuration,
        string? title,
        string? artist,
        string? album,
        int? durationSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        try
        {
            return await SearchRecordsAsync(configuration, title, artist, album, durationSeconds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LRCLIB timed out while searching for {Artist} - {Song}", artist, title);
            return [];
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "LRCLIB search failed for {Artist} - {Song}", artist, title);
            return [];
        }
        catch (Exception exception) when (exception is JsonException or UriFormatException)
        {
            _logger.LogWarning(exception, "LRCLIB returned an unreadable response for {Artist} - {Song}", artist, title);
            return [];
        }
    }

    internal static string ResolveBaseUrl(PluginConfiguration configuration)
    {
        var candidate = configuration.BaseUrl?.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return candidate!.TrimEnd('/');
        }

        return PluginConfiguration.DefaultBaseUrl;
    }

    internal static string? FirstName(IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }

        return null;
    }

    internal static int? ToDurationSeconds(long? ticks)
    {
        if (ticks is null or <= 0)
        {
            return null;
        }

        var seconds = (int)Math.Round(TimeSpan.FromTicks(ticks.Value).TotalSeconds, MidpointRounding.AwayFromZero);
        return seconds < 1 ? null : Math.Min(seconds, 3600);
    }

    private static bool HasLyrics(LrcLibRecord record)
    {
        return !record.Instrumental
            && (LyricText.IsUsable(record.SyncedLyrics) || LyricText.IsUsable(record.PlainLyrics));
    }

    private static int Score(LrcLibRecord record, string title, string? artist, string? album, int? durationSeconds)
    {
        var score = 0;
        if (SameText(record.TrackName, title))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(artist) && SameText(record.ArtistName, artist))
        {
            score += 4;
        }

        if (SameText(record.AlbumName, album))
        {
            score += 2;
        }

        if (durationSeconds is not null
            && record.Duration is not null
            && Math.Abs(record.Duration.Value - durationSeconds.Value) <= 2)
        {
            score += 4;
        }

        if (LyricText.IsUsable(record.SyncedLyrics))
        {
            score += 1;
        }

        return score;
    }

    private static bool SameText(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Original lyrics returned by LRCLIB.
/// </summary>
/// <param name="Retrieved">False when the request failed and the song should be tried again later.</param>
/// <param name="Synced">Synced LRC, when LRCLIB had it.</param>
/// <param name="Plain">Plain lyrics, when LRCLIB had them.</param>
public readonly record struct LookupResult(bool Retrieved, string? Synced, string? Plain)
{
    /// <summary>
    /// Gets a result for a song LRCLIB does not have.
    /// </summary>
    public static LookupResult Empty { get; } = new(true, null, null);

    /// <summary>
    /// Gets a result for a failed request.
    /// </summary>
    public static LookupResult Failed { get; } = new(false, null, null);
}
