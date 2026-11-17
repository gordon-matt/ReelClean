using ReelClean.Services.Parsing;

namespace ReelClean.Tests.Parsing;

public class SeriesNameGuessTests
{
    [Fact]
    public void FromFileName_ReadsTheTitleBeforeTheEpisodeMarker()
    {
        Assert.Equal("The Office", SeriesNameGuess.FromFileName("The.Office.S03E04.Grief.Counseling.mkv"));
        Assert.Equal("The Office US", SeriesNameGuess.FromFileName("The.Office.US.1x02.mkv"));
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
}
