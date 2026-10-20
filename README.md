# Censored Lyrics

Jellyfin plugin that downloads **timed lyrics from [LRCLIB](https://lrclib.net)** and censors swear words before Jellyfin saves them.

`fuck` is stored as `f***`. The first letter stays, the rest of the word becomes asterisks, and the LRC timestamps stay put:

```text
[00:17.12] what the fuck
[00:17.12] what the f***
```

Jellyfin's own lyric parser still reads the timestamps, so synced lyrics keep their timing.

## Install from Jellyfin

The server has to be able to download the manifest, so the repository is public.

1. Open **Dashboard → Plugins → Repositories**.
2. Choose **Add Repository**.
3. Name it `Censored Lyrics`.
4. Set the repository URL to:

   ```text
   https://raw.githubusercontent.com/TidBits16/jellyfin-plugin-censored-lyrics/main/manifest.json
   ```

5. Open **Catalog**, find **Censored Lyrics** under **Music**, and install it.
6. Restart Jellyfin.

Jellyfin 12 installs version 1.0.1.0. Jellyfin 10.11 installs version 1.0.0.0. Older servers are not supported.

## Use it

1. If the official **LrcLib Lyrics** plugin is also installed, disable it or put **Censored Lyrics** first in the music library's lyric provider order. Jellyfin saves the first result it gets, and the other plugin does not censor.
2. Download lyrics again. Open a song's lyric editor and search, or run Jellyfin's lyric download scheduled task.
3. Lyrics that were already saved are not rewritten. Delete those lyric files, or search again, so the censored copy replaces them.

Settings are under **Dashboard → Plugins → Censored Lyrics**:

- Turn censoring on or off.
- Include or skip mild words (`damn`, `hell`, `crap`, `bloody`).
- Add your own words. `yeet` becomes `y***`.
- List words that should never be censored, such as a name.
- Point the plugin at `https://lrclib.net` or at your own LRCLIB server.

Matching is whole-word. `class`, `cocktail`, `assassin`, and `hello` are left alone. Common endings (`fucking`, `shit!`) and stretched spellings (`fuuuck`, `sh1t`) are masked.

## Build

```bash
dotnet test Jellyfin.Plugin.CensoredLyrics.slnx -c Release
./build/package.sh
```

`./build/package.sh` runs the tests, writes `dist/censored-lyrics_1.0.1.0.zip` (Jellyfin 12) and `dist/censored-lyrics_1.0.0.0.zip` (Jellyfin 10.11), and refreshes `manifest.json` with the MD5 checksum Jellyfin checks on install.

## License

[GPL-2.0](LICENSE), the same license as Jellyfin.
