using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Lyrics;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Fetches lyrics for the music library. Songs already in the cache are not fetched again.
/// </summary>
public sealed class LyricRefreshService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILyricManager _lyricManager;
    private readonly LyricLookup _lookup;
    private readonly ILogger<LyricRefreshService> _logger;
    private int _running;

    /// <summary>
    /// Initializes a new instance of the <see cref="LyricRefreshService"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="lyricManager">Lyric manager.</param>
    /// <param name="lookup">LRCLIB lookup.</param>
    /// <param name="logger">Logger.</param>
    public LyricRefreshService(
        ILibraryManager libraryManager,
        ILyricManager lyricManager,
        LyricLookup lookup,
        ILogger<LyricRefreshService> logger)
    {
        _libraryManager = libraryManager;
        _lyricManager = lyricManager;
        _lookup = lookup;
        _logger = logger;
    }

    /// <summary>
    /// Runs a refresh and reports progress to the Jellyfin task. Songs already fetched are skipped unless <paramref name="force"/> is true.
    /// </summary>
    /// <param name="force">True to fetch every song again and overwrite its lyrics.</param>
    /// <param name="progress">Progress from 0 to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the refresh finishes.</returns>
    public async Task ExecuteAsync(bool force, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        while (!Begin())
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        await FinishAsync(force, progress, cancellationToken).ConfigureAwait(false);
    }

    private bool Begin()
    {
        return Interlocked.CompareExchange(ref _running, 1, 0) == 0;
    }

    private async Task FinishAsync(bool force, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(force, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private async Task RunAsync(bool force, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        try
        {
            var plugin = CensoredLyricsPlugin.Instance;
            if (plugin is null)
            {
                _logger.LogError("Lyric refresh failed because the plugin is not loaded");
                return;
            }

            var configuration = plugin.Configuration;
            var censor = ProfanityCatalog.ForConfiguration(configuration);
            var fingerprint = Fingerprint(configuration);
            var cache = new LyricCacheStore(Path.Combine(plugin.DataFolderPath, "lyric-cache.json"));
            var songs = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Audio],
                MediaTypes = [MediaType.Audio],
                Recursive = true,
                IsFolder = false,
                IsVirtualItem = false
            }).OfType<Audio>().ToList();

            var processed = 0;
            progress?.Report(songs.Count == 0 ? 100 : 0);
            foreach (var song in songs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await RefreshSongAsync(song, cache, censor, configuration, fingerprint, force, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Failed to refresh lyrics for {Song}", song.Name);
                }

                processed++;
                if (songs.Count > 0)
                {
                    progress?.Report(100d * processed / songs.Count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lyric refresh failed");
        }
    }

    private async Task<bool> RefreshSongAsync(
        Audio song,
        LyricCacheStore cache,
        LyricCensor censor,
        PluginConfiguration configuration,
        string fingerprint,
        bool force,
        CancellationToken cancellationToken)
    {
        LyricCacheEntry entry;
        if (!force && cache.TryGet(song.Id, out var cached))
        {
            entry = cached;
        }
        else
        {
            var found = await _lookup.FindAsync(
                configuration,
                song.Name,
                LyricLookup.FirstName(song.Artists) ?? LyricLookup.FirstName(song.AlbumArtists),
                song.Album,
                LyricLookup.ToDurationSeconds(song.RunTimeTicks),
                cancellationToken).ConfigureAwait(false);

            if (!found.Retrieved)
            {
                return false;
            }

            entry = new LyricCacheEntry
            {
                Synced = found.Synced,
                Plain = found.Plain
            };
            cache.Set(song.Id, entry);
        }

        if (!TryChoose(entry, out var original, out var synced))
        {
            if (force)
            {
                await DeleteQuietlyAsync(song).ConfigureAwait(false);
            }

            return false;
        }

        var applied = fingerprint + "\n" + original;
        if (!force && string.Equals(entry.Applied, applied, StringComparison.Ordinal) && song.HasLyrics == true)
        {
            return false;
        }

        var text = censor.Censor(original);
        await DeleteQuietlyAsync(song).ConfigureAwait(false);
        await _lyricManager.SaveLyricAsync(song, synced ? "lrc" : "txt", text).ConfigureAwait(false);
        entry.Applied = applied;
        cache.Set(song.Id, entry);
        return true;
    }

    private async Task DeleteQuietlyAsync(Audio song)
    {
        try
        {
            await _lyricManager.DeleteLyricsAsync(song).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not remove existing lyrics for {Song}", song.Name);
        }
    }

    private static bool TryChoose(LyricCacheEntry entry, out string lyrics, out bool synced)
    {
        if (LyricText.IsUsable(entry.Synced))
        {
            lyrics = entry.Synced;
            synced = true;
            return true;
        }

        if (LyricText.IsUsable(entry.Plain))
        {
            lyrics = entry.Plain;
            synced = false;
            return true;
        }

        lyrics = string.Empty;
        synced = false;
        return false;
    }

    private static string Fingerprint(PluginConfiguration configuration)
    {
        return string.Join('\n', configuration.GetStyle(), configuration.GetMask(), configuration.Words ?? string.Empty);
    }
}
