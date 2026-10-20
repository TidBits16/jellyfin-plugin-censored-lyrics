namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Progress of a lyric refresh.
/// </summary>
public sealed class RefreshStatus
{
    /// <summary>
    /// Gets or sets a value indicating whether a refresh is running.
    /// </summary>
    public bool Running { get; set; }

    /// <summary>
    /// Gets or sets how many songs have been visited.
    /// </summary>
    public int Processed { get; set; }

    /// <summary>
    /// Gets or sets how many songs are in the library.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets how many lyric files were written.
    /// </summary>
    public int Written { get; set; }

    /// <summary>
    /// Gets or sets the error that stopped the refresh.
    /// </summary>
    public string? Error { get; set; }
}
