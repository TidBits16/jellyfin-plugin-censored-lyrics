using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CensoredLyrics;

/// <summary>
/// Jellyfin plugin that supplies censored timed lyrics from LRCLIB.
/// </summary>
public sealed class CensoredLyricsPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Name of the HTTP client used for every LRCLIB request.
    /// </summary>
    public const string HttpClientName = "CensoredLyrics";

    /// <summary>
    /// Initializes a new instance of the <see cref="CensoredLyricsPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer used for the plugin configuration.</param>
    public CensoredLyricsPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        if (Configuration.WordListVersion < 7)
        {
            Configuration.Words = ProfanityCatalog.DefaultWordText;
            Configuration.WordListVersion = 7;
            SaveConfiguration();
        }
    }

    /// <summary>
    /// Gets the running plugin instance.
    /// </summary>
    public static CensoredLyricsPlugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Censored LRCLIB";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("d045e257-f274-46e5-a2a7-b88f72c725e9");

    /// <inheritdoc />
    public override string Description =>
        "Downloads timed lyrics from LRCLIB and masks swear words before Jellyfin saves them.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = "Jellyfin.Plugin.CensoredLyrics.Configuration.config.html"
        };
    }
}
