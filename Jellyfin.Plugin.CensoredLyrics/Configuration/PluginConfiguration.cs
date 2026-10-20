using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CensoredLyrics.Configuration;

/// <summary>
/// Words to censor and how they are masked.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// The public LRCLIB API.
    /// </summary>
    public const string DefaultBaseUrl = "https://lrclib.net";

    /// <summary>
    /// Gets or sets the words to censor, one per line.
    /// </summary>
    public string Words { get; set; } = Censor.ProfanityCatalog.DefaultWordText;

    /// <summary>
    /// Gets or sets how matched words are replaced.
    /// </summary>
    public string Style { get; set; } = CensorStyle.FirstLetter.ToString();

    /// <summary>
    /// Gets or sets the mask characters.
    /// </summary>
    public string Mask { get; set; } = MaskCharacters.Asterisk.ToString();

    /// <summary>
    /// Gets or sets the LRCLIB base URL.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Reads <see cref="Style"/>.
    /// </summary>
    /// <returns>The selected style, or first-letter when the saved value is unknown.</returns>
    public CensorStyle GetStyle()
    {
        return Enum.TryParse<CensorStyle>(Style, ignoreCase: true, out var style)
            ? style
            : CensorStyle.FirstLetter;
    }

    /// <summary>
    /// Reads <see cref="Mask"/>.
    /// </summary>
    /// <returns>The selected characters, or asterisks when the saved value is unknown.</returns>
    public MaskCharacters GetMask()
    {
        return Enum.TryParse<MaskCharacters>(Mask, ignoreCase: true, out var mask)
            ? mask
            : MaskCharacters.Asterisk;
    }
}
