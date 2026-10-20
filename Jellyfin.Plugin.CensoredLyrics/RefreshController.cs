using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Starts a library lyric refresh from the plugin settings page.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("CensoredLyrics")]
public sealed class RefreshController : ControllerBase
{
    private readonly LyricRefreshService _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshController"/> class.
    /// </summary>
    /// <param name="refresh">Refresh service.</param>
    public RefreshController(LyricRefreshService refresh)
    {
        _refresh = refresh;
    }

    /// <summary>
    /// Starts a refresh. Songs already fetched are skipped unless <paramref name="force"/> is true.
    /// </summary>
    /// <param name="force">True to fetch every song again and overwrite its lyrics.</param>
    /// <returns>202 when the refresh starts, or 409 when one is already running.</returns>
    [HttpPost("Run")]
    public IActionResult Run([FromQuery] bool force = false)
    {
        if (!_refresh.TryStart(force))
        {
            return Conflict(GetStatus());
        }

        return Accepted(GetStatus());
    }

    /// <summary>
    /// Gets refresh progress.
    /// </summary>
    /// <returns>The current status.</returns>
    [HttpGet("Status")]
    public ActionResult<RefreshStatus> GetStatus()
    {
        return _refresh.GetStatus();
    }
}
