using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Checks whether lyric text is a real lyric rather than an empty or placeholder record.
/// </summary>
public static partial class LyricText
{
    /// <summary>
    /// Returns true when <paramref name="lyrics"/> has enough text to save.
    /// </summary>
    /// <param name="lyrics">Plain text or LRC.</param>
    /// <returns>True when the text is usable.</returns>
    public static bool IsUsable([NotNullWhen(true)] string? lyrics)
    {
        if (string.IsNullOrWhiteSpace(lyrics))
        {
            return false;
        }

        var text = Tags().Replace(lyrics, string.Empty).Trim();
        return text.Length >= 6
            && !string.Equals(text, "probe", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\[[^\]]*\]|<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}
