# ReelClean

Your media, properly named.

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

Create a v3 API key at themoviedb.org and paste it into the box at the top of the window, then click Save. It is stored in `%AppData%\ReelClean\settings.json` on Windows (the OS application-data folder on macOS and Linux). You can also set `Tmdb:ApiKey` in `appsettings.json` or the `Tmdb__ApiKey` environment variable before the first save.

The client calls `https://api.themoviedb.org/3/` with the `api_key` query parameter, same as ReelSoon.

## Run

```bash
dotnet test
dotnet run --project ReelClean
```

Scanning does not rename anything. High-confidence matches can be renamed after you confirm the preview. Medium, low, and unmatched files have to be assigned or ignored first. Renames stay inside the file's current folder. A history file under the ReelClean application-data folder supports undo when the new files are still in place and the original paths are free.

## Not in this version

Movies, subtitles, anime numbering, multi-series scans, and installers are out of scope until this workflow is settled.
