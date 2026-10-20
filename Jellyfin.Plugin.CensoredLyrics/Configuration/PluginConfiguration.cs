using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CensoredLyrics.Configuration;

/// <summary>
/// Settings for LRCLIB lookup and profanity censoring.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The public LRCLIB API.
    /// </summary>
    public const string DefaultBaseUrl = "https://lrclib.net";

    /// <summary>
    /// Gets or sets a value indicating whether fetched lyrics are censored.
    /// </summary>
    public bool EnableCensoring { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether milder words such as damn, hell, crap, and bloody are censored.
    /// </summary>
    public bool IncludeMildProfanity { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether synced lyrics are preferred over plain lyrics.
    /// </summary>
    public bool PreferSyncedLyrics { get; set; } = true;

    /// <summary>
    /// Gets or sets extra words to censor, one per line or separated by commas.
    /// </summary>
    public string ExtraWords { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets words that should never be censored, one per line or separated by commas.
    /// </summary>
    public string AllowedWords { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the LRCLIB base URL. Use this for a self-hosted LRCLIB instance.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
