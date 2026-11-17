using ReelClean.Services.Parsing;

namespace ReelClean.Tests.Parsing;

public class SeriesNameGuessTests
{
    [Fact]
    public void FromFileName_ReadsTheTitleBeforeTheEpisodeMarker()
    {
        Assert.Equal("The Office", SeriesNameGuess.FromFileName("The.Office.S03E04.Grief.Counseling.mkv"));
        Assert.Equal("The Office", SeriesNameGuess.FromFileName("The.Office.US.1080p.WEB-DL.x264-GROUP.S03E04.mkv"));
        Assert.Equal("The 100", SeriesNameGuess.FromFileName("The.100.S01E01.720p.mkv"));
    }

    [Fact]
    public void FromFileName_WithoutAMarker_ReturnsNothing()
    {
        Assert.Null(SeriesNameGuess.FromFileName("Pilot.mkv"));
        Assert.Null(SeriesNameGuess.FromFileName("Show.1080p.mkv"));
    }

    [Fact]
    public void FromFolderAndFiles_SkipsASeasonFolderAndUsesTheParent()
    {
        string folder = Path.Combine("D:", "TV", "The Office", "Season 01");
        string file = Path.Combine(folder, "S01E01.mkv");

        Assert.Equal("The Office", SeriesNameGuess.FromFolderAndFiles(folder, [file]));
    }

    [Fact]
    public void FromFolderAndFiles_IgnoresAGenericDownloadFolder()
    {
        string folder = Path.Combine("D:", "Downloads");
        string file = Path.Combine(folder, "The.Office.S01E02.mkv");

        Assert.Equal("The Office", SeriesNameGuess.FromFolderAndFiles(folder, [file]));
    }

    [Fact]
    public void FromFolderAndFiles_WhenNothingAgrees_ReturnsNothing()
    {
        string folder = Path.Combine("D:", "Downloads");
        string[] files =
        [
            Path.Combine(folder, "Alpha.S01E01.mkv"),
            Path.Combine(folder, "Beta.S01E01.mkv"),
        ];

        Assert.Null(SeriesNameGuess.FromFolderAndFiles(folder, files));
    }

    [Fact]
    public void FromFolderAndFiles_PrefersTheShortFolderNameOverReleaseText()
    {
        string folder = Path.Combine("D:", "TV", "The Office Season 3 1080p BluRay");
        string file = Path.Combine(folder, "The.Office.US.2020.S03E04.1080p.WEB-DL.mkv");

        Assert.Equal("The Office", SeriesNameGuess.FromFolderAndFiles(folder, [file]));
    }

    [Fact]
    public void FromFolderAndFiles_UsesTheWordsEveryFileShares()
    {
        string folder = Path.Combine("D:", "Downloads");
        string[] files =
        [
            Path.Combine(folder, "The.Office.US.S01E01.1080p.mkv"),
            Path.Combine(folder, "The.Office.UK.S01E01.720p.mkv"),
        ];

        Assert.Equal("The Office", SeriesNameGuess.FromFolderAndFiles(folder, files));
    }
}
