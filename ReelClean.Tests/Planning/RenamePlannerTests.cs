using ReelClean.Services.Matching;
using ReelClean.Services.Naming;
using ReelClean.Services.Parsing;
using ReelClean.Services.Planning;

namespace ReelClean.Tests.Planning;

public class RenamePlannerTests
{
    private readonly FilenameParser _parser = new();
    private readonly EpisodeMatcher _matcher = new();
    private readonly RenamePlanner _planner = new(new RenameTemplateFormatter(new FilenameSanitizer()), new FilenameSanitizer());
    private static readonly TvSeries Series = new(1, "The Office (US)", 2005, null);

    [Fact]
    public void CreatePlan_HighConfidenceMatch_UsesThePlexTemplate()
    {
        using var folder = new TempFolder();
        var match = Match(folder, "The.Office.S03E04.Grief.Counseling.mkv", Catalog());

        var plan = _planner.CreatePlan(folder.Path, Series, [match], RenameTemplates.PatternFor(RenameTemplates.PlexName), includeEpisodeTitles: true);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(PlanStatus.Ready, operation.Status);
        Assert.Equal("The Office (US) - S03E04 - Grief Counseling.mkv", operation.ProposedFileName);
        Assert.True(plan.CanExecute);
    }

    [Fact]
    public void CreatePlan_MultiEpisode_JoinsTitles()
    {
        using var folder = new TempFolder();
        var match = Match(folder, "Show.S01E01E02.mkv",
        [
            Episode(1, 1, "Pilot"),
            Episode(1, 2, "The First Day"),
        ]);

        var plan = _planner.CreatePlan(folder.Path, Series, [match], RenameTemplates.PatternFor(RenameTemplates.PlexName), true);
        var operation = Assert.Single(plan.Operations);

        Assert.Equal("The Office (US) - S01E01-E02 - Pilot + The First Day.mkv", operation.ProposedFileName);
    }

    [Fact]
    public void CreatePlan_ColonInTheTitle_IsRemoved()
    {
        using var folder = new TempFolder();
        var file = folder.Write("Show.S01E01.mkv", "video");
        var video = Video(file);
        var match = _matcher.Match(video, _parser.Parse(video.FileName), [Episode(1, 1, "Grief: Counseling")]);

        var plan = _planner.CreatePlan(folder.Path, Series, [match], RenameTemplates.PatternFor(RenameTemplates.PlexName), true);

        Assert.Equal("The Office (US) - S01E01 - Grief Counseling.mkv", Assert.Single(plan.Operations).ProposedFileName);
    }

    [Fact]
    public void CreatePlan_MediumConfidence_BlocksExecutionUntilConfirmed()
    {
        using var folder = new TempFolder();
        var match = Match(folder, "Show.102.mkv", [Episode(1, 2, "The First Day")]);

        var planned = _planner.CreatePlan(folder.Path, Series, [match], RenameTemplates.PatternFor(RenameTemplates.MinimalName), true);
        Assert.Equal(PlanStatus.NeedsReview, Assert.Single(planned.Operations).Status);
        Assert.False(planned.CanExecute);

        var confirmed = _matcher.Confirm(match.File, match.Parsed, match.Episodes);
        var accepted = _planner.CreatePlan(folder.Path, Series, [confirmed], RenameTemplates.PatternFor(RenameTemplates.MinimalName), true);
        Assert.True(accepted.CanExecute);
    }

    [Fact]
    public void CreatePlan_DuplicateDestinationAndExistingFile_AreInvalid()
    {
        using var folder = new TempFolder();
        var first = Match(folder, "A.S01E01.mkv", [Episode(1, 1, "Pilot")]);
        var second = Match(folder, "B.S01E01.mkv", [Episode(1, 1, "Pilot")]);
        var duplicates = _planner.CreatePlan(folder.Path, Series, [first, second], RenameTemplates.PatternFor(RenameTemplates.MinimalName), false);
        Assert.All(duplicates.Operations, operation => Assert.Equal(PlanStatus.Invalid, operation.Status));

        var occupied = Match(folder, "C.S01E02.mkv", [Episode(1, 2, "Next")]);
        string template = RenameTemplates.PatternFor(RenameTemplates.MinimalName);
        string destination = Assert.Single(_planner.CreatePlan(folder.Path, Series, [occupied], template, false).Operations).DestinationPath;
        File.WriteAllText(destination, "already there");

        var collision = _planner.CreatePlan(folder.Path, Series, [occupied], template, false);
        Assert.Equal(PlanStatus.Invalid, Assert.Single(collision.Operations).Status);
        Assert.Contains("already exists", Assert.Single(collision.Operations).Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePlan_IgnoredFile_DoesNotBlockTheOthers()
    {
        using var folder = new TempFolder();
        var ready = Match(folder, "Show.S01E01.mkv", [Episode(1, 1, "Pilot")]);
        var ignoredFile = Video(folder.Write("notes.mkv", "skip"));
        var ignored = _matcher.Ignore(ignoredFile, _parser.Parse(ignoredFile.FileName));

        var plan = _planner.CreatePlan(folder.Path, Series, [ready, ignored], RenameTemplates.PatternFor(RenameTemplates.MinimalName), false);

        Assert.True(plan.CanExecute);
        Assert.Equal(1, plan.IgnoredCount);
    }

    [Fact]
    public void CreatePlan_JellyfinTemplate_KeepsTheSeasonFolderUnderTheSourceFolder()
    {
        using var folder = new TempFolder();
        var match = Match(folder, "Show.S03E04.mkv", [Episode(3, 4, "Grief Counseling")]);

        var plan = _planner.CreatePlan(folder.Path, Series, [match], RenameTemplates.PatternFor(RenameTemplates.JellyfinName), true);
        string destination = Assert.Single(plan.Operations).DestinationPath;

        Assert.Contains(System.IO.Path.Combine("The Office (US) (2005)", "Season 03"), destination, StringComparison.Ordinal);
        Assert.StartsWith(folder.Path, destination, StringComparison.OrdinalIgnoreCase);
    }

    private EpisodeMatch Match(TempFolder folder, string fileName, IReadOnlyList<TvEpisode> catalog)
    {
        var video = Video(folder.Write(fileName, "video"));
        return _matcher.Match(video, _parser.Parse(fileName), catalog);
    }

    private static VideoFile Video(string path) =>
        new(path, System.IO.Path.GetFileName(path), System.IO.Path.GetExtension(path), new FileInfo(path).Length);

    private static List<TvEpisode> Catalog() =>
    [
        Episode(3, 4, "Grief Counseling"),
    ];

    private static TvEpisode Episode(int season, int number, string name) =>
        new(number, 1, season, number, name, new DateOnly(2005, 3, 24));
}
