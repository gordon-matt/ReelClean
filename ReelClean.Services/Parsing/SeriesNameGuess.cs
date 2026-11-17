using System.Text.RegularExpressions;

namespace ReelClean.Services.Parsing;

/// <summary>
/// Suggests a TMDB search from a folder name or the text before an episode marker.
/// Release tags, years, and season labels are left out so the query is just the series name.
/// </summary>
public static partial class SeriesNameGuess
{
    private static readonly HashSet<string> GenericFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "tv", "tvs", "television", "videos", "video", "downloads", "download",
        "episodes", "episode", "inbox", "complete", "new", "temp", "tmp",
        "media", "unsorted", "incoming", "shows", "series",
    };

    private static readonly HashSet<string> ReleaseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "576p", "720p", "1080p", "2160p", "4k", "8k",
        "x264", "x265", "h264", "h265", "hevc", "xvid", "divx", "av1",
        "web", "webrip", "webdl", "bluray", "bdrip", "brrip", "hdtv", "hdrip",
        "dvd", "dvdrip", "pdtv", "dsr", "remux", "hdr", "sdr", "10bit", "8bit",
        "aac", "ac3", "eac3", "dts", "truehd", "atmos", "flac", "dd", "ddp",
        "proper", "repack", "rerip", "internal", "limited", "extended", "unrated", "remastered",
        "season", "seasons", "specials", "extras", "bonus", "complete", "multi", "custom",
        "dl", "pack", "batch", "sample", "nfo", "readnfo", "sub", "subs", "dub", "dubbed", "subbed",
        "amzn", "nf", "dsnp", "hmax", "hulu", "atvp", "itunes",
    };

    private static readonly HashSet<string> CountryTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "us", "uk", "au", "nz", "ca",
    };

    public static string? FromFolderAndFiles(string? folderPath, IReadOnlyList<string> filePaths)
    {
        var fromFiles = filePaths
            .Select(path => FromFileName(Path.GetFileName(path)))
            .OfType<string>()
            .ToList();
        string? fileTitle = CommonTitle(fromFiles);

        string? folderTitle = null;
        foreach (string path in filePaths)
        {
            folderTitle ??= FromDirectory(Path.GetDirectoryName(path), folderPath);
        }

        folderTitle ??= FromDirectory(folderPath, folderPath);
        return Choose(folderTitle, fileTitle);
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

    private static string? Choose(string? folderTitle, string? fileTitle)
    {
        if (folderTitle is null)
        {
            return fileTitle;
        }

        if (fileTitle is null)
        {
            return folderTitle;
        }

        if (StartsWithWords(fileTitle, folderTitle))
        {
            return folderTitle;
        }

        if (StartsWithWords(folderTitle, fileTitle))
        {
            return fileTitle;
        }

        return folderTitle;
    }

    private static string? CommonTitle(IReadOnlyList<string> titles)
    {
        if (titles.Count == 0)
        {
            return null;
        }

        var words = titles.Select(title => title.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        int shared = 0;
        int limit = words.Min(title => title.Length);
        while (shared < limit && words.All(title => title[shared].Equals(words[0][shared], StringComparison.OrdinalIgnoreCase)))
        {
            shared++;
        }

        return shared == 0 ? null : string.Join(' ', words[0].Take(shared));
    }

    private static bool StartsWithWords(string text, string prefix)
    {
        string[] textWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] prefixWords = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (prefixWords.Length > textWords.Length)
        {
            return false;
        }

        for (int i = 0; i < prefixWords.Length; i++)
        {
            if (!textWords[i].Equals(prefixWords[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
            string trimmed = token.Trim('(', ')', '[', ']', '{', '}', '\'', '"');
            if (trimmed.Length > 0)
            {
                tokens.Add(trimmed);
            }
        }

        int release = tokens.FindIndex(IsReleaseToken);
        if (release > 0)
        {
            tokens.RemoveRange(release, tokens.Count - release);
        }

        while (tokens.Count > 0 && IsReleaseToken(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        if (tokens.Count > 1 && CountryTags.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return tokens.Count == 0 ? null : string.Join(' ', tokens);
    }

    private static bool IsReleaseToken(string token) =>
        ReleaseTokens.Contains(token)
        || YearOrResolution().IsMatch(token)
        || EpisodeToken().IsMatch(token)
        || AudioToken().IsMatch(token);

    [GeneratedRegex(@"(?i)(?<![A-Za-z0-9])(?:S\d{1,2}[\s._-]*E\d{1,3}|\d{1,2}x\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeMarker();

    [GeneratedRegex(@"^(season|specials|extras|bonus)(\s+\d{1,2})?$|^s\d{1,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonFolder();

    [GeneratedRegex(@"^(19|20)\d{2}$|^\d{3,4}p$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YearOrResolution();

    [GeneratedRegex(@"^[se]\d{1,3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeToken();

    [GeneratedRegex(@"^(dd|ddp|aac)?\d\.\d$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioToken();
}
