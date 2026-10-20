namespace Jellyfin.Plugin.CensoredLyrics.Configuration;

/// <summary>
/// How a matched word is replaced.
/// </summary>
public enum CensorStyle
{
    /// <summary>
    /// Leave the word as it is.
    /// </summary>
    None,

    /// <summary>
    /// Keep the first letter. "beans" becomes "b****".
    /// </summary>
    FirstLetter,

    /// <summary>
    /// Replace every letter. "beans" becomes "*****".
    /// </summary>
    WholeWord
}
