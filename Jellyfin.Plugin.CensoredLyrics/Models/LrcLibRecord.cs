using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CensoredLyrics.Models;

/// <summary>
/// One lyrics record from the LRCLIB API.
/// </summary>
public sealed class LrcLibRecord
{
    /// <summary>
    /// Gets or sets the stable LRCLIB id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the track title.
    /// </summary>
    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    /// <summary>
    /// Gets or sets the album name.
    /// </summary>
    [JsonPropertyName("albumName")]
    public string? AlbumName { get; set; }

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the track is instrumental.
    /// </summary>
    [JsonPropertyName("instrumental")]
    public bool Instrumental { get; set; }

    /// <summary>
    /// Gets or sets unsynced lyrics.
    /// </summary>
    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    /// <summary>
    /// Gets or sets LRC lyrics with line timestamps.
    /// </summary>
    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }
}
