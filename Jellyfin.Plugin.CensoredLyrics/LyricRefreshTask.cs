using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Scheduled fetch of censored lyrics for songs that are not cached yet.
/// </summary>
public sealed class LyricRefreshTask : IScheduledTask
{
    private readonly LyricRefreshService _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="LyricRefreshTask"/> class.
    /// </summary>
    /// <param name="refresh">Refresh service.</param>
    public LyricRefreshTask(LyricRefreshService refresh)
    {
        _refresh = refresh;
    }

    /// <inheritdoc />
    public string Name => "Fetch censored lyrics";

    /// <inheritdoc />
    public string Key => "CensoredLrclibDaily";

    /// <inheritdoc />
    public string Description => "Fetches lyrics for songs that have not been fetched yet, then censors them.";

    /// <inheritdoc />
    public string Category => "DangerDrive";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
        };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        return _refresh.ExecuteAsync(false, progress, cancellationToken);
    }
}
