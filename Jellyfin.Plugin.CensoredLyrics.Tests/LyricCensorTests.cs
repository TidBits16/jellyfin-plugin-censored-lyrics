using Jellyfin.Plugin.CensoredLyrics.Censor;
using Xunit;

namespace Jellyfin.Plugin.CensoredLyrics.Tests;

public class LyricCensorTests
{
    private static LyricCensor Strict()
    {
        return ProfanityCatalog.Create(includeMild: true, extra: null, allowed: null);
    }

    [Fact]
    public void Masks_fuck_and_keeps_the_first_letter()
    {
        var censor = Strict();

        Assert.Equal("f***", censor.Censor("fuck"));
        Assert.Equal("F***", censor.Censor("Fuck"));
        Assert.Equal("F***", censor.Censor("FUCK"));
        Assert.Equal("f******", censor.Censor("fucking"));
        Assert.Equal("m***********", censor.Censor("motherfucker"));
    }

    [Fact]
    public void Keeps_timestamps_and_censors_the_line()
    {
        var censor = Strict();
        const string line = "[00:17.12] I feel the fuck in the room";

        Assert.Equal("[00:17.12] I feel the f*** in the room", censor.Censor(line));
        Assert.Equal("[00:01.00][00:02.50] f***", censor.Censor("[00:01.00][00:02.50] fuck"));
        Assert.Equal("<00:12.34>f*** you", censor.Censor("<00:12.34>fuck you"));
        Assert.Equal("[ar:Artist Name]", censor.Censor("[ar:Artist Name]"));
        Assert.Equal("[ti:Title]", censor.Censor("[ti:Title]"));
    }

    [Fact]
    public void Leaves_ordinary_words_alone()
    {
        var censor = Strict();
        string[] safe =
        [
            "class", "classic", "assault", "assembly", "assassin", "assess", "assert", "assume",
            "cocktail", "peacock", "bass", "pass", "password", "hello", "shell", "spicy",
            "Dickens", "document", "cucumber", "scunthorpe", "therapist", "glass", "success",
            "access", "book", "good", "coffee", "little", "better", "balloon", "committee",
            "happy", "sorry", "look", "cool", "title", "titan", "analysis", "analog", "canal",
            "arsenal", "shoe", "come", "assistant", "passion", "compass", "massive", "embassy"
        ];

        foreach (var word in safe)
        {
            Assert.Equal(word, censor.Censor(word));
        }
    }

    [Fact]
    public void Masks_suffixes_punctuation_and_stretched_spellings()
    {
        var censor = Strict();

        Assert.Equal("f*****'", censor.Censor("fuckin'"));
        Assert.Equal("s***!", censor.Censor("shit!"));
        Assert.Equal("b******", censor.Censor("bitches"));
        Assert.Equal("a**", censor.Censor("ass"));
        Assert.Equal("a******", censor.Censor("asshole"));
        Assert.Equal("s***", censor.Censor("sh1t"));
        Assert.Equal("a**", censor.Censor("a$$"));
        Assert.Equal("@**", censor.Censor("@ss"));
        Assert.Equal("f*****", censor.Censor("fuuuck"));
        Assert.Equal("a***", censor.Censor("asss"));
    }

    [Fact]
    public void Mild_words_follow_the_setting()
    {
        var withMild = ProfanityCatalog.Create(true, null, null);
        var withoutMild = ProfanityCatalog.Create(false, null, null);

        Assert.Equal("h***", withMild.Censor("hell"));
        Assert.Equal("d***", withMild.Censor("damn"));
        Assert.Equal("hell", withoutMild.Censor("hell"));
        Assert.Equal("damn", withoutMild.Censor("damn"));
        Assert.Equal("f***", withoutMild.Censor("fuck"));
        Assert.Equal("hello", withMild.Censor("hello"));
    }

    [Fact]
    public void Extra_and_allowed_words_override_the_built_in_list()
    {
        var censor = ProfanityCatalog.Create(true, "yeet", "fuck");

        Assert.Equal("y***", censor.Censor("yeet"));
        Assert.Equal("Y***", censor.Censor("Yeet"));
        Assert.Equal("fuck", censor.Censor("fuck"));
        Assert.Equal("fucking", censor.Censor("fucking"));
        Assert.Equal("s***", censor.Censor("shit"));
    }

    [Fact]
    public void Embedded_lists_are_packaged_with_the_plugin()
    {
        var names = typeof(ProfanityCatalog).Assembly.GetManifestResourceNames();

        Assert.Contains("Jellyfin.Plugin.CensoredLyrics.profanity.txt", names);
        Assert.Contains("Jellyfin.Plugin.CensoredLyrics.profanity-mild.txt", names);
        Assert.Contains("Jellyfin.Plugin.CensoredLyrics.Configuration.config.html", names);
    }
}
