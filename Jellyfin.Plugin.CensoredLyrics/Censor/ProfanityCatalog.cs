using Jellyfin.Plugin.CensoredLyrics.Configuration;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Builds a censor from the word list in the plugin settings.
/// </summary>
public static class ProfanityCatalog
{
    private const string StrongResource = "Jellyfin.Plugin.CensoredLyrics.profanity.txt";
    private const string MildResource = "Jellyfin.Plugin.CensoredLyrics.profanity-mild.txt";

    private static readonly object CacheGate = new();
    private static string? _cacheKey;
    private static LyricCensor? _cached;

    /// <summary>
    /// Gets the starting word list, one word per line.
    /// </summary>
    public static string DefaultWordText { get; } = string.Join(
        '\n',
        Load(StrongResource).Concat(Load(MildResource)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Creates a censor from an explicit word list.
    /// </summary>
    /// <param name="words">Words to censor.</param>
    /// <param name="style">How matched words are replaced.</param>
    /// <param name="mask">Which characters replace the letters.</param>
    /// <returns>A censor for that list.</returns>
    public static LyricCensor Create(IEnumerable<string> words, CensorStyle style, MaskCharacters mask)
    {
        return LyricCensor.Create(words, style, mask);
    }

    /// <summary>
    /// Returns the censor for the current plugin configuration.
    /// </summary>
    /// <param name="configuration">Plugin configuration. Defaults are used when this is null.</param>
    /// <returns>The censor for that configuration.</returns>
    public static LyricCensor ForConfiguration(PluginConfiguration? configuration)
    {
        configuration ??= new PluginConfiguration();
        var key = string.Join(
            '\n',
            configuration.GetStyle(),
            configuration.GetMask(),
            configuration.Words ?? string.Empty);

        lock (CacheGate)
        {
            if (_cached is not null && string.Equals(_cacheKey, key, StringComparison.Ordinal))
            {
                return _cached;
            }

            _cacheKey = key;
            _cached = Create(SplitWords(configuration.Words), configuration.GetStyle(), configuration.GetMask());
            return _cached;
        }
    }

    /// <summary>
    /// Splits a settings field into individual words.
    /// </summary>
    /// <param name="text">Newline-separated words.</param>
    /// <returns>The trimmed words.</returns>
    public static IEnumerable<string> SplitWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var part in text.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0 && !part.StartsWith('#'))
            {
                yield return part;
            }
        }
    }

    private static IReadOnlyList<string> Load(string resourceName)
    {
        using var stream = typeof(ProfanityCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded word list {resourceName}.");
        using var reader = new StreamReader(stream);
        var words = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                words.Add(trimmed);
            }
        }

        return words;
    }
}
