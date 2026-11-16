using System.Globalization;

namespace ReelClean.Services.Parsing;

public sealed class FilenameParser : IFilenameParser
{
    public ParsedFilename Parse(string filename)
    {
        string name = Path.GetFileName(filename);
        string stem = Path.GetFileNameWithoutExtension(name);

        if (StandardSeasonEpisodePattern.TryParse(name, stem, out var standard))
        {
            return standard;
        }

        if (AlternateSeasonEpisodePattern.TryParse(name, stem, out var alternate))
        {
            return alternate;
        }

        if (CompactEpisodePattern.TryParse(name, stem, out var compact))
        {
            return compact;
        }

        if (EpisodeNumberOnlyPattern.TryParse(name, stem, out var episodeOnly))
        {
            return episodeOnly;
        }

        return new ParsedFilename(
            name,
            null,
            [],
            null,
            ParsePatterns.None,
            "No season or episode number was found.");
    }
}

internal static class StandardSeasonEpisodePattern
{
    public static bool TryParse(string fileName, string stem, out ParsedFilename parsed)
    {
        parsed = null!;
        int index = 0;
        while (index < stem.Length)
        {
            if (!IsBoundary(stem, index) || (stem[index] is not ('S' or 's')))
            {
                index++;
                continue;
            }

            if (!TryReadNumber(stem, index + 1, 2, out int season, out int afterSeason))
            {
                index++;
                continue;
            }

            int episodeMarker = SkipSeparators(stem, afterSeason);
            if (episodeMarker >= stem.Length || stem[episodeMarker] is not ('E' or 'e'))
            {
                index++;
                continue;
            }

            if (!TryReadNumber(stem, episodeMarker + 1, 3, out int first, out int afterFirst))
            {
                index++;
                continue;
            }

            var episodes = new List<int> { first };
            int cursor = afterFirst;
            while (TryReadExtraEpisode(stem, ref cursor, out int extra))
            {
                episodes.Add(extra);
            }

            if (TryReadRangeEnd(stem, cursor, out int rangeEnd, out int afterRange) && IsUsefulRange(episodes, rangeEnd))
            {
                Expand(episodes, rangeEnd);
                cursor = afterRange;
            }

            episodes.Sort();
            string label = Describe(season, episodes);
            parsed = new ParsedFilename(
                fileName,
                season,
                episodes,
                TitleCleaner.After(stem, cursor),
                ParsePatterns.Standard,
                $"Matched {label}.");
            return true;
        }

        return false;
    }

    private static bool TryReadExtraEpisode(string text, ref int index, out int episode)
    {
        int marker = SkipSeparators(text, index);
        if (marker >= text.Length || text[marker] is not ('E' or 'e') || !TryReadNumber(text, marker + 1, 3, out episode, out int after))
        {
            episode = 0;
            return false;
        }

        index = after;
        return true;
    }

    internal static bool TryReadRangeEnd(string text, int index, out int episode, out int after)
    {
        episode = 0;
        after = index;
        int cursor = SkipSeparators(text, index);
        if (cursor >= text.Length || text[cursor] != '-')
        {
            return false;
        }

        cursor = SkipSeparators(text, cursor + 1);
        if (cursor < text.Length && text[cursor] is 'E' or 'e')
        {
            cursor++;
        }

        if (!TryReadNumber(text, cursor, 3, out episode, out after))
        {
            return false;
        }

        if (after < text.Length && text[after] is 'p' or 'P')
        {
            return false;
        }

        return true;
    }

    internal static bool IsUsefulRange(IReadOnlyList<int> episodes, int rangeEnd)
    {
        int start = episodes.Min();
        if (rangeEnd <= start || rangeEnd - start > Constants.MaxEpisodeRange)
        {
            return false;
        }

        return !TitleCleaner.IsResolution(rangeEnd);
    }

    internal static void Expand(List<int> episodes, int rangeEnd)
    {
        int start = episodes.Min();
        for (int number = start; number <= rangeEnd; number++)
        {
            if (!episodes.Contains(number))
            {
                episodes.Add(number);
            }
        }
    }

    internal static string Describe(int season, IReadOnlyList<int> episodes)
    {
        var ordered = episodes.Order().ToList();
        string episode = ordered.Count == 1
            ? ordered[0].ToString("00", CultureInfo.InvariantCulture)
            : ordered[^1] - ordered[0] == ordered.Count - 1
                ? $"{ordered[0]:00}-E{ordered[^1]:00}"
                : string.Join('+', ordered.Select(number => number.ToString("00", CultureInfo.InvariantCulture)));
        return $"S{season:00}E{episode}";
    }

    internal static bool IsBoundary(string text, int index) =>
        index == 0 || !char.IsLetterOrDigit(text[index - 1]);

    internal static int SkipSeparators(string text, int index)
    {
        while (index < text.Length && text[index] is ' ' or '.' or '_')
        {
            index++;
        }

        return index;
    }

    internal static bool TryReadNumber(string text, int index, int maxDigits, out int number, out int after)
    {
        int end = index;
        while (end < text.Length && char.IsDigit(text[end]) && end - index < maxDigits)
        {
            end++;
        }

        if (end == index || (end < text.Length && char.IsDigit(text[end])))
        {
            number = 0;
            after = index;
            return false;
        }

        number = int.Parse(text[index..end], CultureInfo.InvariantCulture);
        after = end;
        return true;
    }
}

internal static class AlternateSeasonEpisodePattern
{
    public static bool TryParse(string fileName, string stem, out ParsedFilename parsed)
    {
        parsed = null!;
        for (int index = 0; index < stem.Length; index++)
        {
            if (!StandardSeasonEpisodePattern.IsBoundary(stem, index))
            {
                continue;
            }

            if (!StandardSeasonEpisodePattern.TryReadNumber(stem, index, 2, out int season, out int afterSeason))
            {
                continue;
            }

            if (afterSeason >= stem.Length || stem[afterSeason] is not ('x' or 'X'))
            {
                continue;
            }

            if (!StandardSeasonEpisodePattern.TryReadNumber(stem, afterSeason + 1, 3, out int episode, out int afterEpisode))
            {
                continue;
            }

            if (LooksLikeResolution(stem, afterEpisode))
            {
                continue;
            }

            int cursor = afterEpisode;
            var episodes = new List<int> { episode };
            if (TryReadCrossRange(stem, cursor, out int season2, out int rangeEnd, out int afterRange))
            {
                if (season2 != season)
                {
                    parsed = new ParsedFilename(
                        fileName,
                        null,
                        [],
                        null,
                        ParsePatterns.Alternate,
                        "The filename spans more than one season, so it was not matched.");
                    return true;
                }

                if (StandardSeasonEpisodePattern.IsUsefulRange(episodes, rangeEnd))
                {
                    StandardSeasonEpisodePattern.Expand(episodes, rangeEnd);
                    cursor = afterRange;
                }
            }

            episodes.Sort();
            parsed = new ParsedFilename(
                fileName,
                season,
                episodes,
                TitleCleaner.After(stem, cursor),
                ParsePatterns.Alternate,
                $"Matched {StandardSeasonEpisodePattern.Describe(season, episodes)} from x notation.");
            return true;
        }

        return false;
    }

    private static bool TryReadCrossRange(string text, int index, out int season, out int episode, out int after)
    {
        season = 0;
        episode = 0;
        after = index;
        int cursor = StandardSeasonEpisodePattern.SkipSeparators(text, index);
        if (cursor >= text.Length || text[cursor] != '-')
        {
            return false;
        }

        cursor = StandardSeasonEpisodePattern.SkipSeparators(text, cursor + 1);
        if (!StandardSeasonEpisodePattern.TryReadNumber(text, cursor, 2, out season, out int afterSeason))
        {
            return false;
        }

        if (afterSeason >= text.Length || text[afterSeason] is not ('x' or 'X'))
        {
            return false;
        }

        return StandardSeasonEpisodePattern.TryReadNumber(text, afterSeason + 1, 3, out episode, out after);
    }

    private static bool LooksLikeResolution(string text, int end) =>
        end < text.Length && text[end] is 'p' or 'P';
}

internal static class CompactEpisodePattern
{
    public static bool TryParse(string fileName, string stem, out ParsedFilename parsed)
    {
        parsed = null!;
        var tokens = NumberTokens.FindExact(stem, digits: 3);
        if (tokens.Count != 1)
        {
            return false;
        }

        int value = tokens[0].Value;
        if (TitleCleaner.IsResolution(value) || value % 100 == 0)
        {
            return false;
        }

        int season = value / 100;
        int episode = value % 100;
        parsed = new ParsedFilename(
            fileName,
            season,
            [episode],
            TitleCleaner.After(stem, tokens[0].End),
            ParsePatterns.Compact,
            $"Read {value} as S{season:00}E{episode:00}. Compact numbers are ambiguous.");
        return true;
    }
}

internal static class EpisodeNumberOnlyPattern
{
    public static bool TryParse(string fileName, string stem, out ParsedFilename parsed)
    {
        parsed = null!;
        var explicitMarkers = ExplicitEpisodeMarkers(stem);
        if (explicitMarkers.Count >= 1)
        {
            var episodes = explicitMarkers.Select(token => token.Value).ToList();
            string list = string.Join(", ", episodes.Select(episode => episode.ToString("00", CultureInfo.InvariantCulture)));
            parsed = new ParsedFilename(
                fileName,
                null,
                episodes,
                TitleCleaner.After(stem, explicitMarkers[^1].End),
                ParsePatterns.EpisodeOnly,
                $"Found episode {list} without a season.");
            return true;
        }

        var tokens = NumberTokens.FindExact(stem, 1).Concat(NumberTokens.FindExact(stem, 2)).ToList();
        if (tokens.Count != 1)
        {
            return false;
        }

        parsed = new ParsedFilename(
            fileName,
            null,
            [tokens[0].Value],
            TitleCleaner.After(stem, tokens[0].End),
            ParsePatterns.EpisodeOnly,
            $"Found episode {tokens[0].Value:00} without a season.");
        return true;
    }

    private static List<NumberToken> ExplicitEpisodeMarkers(string stem)
    {
        var found = new List<NumberToken>();
        for (int index = 0; index < stem.Length; index++)
        {
            if (!StandardSeasonEpisodePattern.IsBoundary(stem, index) || stem[index] is not ('E' or 'e'))
            {
                continue;
            }

            if (index > 0 && stem[index - 1] is 'S' or 's')
            {
                continue;
            }

            if (!StandardSeasonEpisodePattern.TryReadNumber(stem, index + 1, 3, out int episode, out int after))
            {
                continue;
            }

            found.Add(new NumberToken(episode, after - index, index, after));
        }

        return found;
    }
}

internal readonly record struct NumberToken(int Value, int Digits, int Start, int End);

internal static class NumberTokens
{
    public static List<NumberToken> FindExact(string stem, int digits)
    {
        var found = new List<NumberToken>();
        int index = 0;
        while (index < stem.Length)
        {
            if (!char.IsDigit(stem[index]) || !StandardSeasonEpisodePattern.IsBoundary(stem, index))
            {
                index++;
                continue;
            }

            int end = index;
            while (end < stem.Length && char.IsDigit(stem[end]))
            {
                end++;
            }

            int length = end - index;
            bool closed = end >= stem.Length || !char.IsLetterOrDigit(stem[end]);
            bool followedByResolutionMark = end < stem.Length && stem[end] is 'p' or 'P';
            if (closed && length == digits && !followedByResolutionMark)
            {
                int value = int.Parse(stem[index..end], CultureInfo.InvariantCulture);
                found.Add(new NumberToken(value, length, index, end));
            }

            index = end;
        }

        return found;
    }
}

internal static class TitleCleaner
{
    private static readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "576p", "720p", "1080p", "2160p", "4k", "8k",
        "x264", "x265", "h264", "h265", "h.264", "h.265", "hevc", "avc", "av1",
        "aac", "ac3", "eac3", "dts", "truehd", "atmos", "flac",
        "webrip", "web-dl", "webdl", "web", "bluray", "blu-ray", "bdrip", "brrip", "hdtv", "dvdrip", "dvd", "hdrip",
        "proper", "repack", "rerip", "hdr", "sdr", "dv", "10bit", "8bit", "dl", "rip",
        "yify", "rarbg", "amzn", "nf", "dsnp", "hmax", "complete", "internal", "remux", "extended", "dubbed",
    };

    public static string? After(string stem, int start)
    {
        if (start >= stem.Length)
        {
            return null;
        }

        string rest = stem[start..];
        rest = System.Text.RegularExpressions.Regex.Replace(
            rest,
            @"\[[^\]]*\]|\([^)]*\)",
            match => KeepBracket(match.Value) ? " " + Unwrap(match.Value) + " " : " ");
        rest = rest.Replace('.', ' ').Replace('_', ' ');

        var kept = new List<string>();
        foreach (string token in rest.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string trimmed = token.Trim('-', ' ');
            if (trimmed.Length == 0 || IsReleaseTag(trimmed))
            {
                continue;
            }

            kept.Add(trimmed);
        }

        return kept.Count == 0 ? null : string.Join(' ', kept);
    }

    public static bool IsResolution(int value) => value is 480 or 576 or 720 or 1080 or 2160;

    private static readonly System.Text.RegularExpressions.Regex AudioTag = new(
        @"^(dd|ddp|aac)?\d\.\d$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool KeepBracket(string token)
    {
        string inner = Unwrap(token);
        var parts = inner.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(IsReleaseTag))
        {
            return false;
        }

        return !(parts.Length == 1
            && parts[0].Length is >= 2 and <= 12
            && parts[0].All(char.IsLetterOrDigit)
            && parts[0].All(char.IsUpper));
    }

    private static string Unwrap(string token) => token.Trim('[', ']', '(', ')');

    private static bool IsReleaseTag(string token)
    {
        if (Tags.Contains(token))
        {
            return true;
        }

        if (token.Length == 4 && token.All(char.IsDigit) && (token.StartsWith("19", StringComparison.Ordinal) || token.StartsWith("20", StringComparison.Ordinal)))
        {
            return true;
        }

        if ((token.EndsWith('p') || token.EndsWith('P'))
            && int.TryParse(token[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out int resolution)
            && IsResolution(resolution))
        {
            return true;
        }

        return AudioTag.IsMatch(token);
    }
}
