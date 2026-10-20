# Censored LRCLIB

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
3. Name it `Censored LRCLIB`.
4. Set the repository URL to:

   ```text
   https://raw.githubusercontent.com/TidBits16/jellyfin-plugin-censored-lyrics/main/manifest.json
   ```

5. Open **Catalog**, find **Censored LRCLIB** under **Music**, and install it.
6. Restart Jellyfin.

Jellyfin 12 installs the newest 12 build. Jellyfin 10.11 installs the newest 10.11 build.

## Use it

Open **Dashboard → Plugins → Censored LRCLIB**. The default mask keeps the first letter and covers the rest with asterisks. **Apply** saves and fetches lyrics for songs that have not been fetched yet. **Save** only stores the settings. The same fetch also runs every day at 2:00 AM from **Dashboard → Scheduled Tasks**, where the time can be changed. The word list and **Force Refresh all Lyrics** are under **Advanced**. Force refresh fetches every song again and overwrites its lyrics.

## Build

```bash
dotnet test Jellyfin.Plugin.CensoredLyrics.slnx -c Release
./build/package.sh
```

`./build/package.sh` runs the tests, writes the plugin zips, and refreshes `manifest.json` with the MD5 checksum Jellyfin checks on install.

## License

[GPL-2.0](LICENSE), the same license as Jellyfin.
