using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CensoredLyrics.Configuration;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Masks words from the configured list. LRC timestamps are left unchanged.
/// </summary>
public sealed partial class LyricCensor
{
    private static readonly string[] Suffixes = ["ers", "ing", "es", "ed", "er", "in", "s"];

    private readonly HashSet<string> _banned;
    private readonly CensorStyle _style;
    private readonly MaskCharacters _mask;
    private readonly string _alphabet;

    private LyricCensor(HashSet<string> banned, CensorStyle style, MaskCharacters mask, string alphabet)
    {
        _banned = banned;
        _style = style;
        _mask = mask;
        _alphabet = alphabet;
    }

    /// <summary>
    /// Builds a censor from a word list.
    /// </summary>
    /// <param name="banned">Words that should be masked.</param>
    /// <param name="style">How matched words are replaced.</param>
    /// <param name="mask">Which characters replace the letters.</param>
    /// <returns>A censor for that list.</returns>
    public static LyricCensor Create(IEnumerable<string> banned, CensorStyle style, MaskCharacters mask)
    {
        var alphabet = mask switch
        {
            MaskCharacters.Hash => "#",
            MaskCharacters.Symbols => "@#?",
            MaskCharacters.Hyphen => "-",
            MaskCharacters.Underline => "_",
            _ => "*"
        };

        return new LyricCensor(NormalizeAll(banned), style, mask, alphabet);
    }

    /// <summary>
    /// Returns <paramref name="text"/> with listed words masked. Timestamps in square or angle brackets are preserved.
    /// </summary>
    /// <param name="text">Lyric text, plain or LRC.</param>
    /// <returns>The censored text.</returns>
    public string Censor(string? text)
    {
        if (string.IsNullOrEmpty(text) || _style == CensorStyle.None || _banned.Count == 0)
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

        if (_mask == MaskCharacters.Brackets)
        {
            var blanks = _style == CensorStyle.WholeWord ? token.Length : Math.Max(0, token.Length - 1);
            var inside = new string(' ', blanks);
            return _style == CensorStyle.WholeWord
                ? "[" + inside + "]"
                : token[0] + "[" + inside + "]";
        }

        var chars = token.ToCharArray();
        var start = _style == CensorStyle.WholeWord ? 0 : 1;
        for (var i = start; i < chars.Length; i++)
        {
            chars[i] = MaskAt(token, i);
        }

        return new string(chars);
    }

    private char MaskAt(string token, int index)
    {
        if (_alphabet.Length == 1)
        {
            return _alphabet[0];
        }

        var hash = 17;
        foreach (var character in token)
        {
            hash = (hash * 31) + character;
        }

        hash = (hash * 31) + index;
        var bucket = hash == int.MinValue ? 0 : Math.Abs(hash);
        return _alphabet[bucket % _alphabet.Length];
    }

    private bool IsProfane(string token)
    {
        var normalized = Normalize(token);
        if (normalized.Length < 3)
        {
            return false;
        }

        if (!normalized.Any(char.IsLetter))
        {
            return false;
        }

        return Matches(normalized);
    }

    private bool Matches(string word)
    {
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

            if (_banned.Contains(word[..^suffix.Length]))
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

    [GeneratedRegex(
        @"\[[^\]]*\]|<[^>]*>|(?<word>[\p{L}\d@$]+(?:['’][\p{L}\d@$]+)*)|[^\p{L}\d@$\[<]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
