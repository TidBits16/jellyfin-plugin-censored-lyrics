namespace Jellyfin.Plugin.CensoredLyrics.Configuration;

/// <summary>
/// Characters used to mask a censored word.
/// </summary>
public enum MaskCharacters
{
    /// <summary>
    /// Asterisks.
    /// </summary>
    Asterisk,

    /// <summary>
    /// Hash marks.
    /// </summary>
    Hash,

    /// <summary>
    /// A mix of @, #, and ?.
    /// </summary>
    Symbols,

    /// <summary>
    /// Hyphens.
    /// </summary>
    Hyphen,

    /// <summary>
    /// Square brackets with a blank inside.
    /// </summary>
    Brackets,

    /// <summary>
    /// Underlines.
    /// </summary>
    Underline
}
