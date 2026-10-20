using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Masks whole-word profanity while keeping the first letter and the original length.
/// "fuck" becomes "f***". LRC timestamp tags are left unchanged.
/// </summary>
public sealed partial class LyricCensor
{
    private static readonly string[] Suffixes = ["ers", "ing", "es", "ed", "er", "in", "s"];

    private readonly HashSet<string> _banned;
    private readonly HashSet<string> _allowed;

    private LyricCensor(HashSet<string> banned, HashSet<string> allowed)
    {
        _banned = banned;
        _allowed = allowed;
    }

    /// <summary>
    /// Builds a censor from word lists. Matching is case-insensitive.
    /// </summary>
    /// <param name="banned">Words that should be masked.</param>
    /// <param name="allowed">Words that should be left alone, even if they are also banned.</param>
    /// <returns>A censor that applies both lists.</returns>
    public static LyricCensor Create(IEnumerable<string> banned, IEnumerable<string> allowed)
    {
        return new LyricCensor(NormalizeAll(banned), NormalizeAll(allowed));
    }

    /// <summary>
    /// Returns <paramref name="text"/> with profanity masked. Timestamps in square or angle brackets are preserved.
    /// </summary>
    /// <param name="text">Lyric text, plain or LRC.</param>
    /// <returns>The censored text.</returns>
    public string Censor(string? text)
    {
        if (string.IsNullOrEmpty(text) || _banned.Count == 0)
        {
            return text ?? string.Empty;
        }

        return TokenPattern().Replace(text, match =>
        {
            if (match.Groups["word"].Success)
            {
                return CensorToken(match.Value);
            }

            return match.Value;
        });
    }

    private string CensorToken(string token)
    {
        if (!IsProfane(token))
        {
            return token;
        }

        var chars = token.ToCharArray();
        for (var i = 1; i < chars.Length; i++)
        {
            chars[i] = '*';
        }

        return new string(chars);
    }

    private bool IsProfane(string token)
    {
        var normalized = Normalize(token);
        if (normalized.Length < 3 || _allowed.Contains(normalized))
        {
            return false;
        }

        if (!normalized.Any(char.IsLetter))
        {
            return false;
        }

        if (Matches(normalized))
        {
            return true;
        }

        var collapsedToTwo = CollapseRuns(normalized, 3, 2);
        if (!string.Equals(collapsedToTwo, normalized, StringComparison.Ordinal)
            && !_allowed.Contains(collapsedToTwo)
            && Matches(collapsedToTwo))
        {
            return true;
        }

        var collapsedToOne = CollapseRuns(normalized, 2, 1);
        return !string.Equals(collapsedToOne, normalized, StringComparison.Ordinal)
            && !_allowed.Contains(collapsedToOne)
            && Matches(collapsedToOne);
    }

    private bool Matches(string word)
    {
        if (_allowed.Contains(word))
        {
            return false;
        }

        if (_banned.Contains(word))
        {
            return true;
        }

        foreach (var suffix in Suffixes)
        {
            if (word.Length <= suffix.Length + 2 || !word.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var stem = word[..^suffix.Length];
            if (_allowed.Contains(stem))
            {
                return false;
            }

            if (_banned.Contains(stem))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> NormalizeAll(IEnumerable<string> words)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var normalized = Normalize(word);
            if (normalized.Length >= 3)
            {
                set.Add(normalized);
            }
        }

        return set;
    }

    private static string Normalize(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var character in word.ToLower(CultureInfo.InvariantCulture))
        {
            switch (character)
            {
                case '\'':
                case '’':
                    break;
                case '@':
                case '4':
                    builder.Append('a');
                    break;
                case '0':
                    builder.Append('o');
                    break;
                case '1':
                    builder.Append('i');
                    break;
                case '3':
                    builder.Append('e');
                    break;
                case '5':
                case '$':
                    builder.Append('s');
                    break;
                case '7':
                    builder.Append('t');
                    break;
                default:
                    if (char.IsLetter(character))
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static string CollapseRuns(string word, int minimumRun, int keep)
    {
        var builder = new StringBuilder(word.Length);
        var index = 0;
        while (index < word.Length)
        {
            var runEnd = index + 1;
            while (runEnd < word.Length && word[runEnd] == word[index])
            {
                runEnd++;
            }

            var runLength = runEnd - index;
            var copies = runLength >= minimumRun ? keep : runLength;
            builder.Append(word[index], copies);
            index = runEnd;
        }

        return builder.ToString();
    }

    [GeneratedRegex(
        @"\[[^\]]*\]|<[^>]*>|(?<word>[\p{L}\d@$]+(?:['’][\p{L}\d@$]+)*)|[^\p{L}\d@$\[<]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
