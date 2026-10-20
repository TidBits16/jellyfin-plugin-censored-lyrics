using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Queues the Jellyfin lyric tasks from the plugin settings page.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CensoredLyrics")]
public sealed class RefreshController : ControllerBase
{
    private readonly ITaskManager _tasks;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshController"/> class.
    /// </summary>
    /// <param name="tasks">Jellyfin task manager.</param>
    public RefreshController(ITaskManager tasks)
    {
        _tasks = tasks;
    }

    /// <summary>
    /// Queues a Jellyfin scheduled task. Songs already fetched are skipped unless <paramref name="force"/> is true.
    /// </summary>
    /// <param name="force">True to queue the overwrite task.</param>
    /// <returns>202 when the task is queued.</returns>
    [HttpPost("Run")]
    public IActionResult Run([FromQuery] bool force = false)
    {
        if (force)
        {
            _tasks.CancelIfRunning<LyricRefreshTask>();
            _tasks.QueueScheduledTask<LyricForceRefreshTask>();
        }
        else
        {
            _tasks.QueueIfNotRunning<LyricRefreshTask>();
        }

        return Accepted();
    }
}
