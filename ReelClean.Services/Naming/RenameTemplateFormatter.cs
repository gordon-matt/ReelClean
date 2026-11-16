using System.Globalization;
using System.Text.RegularExpressions;

namespace ReelClean.Services.Naming;

public sealed partial class RenameTemplateFormatter(IFilenameSanitizer sanitizer) : IRenameTemplateFormatter
{
    public string Format(
        string template,
        TvSeries series,
        IReadOnlyList<TvEpisode> episodes,
        string originalFileName,
        bool includeEpisodeTitles)
    {
        var ordered = episodes.OrderBy(episode => episode.SeasonNumber).ThenBy(episode => episode.EpisodeNumber).ToList();
        if (ordered.Count == 0)
        {
            return string.Empty;
        }

        var first = ordered[0];
        string pattern = includeEpisodeTitles
            ? template
            : template.Replace(" - {EpisodeName}", string.Empty, StringComparison.Ordinal).Replace("{EpisodeName}", string.Empty, StringComparison.Ordinal);

        string formatted = TokenPattern().Replace(pattern, match =>
        {
            string name = match.Groups["name"].Value;
            string format = match.Groups["format"].Success ? match.Groups["format"].Value : "0";
            return name.ToLowerInvariant() switch
            {
                "seriesname" => sanitizer.SanitizeFileName(series.Name),
                "year" => series.FirstAirYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "season" => first.SeasonNumber.ToString(format, CultureInfo.InvariantCulture),
                "episode" => EpisodeToken(ordered, format),
                "episodename" => sanitizer.SanitizeFileName(string.Join(" + ", ordered.Select(episode => episode.Name.Trim()).Where(episode => episode.Length > 0))),
                "airdate" => first.AirDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                "originalfilename" => sanitizer.SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName)),
                "extension" => Path.GetExtension(originalFileName),
                _ => string.Empty,
            };
        });

        formatted = formatted.Replace(" ()", string.Empty, StringComparison.Ordinal).Replace("()", string.Empty, StringComparison.Ordinal);
        var segments = formatted.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim().TrimEnd('-').Trim())
            .Where(segment => segment.Length > 0);
        return string.Join('/', segments);
    }

    private static string EpisodeToken(IReadOnlyList<TvEpisode> episodes, string format)
    {
        if (episodes.Count == 1)
        {
            return episodes[0].EpisodeNumber.ToString(format, CultureInfo.InvariantCulture);
        }

        bool contiguous = episodes[^1].EpisodeNumber - episodes[0].EpisodeNumber == episodes.Count - 1
            && episodes.Select(episode => episode.SeasonNumber).Distinct().Count() == 1;
        if (contiguous)
        {
            return episodes[0].EpisodeNumber.ToString(format, CultureInfo.InvariantCulture)
                + "-E"
                + episodes[^1].EpisodeNumber.ToString(format, CultureInfo.InvariantCulture);
        }

        return string.Join(
            "+E",
            episodes.Select(episode => episode.EpisodeNumber.ToString(format, CultureInfo.InvariantCulture)));
    }

    [GeneratedRegex(@"\{(?<name>SeriesName|Year|Season|Episode|EpisodeName|AirDate|OriginalFilename|Extension)(:(?<format>[^}]+))?\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
