using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using Jellyfin.Plugin.CensoredLyrics.Models;
using MediaBrowser.Controller.Lyrics;
using MediaBrowser.Model.Lyrics;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Downloads timed lyrics from LRCLIB and censors profanity before Jellyfin stores them.
/// </summary>
public sealed class CensoredLyricProvider : ILyricProvider
{
    private const string SyncedSuffix = "synced";
    private const string PlainSuffix = "plain";
    private static readonly Regex LyricTags = new(@"\[[^\]]*\]|<[^>]*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly LrcLibClient _client;
    private readonly ILogger<CensoredLyricProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CensoredLyricProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory used to call LRCLIB.</param>
    /// <param name="logger">Logger.</param>
    public CensoredLyricProvider(IHttpClientFactory httpClientFactory, ILogger<CensoredLyricProvider> logger)
    {
        _client = new LrcLibClient(httpClientFactory);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => CensoredLyricsPlugin.Instance?.Name ?? "Censored Lyrics";

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteLyricInfo>> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SongName))
        {
            return [];
        }

        var configuration = CurrentConfiguration();
        var baseUrl = ResolveBaseUrl(configuration);
        var artist = FirstName(request.ArtistNames) ?? FirstName(request.AlbumArtistsNames);
        var durationSeconds = ToDurationSeconds(request.Duration);

        try
        {
            if (!string.IsNullOrWhiteSpace(artist))
            {
                var exact = await _client.GetExactAsync(
                    baseUrl,
                    request.SongName,
                    artist,
                    request.AlbumName,
                    durationSeconds,
                    cancellationToken).ConfigureAwait(false);

                if (exact is not null && HasLyrics(exact))
                {
                    return ToRemoteLyrics(exact, configuration);
                }
            }

            var matches = await _client.SearchAsync(
                baseUrl,
                request.SongName,
                artist,
                request.AlbumName,
                cancellationToken).ConfigureAwait(false);

            return matches
                .Where(HasLyrics)
                .Select(record => (Record: record, Score: Score(record, request, artist, durationSeconds)))
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => !string.IsNullOrWhiteSpace(item.Record.SyncedLyrics))
                .Take(5)
                .SelectMany(item => ToRemoteLyrics(item.Record, configuration))
                .ToList();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LRCLIB timed out while searching for {Artist} - {Song}", artist, request.SongName);
            return [];
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "LRCLIB search failed for {Artist} - {Song}", artist, request.SongName);
            return [];
        }
        catch (Exception exception) when (exception is JsonException or UriFormatException)
        {
            _logger.LogWarning(exception, "LRCLIB returned an unreadable response for {Artist} - {Song}", artist, request.SongName);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<LyricResponse?> GetLyricsAsync(string id, CancellationToken cancellationToken)
    {
        if (!TryParseId(id, out var recordId, out var synced))
        {
            return null;
        }

        var configuration = CurrentConfiguration();
        var baseUrl = ResolveBaseUrl(configuration);
        try
        {
            var record = await _client.GetByIdAsync(baseUrl, recordId, cancellationToken)
                .ConfigureAwait(false);
            if (record is null)
            {
                return null;
            }

            var lyrics = synced ? record.SyncedLyrics : record.PlainLyrics;
            if (!IsUsableLyric(lyrics))
            {
                synced = !synced;
                lyrics = synced ? record.SyncedLyrics : record.PlainLyrics;
            }

            if (!IsUsableLyric(lyrics))
            {
                return null;
            }

            return CreateResponse(lyrics, synced, configuration);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LRCLIB timed out while loading lyrics {Id}", id);
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "LRCLIB download failed for lyrics {Id}", id);
            return null;
        }
        catch (Exception exception) when (exception is JsonException or UriFormatException)
        {
            _logger.LogWarning(exception, "LRCLIB returned an unreadable response for lyrics {Id}", id);
            return null;
        }
    }

    private static string ResolveBaseUrl(PluginConfiguration configuration)
    {
        var candidate = configuration.BaseUrl?.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return candidate!.TrimEnd('/');
        }

        return PluginConfiguration.DefaultBaseUrl;
    }

    private static PluginConfiguration CurrentConfiguration()
    {
        return CensoredLyricsPlugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    private static IEnumerable<RemoteLyricInfo> ToRemoteLyrics(LrcLibRecord record, PluginConfiguration configuration)
    {
        var synced = CreateInfo(record, record.SyncedLyrics, true, configuration);
        var plain = CreateInfo(record, record.PlainLyrics, false, configuration);
        var first = configuration.PreferSyncedLyrics ? synced : plain;
        var second = configuration.PreferSyncedLyrics ? plain : synced;

        if (first is not null)
        {
            yield return first;
        }

        if (second is not null)
        {
            yield return second;
        }
    }

    private static RemoteLyricInfo? CreateInfo(
        LrcLibRecord record,
        string? lyrics,
        bool synced,
        PluginConfiguration configuration)
    {
        if (!IsUsableLyric(lyrics))
        {
            return null;
        }

        return new RemoteLyricInfo
        {
            Id = record.Id.ToString(CultureInfo.InvariantCulture) + "_" + (synced ? SyncedSuffix : PlainSuffix),
            ProviderName = CensoredLyricsPlugin.Instance?.Name ?? "Censored Lyrics",
            Metadata = new LyricMetadata
            {
                Album = record.AlbumName,
                Artist = record.ArtistName,
                Title = record.TrackName,
                Length = TimeSpan.FromSeconds(record.Duration ?? 0).Ticks,
                IsSynced = synced
            },
            Lyrics = CreateResponse(lyrics, synced, configuration)
        };
    }

    private static LyricResponse CreateResponse(string lyrics, bool synced, PluginConfiguration configuration)
    {
        var text = configuration.EnableCensoring
            ? ProfanityCatalog.ForConfiguration(configuration).Censor(lyrics)
            : lyrics;

        return new LyricResponse
        {
            Format = synced ? "lrc" : "txt",
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(text))
        };
    }

    private static int Score(LrcLibRecord record, LyricSearchRequest request, string? artist, int? durationSeconds)
    {
        var score = 0;
        if (SameText(record.TrackName, request.SongName))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(artist) && SameText(record.ArtistName, artist))
        {
            score += 4;
        }

        if (SameText(record.AlbumName, request.AlbumName))
        {
            score += 2;
        }

        if (durationSeconds is not null
            && record.Duration is not null
            && Math.Abs(record.Duration.Value - durationSeconds.Value) <= 2)
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(record.SyncedLyrics))
        {
            score += 1;
        }

        return score;
    }

    private static bool HasLyrics(LrcLibRecord record)
    {
        return !record.Instrumental
            && (IsUsableLyric(record.SyncedLyrics) || IsUsableLyric(record.PlainLyrics));
    }

    /// <summary>
    /// LRCLIB sometimes returns a duration match whose lyric text is a placeholder such as "probe".
    /// Those records are not usable lyrics.
    /// </summary>
    private static bool IsUsableLyric([NotNullWhen(true)] string? lyrics)
    {
        if (string.IsNullOrWhiteSpace(lyrics))
        {
            return false;
        }

        var text = LyricTags.Replace(lyrics, string.Empty).Trim();
        return text.Length >= 6
            && !string.Equals(text, "probe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseId(string id, out long recordId, out bool synced)
    {
        recordId = 0;
        synced = false;
        var split = id.Split('_', 2);
        if (split.Length != 2 || !long.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out recordId))
        {
            return false;
        }

        if (string.Equals(split[1], SyncedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            synced = true;
            return true;
        }

        return string.Equals(split[1], PlainSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstName(IReadOnlyList<string>? names)
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

    private static int? ToDurationSeconds(long? ticks)
    {
        if (ticks is null or <= 0)
        {
            return null;
        }

        var seconds = (int)Math.Round(TimeSpan.FromTicks(ticks.Value).TotalSeconds, MidpointRounding.AwayFromZero);
        if (seconds < 1)
        {
            return null;
        }

        return Math.Min(seconds, 3600);
    }

    private static bool SameText(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
