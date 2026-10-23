using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Starts a lyric fetch from the plugin settings page.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CensoredLyrics")]
public sealed class RefreshController : ControllerBase
{
    private readonly ITaskManager _tasks;
    private readonly LyricRefreshService _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshController"/> class.
    /// </summary>
    /// <param name="tasks">Jellyfin task manager.</param>
    /// <param name="refresh">Refresh service used by the overwrite button.</param>
    public RefreshController(ITaskManager tasks, LyricRefreshService refresh)
    {
        _tasks = tasks;
        _refresh = refresh;
    }

    /// <summary>
    /// Queues the daily Jellyfin task, or overwrites every song when <paramref name="force"/> is true.
    /// </summary>
    /// <param name="force">True to download every song again outside the scheduled task.</param>
    /// <returns>202 when the work is queued.</returns>
    [HttpPost("Run")]
    public IActionResult Run([FromQuery] bool force = false)
    {
        if (force)
        {
            _tasks.CancelIfRunning<LyricRefreshTask>();
            _ = Task.Run(() => _refresh.ExecuteAsync(true, null, CancellationToken.None));
        }
        else
        {
            _tasks.QueueIfNotRunning<LyricRefreshTask>();
        }

        return Accepted();
    }
}
