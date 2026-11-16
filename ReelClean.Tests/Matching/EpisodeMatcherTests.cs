using ReelClean.Services.Matching;
using ReelClean.Services.Parsing;

namespace ReelClean.Tests.Matching;

public class EpisodeMatcherTests
{
    private readonly FilenameParser _parser = new();
    private readonly EpisodeMatcher _matcher = new();

    [Fact]
    public void Match_ExplicitEpisode_IsHighConfidence()
    {
        var result = Match("Show.S01E02.mkv");

        Assert.Equal(MatchStatus.Suggested, result.Status);
        Assert.Equal(MatchConfidence.High, result.Confidence);
        Assert.Equal("The First Day", result.Episodes[0].Name);
    }

    [Fact]
    public void Match_MissingEpisode_IsUnmatched()
    {
        var result = Match("Show.S01E99.mkv");

        Assert.Equal(MatchStatus.Unmatched, result.Status);
        Assert.Contains("99", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_Specials_UsesSeasonZero()
    {
        var result = Match("Show.S00E01.mkv");

        Assert.Equal(MatchConfidence.High, result.Confidence);
        Assert.Equal("Special", result.Episodes[0].Name);
    }

    [Fact]
    public void Match_MultiEpisodeFile_RequiresEveryEpisode()
    {
        var found = Match("Show.S01E01E02.mkv");
        var missing = Match("Show.S01E01-E03.mkv");

        Assert.Equal(2, found.Episodes.Count);
        Assert.Equal(MatchStatus.Unmatched, missing.Status);
    }

    [Fact]
    public void Match_CompactNumber_NeedsReview()
    {
        var result = Match("Show.102.mkv");

        Assert.Equal(MatchConfidence.Medium, result.Confidence);
        Assert.Equal(MatchStatus.Suggested, result.Status);
    }

    [Fact]
    public void Match_EpisodeNumberSharedBySeasons_IsNotGuessed()
    {
        var result = Match("Show.01.mkv");

        Assert.Equal(MatchStatus.Unmatched, result.Status);
        Assert.Contains("more than one season", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirm_ManualChoice_IsReadyForThePlan()
    {
        var file = new VideoFile("C:\\Show\\Pilot.mkv", "Pilot.mkv", ".mkv", 1);
        var parsed = _parser.Parse(file.FileName);

        var result = _matcher.Confirm(file, parsed, [Episode(1, 1, "Pilot")]);

        Assert.Equal(MatchStatus.Confirmed, result.Status);
        Assert.Equal(MatchConfidence.High, result.Confidence);
    }

    [Fact]
    public void Ignore_ClearsTheEpisode()
    {
        var file = new VideoFile("C:\\Show\\A.mkv", "A.mkv", ".mkv", 1);
        var result = _matcher.Ignore(file, _parser.Parse(file.FileName));

        Assert.Equal(MatchStatus.Ignored, result.Status);
        Assert.Empty(result.Episodes);
    }

    private EpisodeMatch Match(string fileName)
    {
        var file = new VideoFile("C:\\Show\\" + fileName, fileName, ".mkv", 1);
        return _matcher.Match(file, _parser.Parse(fileName), Catalog());
    }

    private static List<TvEpisode> Catalog() =>
    [
        Episode(0, 1, "Special"),
        Episode(1, 1, "Pilot"),
        Episode(1, 2, "The First Day"),
        Episode(2, 1, "Later"),
    ];

    private static TvEpisode Episode(int season, int number, string name) =>
        new(number + (season * 100), 7, season, number, name, new DateOnly(2005, 3, 24));
}
