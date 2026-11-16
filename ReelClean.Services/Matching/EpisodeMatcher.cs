using ReelClean.Services.Parsing;

namespace ReelClean.Services.Matching;

public sealed class EpisodeMatcher : IEpisodeMatcher
{
    public EpisodeMatch Match(VideoFile file, ParsedFilename parsed, IReadOnlyList<TvEpisode> episodes)
    {
        if (parsed.EpisodeNumbers.Count == 0)
        {
            return Unmatched(file, parsed, parsed.Diagnostic);
        }

        if (parsed.SeasonNumber is not int season)
        {
            return MatchWithoutSeason(file, parsed, episodes);
        }

        var found = new List<TvEpisode>();
        var missing = new List<int>();
        foreach (int number in parsed.EpisodeNumbers.Distinct().Order())
        {
            var episode = episodes.FirstOrDefault(item => item.SeasonNumber == season && item.EpisodeNumber == number);
            if (episode is null)
            {
                missing.Add(number);
            }
            else
            {
                found.Add(episode);
            }
        }

        if (missing.Count > 0 || found.Count == 0)
        {
            string missingText = string.Join(", ", missing.Select(number => number.ToString("00")));
            string message = missing.Count == 0
                ? $"Season {season} has no matching episodes."
                : $"Season {season:00} has no episode {missingText}.";
            return Unmatched(file, parsed, message);
        }

        var confidence = parsed.Pattern switch
        {
            ParsePatterns.Standard or ParsePatterns.Alternate => MatchConfidence.High,
            ParsePatterns.Compact => MatchConfidence.Medium,
            _ => MatchConfidence.Low,
        };

        string explanation = confidence == MatchConfidence.High
            ? $"Matched {Describe(found)}."
            : $"Matched {Describe(found)}. {parsed.Diagnostic}";

        return new EpisodeMatch(file, parsed, found, confidence, MatchStatus.Suggested, explanation);
    }

    public EpisodeMatch Confirm(VideoFile file, ParsedFilename parsed, IReadOnlyList<TvEpisode> chosen)
    {
        if (chosen.Count == 0)
        {
            return Unmatched(file, parsed, "No episode was selected.");
        }

        if (chosen.Select(episode => episode.SeasonNumber).Distinct().Count() > 1)
        {
            return Unmatched(file, parsed, "Choose episodes from one season.");
        }

        var ordered = chosen.OrderBy(episode => episode.SeasonNumber).ThenBy(episode => episode.EpisodeNumber).ToList();
        return new EpisodeMatch(file, parsed, ordered, MatchConfidence.High, MatchStatus.Confirmed, "Chosen manually.");
    }

    public EpisodeMatch Ignore(VideoFile file, ParsedFilename parsed) =>
        new(file, parsed, [], MatchConfidence.None, MatchStatus.Ignored, "Ignored.");

    public EpisodeMatch Clear(VideoFile file, ParsedFilename parsed) =>
        Unmatched(file, parsed, "Match cleared.");

    private EpisodeMatch MatchWithoutSeason(VideoFile file, ParsedFilename parsed, IReadOnlyList<TvEpisode> episodes)
    {
        int? season = null;
        var found = new List<TvEpisode>();
        foreach (int number in parsed.EpisodeNumbers.Distinct().Order())
        {
            var hits = episodes.Where(episode => episode.EpisodeNumber == number).ToList();
            if (hits.Count != 1)
            {
                string message = hits.Count == 0
                    ? $"Episode {number:00} is not in the loaded seasons."
                    : $"Episode {number:00} appears in more than one season.";
                return Unmatched(file, parsed, message);
            }

            if (season is null)
            {
                season = hits[0].SeasonNumber;
            }
            else if (season != hits[0].SeasonNumber)
            {
                return Unmatched(file, parsed, "Those episode numbers belong to different seasons.");
            }

            found.Add(hits[0]);
        }

        return new EpisodeMatch(
            file,
            parsed,
            found,
            MatchConfidence.Low,
            MatchStatus.Suggested,
            $"Matched {Describe(found)} without a season in the filename. Review this match.");
    }

    private static EpisodeMatch Unmatched(VideoFile file, ParsedFilename parsed, string explanation) =>
        new(file, parsed, [], MatchConfidence.None, MatchStatus.Unmatched, explanation);

    private static string Describe(IReadOnlyList<TvEpisode> episodes)
    {
        var ordered = episodes.OrderBy(episode => episode.EpisodeNumber).ToList();
        int season = ordered[0].SeasonNumber;
        string numbers = ordered.Count == 1
            ? ordered[0].EpisodeNumber.ToString("00")
            : ordered[^1].EpisodeNumber - ordered[0].EpisodeNumber == ordered.Count - 1
                ? $"{ordered[0].EpisodeNumber:00}-E{ordered[^1].EpisodeNumber:00}"
                : string.Join('+', ordered.Select(episode => episode.EpisodeNumber.ToString("00")));
        string titles = string.Join(" + ", ordered.Select(episode => episode.Name));
        return $"S{season:00}E{numbers} ({titles})";
    }
}
