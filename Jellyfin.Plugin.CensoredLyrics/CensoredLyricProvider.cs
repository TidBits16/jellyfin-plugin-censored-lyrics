using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using Jellyfin.Plugin.CensoredLyrics.Models;
using MediaBrowser.Controller.Lyrics;
using MediaBrowser.Model.Lyrics;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Downloads timed lyrics from LRCLIB and censors the configured words before Jellyfin stores them.
/// </summary>
public sealed class CensoredLyricProvider : ILyricProvider
{
    private const string SyncedSuffix = "synced";
    private const string PlainSuffix = "plain";

    private readonly LyricLookup _lookup;
    private readonly LrcLibClient _client;
    private readonly ILogger<CensoredLyricProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CensoredLyricProvider"/> class.
    /// </summary>
    /// <param name="lookup">LRCLIB lookup.</param>
    /// <param name="httpClientFactory">HTTP client factory used when Jellyfin asks for one lyric id.</param>
    /// <param name="logger">Logger.</param>
    public CensoredLyricProvider(LyricLookup lookup, IHttpClientFactory httpClientFactory, ILogger<CensoredLyricProvider> logger)
    {
        _lookup = lookup;
        _client = new LrcLibClient(httpClientFactory);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => CensoredLyricsPlugin.Instance?.Name ?? "Censored Lyrics";

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteLyricInfo>> SearchAsync(LyricSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configuration = CurrentConfiguration();
        var records = await _lookup.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        return records.SelectMany(record => ToRemoteLyrics(record, configuration)).ToList();
    }

    /// <inheritdoc />
    public async Task<LyricResponse?> GetLyricsAsync(string id, CancellationToken cancellationToken)
    {
        if (!TryParseId(id, out var recordId, out var synced))
        {
            return null;
        }

        var configuration = CurrentConfiguration();
        try
        {
            var record = await _client.GetByIdAsync(LyricLookup.ResolveBaseUrl(configuration), recordId, cancellationToken)
                .ConfigureAwait(false);
            if (record is null)
            {
                return null;
            }

            var lyrics = synced ? record.SyncedLyrics : record.PlainLyrics;
            if (!LyricText.IsUsable(lyrics))
            {
                synced = !synced;
                lyrics = synced ? record.SyncedLyrics : record.PlainLyrics;
            }

            if (!LyricText.IsUsable(lyrics))
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

    private static PluginConfiguration CurrentConfiguration()
    {
        return CensoredLyricsPlugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    private static IEnumerable<RemoteLyricInfo> ToRemoteLyrics(LrcLibRecord record, PluginConfiguration configuration)
    {
        var synced = CreateInfo(record, record.SyncedLyrics, true, configuration);
        var plain = CreateInfo(record, record.PlainLyrics, false, configuration);
        if (synced is not null)
        {
            yield return synced;
        }

        if (plain is not null)
        {
            yield return plain;
        }
    }

    private static RemoteLyricInfo? CreateInfo(LrcLibRecord record, string? lyrics, bool synced, PluginConfiguration configuration)
    {
        if (!LyricText.IsUsable(lyrics))
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
        return new LyricResponse
        {
            Format = synced ? "lrc" : "txt",
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(ProfanityCatalog.ForConfiguration(configuration).Censor(lyrics)))
        };
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
}
