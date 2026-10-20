using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Manual Jellyfin task that downloads every song again and overwrites its lyrics.
/// </summary>
public sealed class LyricForceRefreshTask : IScheduledTask
{
    private readonly LyricRefreshService _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="LyricForceRefreshTask"/> class.
    /// </summary>
    /// <param name="refresh">Refresh service.</param>
    public LyricForceRefreshTask(LyricRefreshService refresh)
    {
        _refresh = refresh;
    }

    /// <inheritdoc />
    public string Name => "Force refresh censored lyrics";

    /// <inheritdoc />
    public string Key => "CensoredLrclibForce";

    /// <inheritdoc />
    public string Description => "Downloads every song again and overwrites its lyrics.";

    /// <inheritdoc />
    public string Category => "Censored LRCLIB";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return _refresh.ExecuteAsync(true, progress, cancellationToken);
    }
}
