using System.Text.RegularExpressions;

namespace ReelClean.Services.Parsing;

/// <summary>
/// Suggests a TMDB search from a folder name or the text before an episode marker.
/// Returns null when nothing reliable is there.
/// </summary>
public static partial class SeriesNameGuess
{
    private static readonly HashSet<string> GenericFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "tv", "tvs", "television", "videos", "video", "downloads", "download",
        "episodes", "episode", "inbox", "complete", "new", "temp", "tmp",
        "media", "unsorted", "incoming", "shows", "series",
    };

    private static readonly HashSet<string> JunkTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "576p", "720p", "1080p", "2160p", "4k", "x264", "x265", "h264", "h265",
        "hevc", "web", "webrip", "web-dl", "webdl", "bluray", "blu-ray", "hdtv", "dvdrip",
        "proper", "repack", "rerip", "season", "seasons", "specials", "extras", "bonus",
        "complete", "multi",
    };

    public static string? FromFolderAndFiles(string? folderPath, IReadOnlyList<string> filePaths)
    {
        var names = new List<string>();
        foreach (string path in filePaths)
        {
            string? fromFile = FromFileName(Path.GetFileName(path));
            if (fromFile is not null)
            {
                names.Add(fromFile);
            }

            string? fromDirectory = FromDirectory(Path.GetDirectoryName(path), folderPath);
            if (fromDirectory is not null)
            {
                names.Add(fromDirectory);
            }
        }

        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            string? fromRoot = FromDirectory(folderPath, folderPath);
            if (fromRoot is not null)
            {
                names.Add(fromRoot);
            }
        }

        if (names.Count == 0)
        {
            return null;
        }

        var best = names
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key.Length)
            .First();

        bool confident = best.Count() >= 2 || names.Count == 1;
        return confident ? best.First() : null;
    }

    public static string? FromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string stem = Path.GetFileNameWithoutExtension(fileName);
        var marker = EpisodeMarker().Match(stem);
        if (!marker.Success || marker.Index == 0)
        {
            return null;
        }

        return Clean(stem[..marker.Index]);
    }

    private static string? FromDirectory(string? directory, string? stopAt)
    {
        string? current = directory;
        bool passedRoot = false;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string name = Path.GetFileName(current);
            bool atRoot = stopAt is not null && PathComparison.Equal(current, stopAt);
            if (!IsSeasonFolder(name) && !GenericFolders.Contains(name))
            {
                string? cleaned = Clean(name);
                if (cleaned is not null)
                {
                    return cleaned;
                }
            }

            // A scan of "Season 01" or "Downloads" can still take the parent folder name.
            if (passedRoot)
            {
                break;
            }

            passedRoot = atRoot;

            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    private static bool IsSeasonFolder(string name)
    {
        string compact = name.Replace('.', ' ').Replace('_', ' ').Trim();
        return SeasonFolder().IsMatch(compact);
    }

    private static string? Clean(string raw)
    {
        string spaced = raw.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        var tokens = new List<string>();
        foreach (string token in spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string trimmed = token.Trim('(', ')', '[', ']', '{', '}');
            if (trimmed.Length > 0)
            {
                tokens.Add(trimmed);
            }
        }

        while (tokens.Count > 0 && IsJunk(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        if (tokens.Count == 0)
        {
            return null;
        }

        return string.Join(' ', tokens);
    }

    private static bool IsJunk(string token) =>
        JunkTokens.Contains(token)
        || YearOrResolution().IsMatch(token)
        || EpisodeToken().IsMatch(token)
        || token.All(char.IsDigit);

    [GeneratedRegex(@"(?i)(?<![A-Za-z0-9])(?:S\d{1,2}[\s._-]*E\d{1,3}|\d{1,2}x\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeMarker();

    [GeneratedRegex(@"^(season|specials|extras|bonus)(\s+\d{1,2})?$|^s\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonFolder();

    [GeneratedRegex(@"^(19|20)\d{2}$|^\d{3,4}p$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YearOrResolution();

    [GeneratedRegex(@"^[se]\d{1,3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeToken();
}
