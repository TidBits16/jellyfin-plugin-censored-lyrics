using Jellyfin.Plugin.CensoredLyrics.Censor;
using Jellyfin.Plugin.CensoredLyrics.Configuration;
using Xunit;

namespace Jellyfin.Plugin.CensoredLyrics.Tests;

public class LyricCensorTests
{
    private static LyricCensor Censor(CensorStyle style = CensorStyle.FirstLetter, MaskCharacters mask = MaskCharacters.Asterisk)
    {
        return ProfanityCatalog.Create(ProfanityCatalog.SplitWords(ProfanityCatalog.DefaultWordText), style, mask);
    }

    [Fact]
    public void Masks_fuck_and_keeps_the_first_letter()
    {
        var censor = Censor();

        Assert.Equal("f***", censor.Censor("fuck"));
        Assert.Equal("F***", censor.Censor("Fuck"));
        Assert.Equal("F***", censor.Censor("FUCK"));
        Assert.Equal("f******", censor.Censor("fucking"));
        Assert.Equal("f*****", censor.Censor("fucker"));
        Assert.Equal("f*****", censor.Censor("fuckin"));
        Assert.Equal("fucked", censor.Censor("fucked"));
        Assert.Equal("motherfucker", censor.Censor("motherfucker"));
    }

    [Fact]
    public void Whole_word_and_hash_and_symbols_follow_the_setting()
    {
        Assert.Equal("beans", Censor(CensorStyle.None).Censor("beans"));
        Assert.Equal("****", Censor(CensorStyle.WholeWord).Censor("fuck"));
        Assert.Equal("f###", Censor(CensorStyle.FirstLetter, MaskCharacters.Hash).Censor("fuck"));
        Assert.Equal("####", Censor(CensorStyle.WholeWord, MaskCharacters.Hash).Censor("fuck"));
        Assert.Equal("f---", Censor(CensorStyle.FirstLetter, MaskCharacters.Hyphen).Censor("fuck"));
        Assert.Equal("----", Censor(CensorStyle.WholeWord, MaskCharacters.Hyphen).Censor("fuck"));
        Assert.Equal("f___", Censor(CensorStyle.FirstLetter, MaskCharacters.Underline).Censor("fuck"));
        Assert.Equal("____", Censor(CensorStyle.WholeWord, MaskCharacters.Underline).Censor("fuck"));
        Assert.Equal("f[   ]", Censor(CensorStyle.FirstLetter, MaskCharacters.Brackets).Censor("fuck"));
        Assert.Equal("[    ]", Censor(CensorStyle.WholeWord, MaskCharacters.Brackets).Censor("fuck"));

        var symbols = Censor(CensorStyle.FirstLetter, MaskCharacters.Symbols).Censor("fuck");
        Assert.Equal('f', symbols[0]);
        Assert.All(symbols[1..], character => Assert.Contains(character, "@#?"));
        Assert.Equal(symbols, Censor(CensorStyle.FirstLetter, MaskCharacters.Symbols).Censor("fuck"));
    }

    [Fact]
    public void Keeps_timestamps_and_censors_the_line()
    {
        var censor = Censor();
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
        var censor = Censor();
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
    public void Masks_a_listed_word_and_leaves_other_spellings()
    {
        var censor = Censor();

        Assert.Equal("s***!", censor.Censor("shit!"));
        Assert.Equal("a**", censor.Censor("ass"));
        Assert.Equal("asshole", censor.Censor("asshole"));
        Assert.Equal("sh1t", censor.Censor("sh1t"));
        Assert.Equal("a$$", censor.Censor("a$$"));
        Assert.Equal("@ss", censor.Censor("@ss"));
        Assert.Equal("f*****'", censor.Censor("fuckin'"));
        Assert.Equal("b*******", censor.Censor("bitching"));
        Assert.Equal("s******", censor.Censor("shitter"));
        Assert.Equal("d******", censor.Censor("damning"));
        Assert.Equal("bitches", censor.Censor("bitches"));
        Assert.Equal("damned", censor.Censor("damned"));
        Assert.Equal("fuuuck", censor.Censor("fuuuck"));
        Assert.Equal("asss", censor.Censor("asss"));
    }

    [Fact]
    public void The_word_list_is_what_gets_censored()
    {
        var censor = ProfanityCatalog.Create(["beans"], CensorStyle.FirstLetter, MaskCharacters.Asterisk);

        Assert.Equal("b****", censor.Censor("beans"));
        Assert.Equal("B****", censor.Censor("Beans"));
        Assert.Equal("beaning", censor.Censor("beaning"));
        Assert.Equal("be4ns", censor.Censor("be4ns"));
        Assert.Equal("fuck", censor.Censor("fuck"));
        Assert.Equal(
            ["fuck", "fucker", "fucking", "fuckin", "shit", "shitter", "shitting", "shittin", "ass", "damn", "damning", "nigger", "nigga", "bitch", "bitching", "bitchin", "dick", "cunt", "fag", "faggot"],
            ProfanityCatalog.DefaultWordText.Split('\n'));
    }

    [Fact]
    public void Cache_remembers_songs_that_were_fetched()
    {
        var path = Path.Combine(Path.GetTempPath(), "censored-lyrics-cache-" + Guid.NewGuid().ToString("N") + ".json");
        var id = Guid.NewGuid();
        var store = new LyricCacheStore(path);
        Assert.False(store.TryGet(id, out _));

        store.Set(id, new LyricCacheEntry { Synced = "[00:01.00] hello", Plain = "hello" });

        var loaded = new LyricCacheStore(path);
        Assert.True(loaded.TryGet(id, out var entry));
        Assert.Equal("[00:01.00] hello", entry.Synced);
        Assert.False(loaded.TryGet(Guid.NewGuid(), out _));
        File.Delete(path);
    }

    [Fact]
    public void Embedded_lists_are_packaged_with_the_plugin()
    {
        var names = typeof(ProfanityCatalog).Assembly.GetManifestResourceNames();

        Assert.Contains("Jellyfin.Plugin.CensoredLyrics.profanity.txt", names);
        Assert.Contains("Jellyfin.Plugin.CensoredLyrics.Configuration.config.html", names);
    }
}
