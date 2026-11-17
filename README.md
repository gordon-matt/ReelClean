<img src="https://github.com/gordon-matt/ReelClean/raw/master/_Misc/Logo_Small.png" alt="Logo" width="350" />

Your media, properly named.

## Screenshots

<img src="https://github.com/gordon-matt/ReelClean/raw/master/_Misc/Screenshots/01_Folder.JPG" alt="Step 1 - Select Folder" />

<img src="https://github.com/gordon-matt/ReelClean/raw/master/_Misc/Screenshots/02_Series.JPG" alt="Step 2 - Select Series" />

<img src="https://github.com/gordon-matt/ReelClean/raw/master/_Misc/Screenshots/03_Match.JPG" alt="Step 3 - Match" />

<img src="https://github.com/gordon-matt/ReelClean/raw/master/_Misc/Screenshots/04_Rename.JPG" alt="Step 4 - Rename" />

ReelClean is a cross-platform desktop app for one TV folder at a time. It reads episode numbers from filenames, matches them to [TMDB](https://www.themoviedb.org/), shows a rename preview, and only then renames files.

This product uses the TMDB API but is not endorsed or certified by TMDB.

## Layout

The projects follow the same split as ReelSoon, Kinnect, and Snippetz:

- `ReelClean.Core` — enums and constants
- `ReelClean.Models` — records for files, episodes, matches, and rename plans
- `ReelClean.Services` — parsing, matching, planning, TMDB, and file operations
- `ReelClean` — Avalonia UI
- `ReelClean.Tests` — xUnit

TMDB JSON stays inside `TmdbClient`, the same way ReelSoon hides its TMDB payloads. Services that can fail return `Ardalis.Result`. A filename that does not contain an episode number is a normal parse result, not an error.

## TMDB key

Create a v3 API key at themoviedb.org, open Settings from the top bar, paste the key in, and click Save.

Save writes `%AppData%\ReelClean\settings.json` on Windows, and the equivalent application-data folder on macOS and Linux. That folder is outside this repository, so the key is not committed. `appsettings.json` is for logging and other app configuration, and it does not contain a TMDB key. A gitignored `appsettings.Local.json`, or the `Tmdb__ApiKey` environment variable, can supply the key before the first save. After you click Save, the app-data file is the one the app uses.

The rename template, including a custom pattern, is saved in that same user-profile file when you change it.

The client calls `https://api.themoviedb.org/3/` with the `api_key` query parameter, same as ReelSoon.

## Run

```bash
dotnet test
dotnet run --project ReelClean
```

Scanning does not rename anything. High-confidence matches can be renamed after you confirm the preview. Medium, low, and unmatched files have to be assigned or ignored first. Renames stay inside the file's current folder. A history file under the ReelClean application-data folder supports undo when the new files are still in place and the original paths are free.

## Not in this version

Movies, subtitles, anime numbering, multi-series scans, and installers are out of scope until this workflow is settled.

## Support

If you find this project useful, consider supporting its development.

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/raw/gh-pages/assets/images/BuyMeACoffee_Logo.png"
         alt="Buy Me a Coffee"
         height="40">
  </a>&nbsp;&nbsp;&nbsp;
  <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=gordon_matt%40live%2ecom&lc=AU&currency_code=AUD&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHosted">
    <img src="https://github.com/gordon-matt/MyResume/raw/gh-pages/assets/images/PayPal_Logo.png"
         alt="Donate with PayPal"
         height="40">
  </a>
</p>

<p align="center">
  Prefer using your phone? Scan the QR code:
</p>

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/raw/gh-pages/assets/images/BuyMeACoffee_QR.png"
         alt="Buy Me a Coffee QR Code"
         width="180">
  </a>
</p>

<img src="https://komarev.com/ghpvc/?username=gordon-matt&label=GitHub%20Hits%20Since%202025-06-01%3A%20&color=ff0000&style=flat" alt="gordon-matt" />