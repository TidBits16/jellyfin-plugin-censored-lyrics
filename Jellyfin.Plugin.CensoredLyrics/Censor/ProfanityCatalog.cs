using Jellyfin.Plugin.CensoredLyrics.Configuration;

namespace Jellyfin.Plugin.CensoredLyrics.Censor;

/// <summary>
/// Loads the built-in word lists and combines them with the plugin settings.
/// </summary>
public static class ProfanityCatalog
{
    private const string StrongResource = "Jellyfin.Plugin.CensoredLyrics.profanity.txt";
    private const string MildResource = "Jellyfin.Plugin.CensoredLyrics.profanity-mild.txt";

    private static readonly IReadOnlyList<string> StrongWords = Load(StrongResource);
    private static readonly IReadOnlyList<string> MildWords = Load(MildResource);
    private static readonly object CacheGate = new();
    private static string? _cacheKey;
    private static LyricCensor? _cached;

    /// <summary>
    /// Creates a censor from the built-in lists and optional extra or allowed words.
    /// </summary>
    /// <param name="includeMild">Whether damn, hell, crap, and the other mild words are included.</param>
    /// <param name="extra">Additional blocked words, separated by commas or new lines.</param>
    /// <param name="allowed">Words that must not be censored.</param>
    /// <returns>A censor for those lists.</returns>
    public static LyricCensor Create(bool includeMild, string? extra, string? allowed)
    {
        var banned = new List<string>(StrongWords);
        if (includeMild)
        {
            banned.AddRange(MildWords);
        }

        banned.AddRange(SplitWords(extra));
        return LyricCensor.Create(banned, SplitWords(allowed));
    }

    /// <summary>
    /// Returns the censor for the current plugin configuration, reusing it while the settings stay the same.
    /// </summary>
    /// <param name="configuration">Plugin configuration. Defaults are used when this is null.</param>
    /// <returns>The censor for that configuration.</returns>
    public static LyricCensor ForConfiguration(PluginConfiguration? configuration)
    {
        configuration ??= new PluginConfiguration();
        var key = string.Join(
            '\n',
            configuration.IncludeMildProfanity ? "1" : "0",
            configuration.ExtraWords ?? string.Empty,
            configuration.AllowedWords ?? string.Empty);

        lock (CacheGate)
        {
            if (_cached is not null && string.Equals(_cacheKey, key, StringComparison.Ordinal))
            {
                return _cached;
            }

            _cacheKey = key;
            _cached = Create(configuration.IncludeMildProfanity, configuration.ExtraWords, configuration.AllowedWords);
            return _cached;
        }
    }

    /// <summary>
    /// Splits a settings field into individual words.
    /// </summary>
    /// <param name="text">Comma- or newline-separated words.</param>
    /// <returns>The trimmed words.</returns>
    public static IEnumerable<string> SplitWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var part in text.Split([',', '\n', '\r', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0 && !part.StartsWith('#'))
            {
                yield return part;
            }
        }
    }

    private static IReadOnlyList<string> Load(string resourceName)
    {
        var assembly = typeof(ProfanityCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
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
