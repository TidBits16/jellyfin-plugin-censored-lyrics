using System.Globalization;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.CensoredLyrics.Configuration;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Masks words from the configured list. LRC timestamps are left unchanged.
/// </summary>
public sealed partial class LyricCensor
{
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
            var letters = token.Count(IsMaskedLetter);
            var blanks = _style == CensorStyle.WholeWord ? letters : Math.Max(0, letters - 1);
            var inside = new string(' ', blanks);
            var first = token.FirstOrDefault(IsMaskedLetter);
            var trailing = token[LetterEnd(token)..];
            return _style == CensorStyle.WholeWord
                ? "[" + inside + "]" + trailing
                : first + "[" + inside + "]" + trailing;
        }

        var chars = token.ToCharArray();
        var started = _style == CensorStyle.WholeWord;
        for (var i = 0; i < chars.Length; i++)
        {
            if (!IsMaskedLetter(chars[i]))
            {
                continue;
            }

            if (!started)
            {
                started = true;
                continue;
            }

            chars[i] = MaskAt(token, i);
        }

        return new string(chars);
    }

    private static bool IsMaskedLetter(char character)
    {
        return !IsApostrophe(character);
    }

    private static bool IsApostrophe(char character)
    {
        return character is '\'' or '\u2018' or '\u2019' or '\u02BC' or '`' or '\u00B4';
    }

    private static int LetterEnd(string token)
    {
        var end = token.Length;
        while (end > 0 && IsApostrophe(token[end - 1]))
        {
            end--;
        }

        return end;
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
        return _banned.Contains(Normalize(token));
    }

    private static HashSet<string> NormalizeAll(IEnumerable<string> words)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            var normalized = Normalize(word);
            if (normalized.Length > 0)
            {
                set.Add(normalized);
            }
        }

        return set;
    }

    private static string Normalize(string word)
    {
        var lowered = word.ToLower(CultureInfo.InvariantCulture);
        return lowered[..LetterEnd(lowered)];
    }

    [GeneratedRegex(
        @"\[[^\]]*\]|<[^>]*>|(?<word>[\p{L}\d@$]+(?:['’\u02BC][\p{L}\d@$]+)*['’\u02BC]*)|[^\p{L}\d@$\[<]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
