using ReelClean.Services.Parsing;

namespace ReelClean.Tests.Parsing;

public class FilenameParserTests
{
    private readonly FilenameParser _parser = new();

    [Theory]
    [InlineData("Show.S01E01.mkv", 1, new[] { 1 })]
    [InlineData("Show.S01E01E02.mkv", 1, new[] { 1, 2 })]
    [InlineData("Show.S01E01-E03.mkv", 1, new[] { 1, 2, 3 })]
    [InlineData("Show.s01e02.mkv", 1, new[] { 2 })]
    [InlineData("Show.S1E2.mkv", 1, new[] { 2 })]
    [InlineData("Show.S00E01.mkv", 0, new[] { 1 })]
    public void Parse_StandardNumbering_ReadsSeasonAndEpisodes(string fileName, int season, int[] episodes)
    {
        var parsed = _parser.Parse(fileName);

        Assert.Equal(ParsePatterns.Standard, parsed.Pattern);
        Assert.Equal(season, parsed.SeasonNumber);
        Assert.Equal(episodes, parsed.EpisodeNumbers);
    }

    [Fact]
    public void Parse_XNotation_ReadsSeasonAndEpisode()
    {
        var parsed = _parser.Parse("Show.1x02.mkv");

        Assert.Equal(ParsePatterns.Alternate, parsed.Pattern);
        Assert.Equal(1, parsed.SeasonNumber);
        Assert.Equal([2], parsed.EpisodeNumbers);
    }

    [Fact]
    public void Parse_XNotationRange_ExpandsTheRange()
    {
        var parsed = _parser.Parse("Show.1x02-1x04.mkv");

        Assert.Equal([2, 3, 4], parsed.EpisodeNumbers);
        Assert.Equal(1, parsed.SeasonNumber);
    }

    [Fact]
    public void Parse_CompactNumber_IsNotConfusedWithAResolution()
    {
        var compact = _parser.Parse("Show.102.mkv");
        var resolution = _parser.Parse("Show.1080p.mkv");
        var shortResolution = _parser.Parse("Show.720p.mkv");

        Assert.Equal(ParsePatterns.Compact, compact.Pattern);
        Assert.Equal(1, compact.SeasonNumber);
        Assert.Equal([2], compact.EpisodeNumbers);
        Assert.Equal(ParsePatterns.None, resolution.Pattern);
        Assert.Equal(ParsePatterns.None, shortResolution.Pattern);
    }

    [Fact]
    public void Parse_YearAndTitleWithoutANumber_StayUnparsed()
    {
        Assert.Equal(ParsePatterns.None, _parser.Parse("Show.2024.mkv").Pattern);
        Assert.Equal(ParsePatterns.None, _parser.Parse("Show.Pilot.mkv").Pattern);
        Assert.Empty(_parser.Parse("Show.5.1.mkv").EpisodeNumbers);
    }

    [Fact]
    public void Parse_EpisodeNumberOnly_DoesNotInventASeason()
    {
        var parsed = _parser.Parse("Show.02.mkv");

        Assert.Equal(ParsePatterns.EpisodeOnly, parsed.Pattern);
        Assert.Null(parsed.SeasonNumber);
        Assert.Equal([2], parsed.EpisodeNumbers);
    }

    [Fact]
    public void Parse_TitleAfterTheMarker_DropsReleaseTags()
    {
        var parsed = _parser.Parse("The.Office.S03E04.Grief.Counseling.1080p.WEB-DL.mkv");

        Assert.Equal("Grief Counseling", parsed.DetectedEpisodeTitle);
        Assert.Equal(3, parsed.SeasonNumber);
        Assert.Equal([4], parsed.EpisodeNumbers);
    }

    [Fact]
    public void Parse_ResolutionIsNotAnEpisodeRange()
    {
        var parsed = _parser.Parse("Show.S01E02-1080p.mkv");

        Assert.Equal([2], parsed.EpisodeNumbers);
    }
}
