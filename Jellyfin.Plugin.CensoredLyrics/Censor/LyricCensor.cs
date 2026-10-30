using System.Globalization;
using System.Text;
using Jellyfin.Plugin.CensoredLyrics.Configuration;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Masks words from the configured list. Spaces and punctuation separate words.
/// LRC timestamps in square or angle brackets are left unchanged.
/// </summary>
public sealed class LyricCensor
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
    /// Returns <paramref name="text"/> with listed words masked.
    /// </summary>
    /// <param name="text">Lyric text, plain or LRC.</param>
    /// <returns>The censored text.</returns>
    public string Censor(string? text)
    {
        if (string.IsNullOrEmpty(text) || _style == CensorStyle.None || _banned.Count == 0)
        {
            return text ?? string.Empty;
        }

        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length;)
        {
            if (TryCopyTag(text, result, ref i))
            {
                continue;
            }

            if (!IsWordCharacter(text[i]))
            {
                result.Append(text[i]);
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length && IsWordCharacter(text[i]))
            {
                i++;
            }

            var word = text[start..i];
            result.Append(_banned.Contains(Normalize(word)) ? Mask(word) : word);
        }

        return result.ToString();
    }

    private static bool TryCopyTag(string text, StringBuilder result, ref int index)
    {
        if (text[index] is not ('[' or '<'))
        {
            return false;
        }

        var close = text[index] == '[' ? ']' : '>';
        var end = text.IndexOf(close, index + 1);
        if (end < 0)
        {
            return false;
        }

        result.Append(text, index, end - index + 1);
        index = end + 1;
        return true;
    }

    private static bool IsWordCharacter(char character)
    {
        return char.IsLetterOrDigit(character);
    }

    private string Mask(string word)
    {
        if (_mask == MaskCharacters.Brackets)
        {
            var blanks = _style == CensorStyle.WholeWord ? word.Length : Math.Max(0, word.Length - 1);
            var inside = new string(' ', blanks);
            return _style == CensorStyle.WholeWord
                ? "[" + inside + "]"
                : word[0] + "[" + inside + "]";
        }

        var chars = word.ToCharArray();
        for (var i = _style == CensorStyle.WholeWord ? 0 : 1; i < chars.Length; i++)
        {
            chars[i] = MaskAt(word, i);
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
        return word.ToLower(CultureInfo.InvariantCulture);
    }
}
