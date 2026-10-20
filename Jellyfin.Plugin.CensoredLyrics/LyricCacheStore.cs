using System.Text.Json;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Remembers the original lyrics already fetched for each song.
/// </summary>
public sealed class LyricCacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, LyricCacheEntry> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="LyricCacheStore"/> class.
    /// </summary>
    /// <param name="path">Path of the cache file.</param>
    public LyricCacheStore(string path)
    {
        _path = path;
        _items = Load(path);
    }

    /// <summary>
    /// Reads a cached song.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="entry">The cached lyrics when this returns true.</param>
    /// <returns>True when this song has already been fetched.</returns>
    public bool TryGet(Guid itemId, out LyricCacheEntry entry)
    {
        lock (_gate)
        {
            return _items.TryGetValue(Key(itemId), out entry!);
        }
    }

    /// <summary>
    /// Stores a song and writes the cache file.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="entry">Original lyrics, or an empty entry when LRCLIB had none.</param>
    public void Set(Guid itemId, LyricCacheEntry entry)
    {
        lock (_gate)
        {
            _items[Key(itemId)] = entry;
            Save();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_items, JsonOptions));
        File.Move(temporary, _path, overwrite: true);
    }

    private static Dictionary<string, LyricCacheEntry> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, LyricCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }

            var items = JsonSerializer.Deserialize<Dictionary<string, LyricCacheEntry>>(File.ReadAllText(path), JsonOptions);
            return items ?? new Dictionary<string, LyricCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new Dictionary<string, LyricCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Key(Guid itemId)
    {
        return itemId.ToString("D");
    }
}

/// <summary>
/// Original lyrics stored for one song.
/// </summary>
public sealed class LyricCacheEntry
{
    /// <summary>
    /// Gets or sets the synced LRC.
    /// </summary>
    public string? Synced { get; set; }

    /// <summary>
    /// Gets or sets the plain lyrics.
    /// </summary>
    public string? Plain { get; set; }

    /// <summary>
    /// Gets or sets the settings fingerprint last written to the song file.
    /// </summary>
    public string? Applied { get; set; }
}
