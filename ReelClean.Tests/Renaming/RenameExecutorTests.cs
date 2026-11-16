using Microsoft.Extensions.Logging.Abstractions;
using ReelClean.Services.Matching;
using ReelClean.Services.Naming;
using ReelClean.Services.Parsing;
using ReelClean.Services.Planning;
using ReelClean.Services.Renaming;

namespace ReelClean.Tests.Renaming;

public class RenameExecutorTests
{
    private readonly EpisodeMatcher _matcher = new();
    private readonly FilenameParser _parser = new();
    private readonly RenamePlanner _planner = new(new RenameTemplateFormatter(new FilenameSanitizer()), new FilenameSanitizer());
    private static readonly TvSeries Series = new(1, "Show", null, null);

    [Fact]
    public async Task ExecuteAsync_RenamesAndUndoRestores()
    {
        using var folder = new TempFolder();
        var plan = Plan(folder, ("Show.S01E01.mkv", "Pilot"));
        var executor = Executor(folder);

        var renamed = await executor.ExecuteAsync(plan);

        Assert.True(renamed.IsSuccess);
        Assert.False(renamed.Value.RolledBack);
        string destination = Path.Combine(folder.Path, "Show - S01E01 - Pilot.mkv");
        Assert.Equal("Pilot", await File.ReadAllTextAsync(destination));
        Assert.False(File.Exists(Path.Combine(folder.Path, "Show.S01E01.mkv")));

        var undone = await executor.UndoAsync(renamed.Value.ManifestPath!);

        Assert.True(undone.IsSuccess);
        Assert.Equal("Pilot", await File.ReadAllTextAsync(Path.Combine(folder.Path, "Show.S01E01.mkv")));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ExecuteAsync_SwapsNamesWithoutLosingEitherFile()
    {
        using var folder = new TempFolder();
        string first = folder.Write("A.mkv", "aaa");
        string second = folder.Write("B.mkv", "bbb");
        var matches = new[]
        {
            Confirmed(first, 1, "One"),
            Confirmed(second, 2, "Two"),
        };
        // Point them at each other instead of the template so this is a real swap.
        var operations = new[]
        {
            new RenameOperation(matches[0], second, "B.mkv", PlanStatus.Ready, []),
            new RenameOperation(matches[1], first, "A.mkv", PlanStatus.Ready, []),
        };
        var plan = new RenamePlan(Guid.NewGuid(), folder.Path, "swap", operations);
        var executor = Executor(folder);

        var result = await executor.ExecuteAsync(plan);

        Assert.True(result.IsSuccess);
        Assert.Equal("bbb", await File.ReadAllTextAsync(first));
        Assert.Equal("aaa", await File.ReadAllTextAsync(second));
    }

    [Fact]
    public async Task ExecuteAsync_CaseOnlyRename_KeepsTheRequestedCasing()
    {
        using var folder = new TempFolder();
        folder.Write("Pilot.mkv", "video");
        var match = Confirmed(Path.Combine(folder.Path, "Pilot.mkv"), 1, "Pilot");
        string destination = Path.Combine(folder.Path, "pilot.mkv");
        var plan = new RenamePlan(Guid.NewGuid(), folder.Path, "case",
        [
            new RenameOperation(match, destination, "pilot.mkv", PlanStatus.Ready, []),
        ]);

        var result = await Executor(folder).ExecuteAsync(plan);

        Assert.True(result.IsSuccess);
        string actual = Directory.EnumerateFiles(folder.Path)
            .Select(path => Path.GetFileName(path))
            .Single(name => name is not null && name.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))!;
        Assert.Equal("pilot.mkv", actual);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSource_DoesNotRenameTheOtherFile()
    {
        using var folder = new TempFolder();
        var plan = Plan(folder, ("Show.S01E01.mkv", "Pilot"), ("Show.S01E02.mkv", "Next"));
        File.Delete(Path.Combine(folder.Path, "Show.S01E01.mkv"));

        var result = await Executor(folder).ExecuteAsync(plan);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.True(File.Exists(Path.Combine(folder.Path, "Show.S01E02.mkv")));
    }

    [Fact]
    public async Task ExecuteAsync_FailureDuringTheBatch_RollsBack()
    {
        using var folder = new TempFolder();
        var plan = Plan(folder, ("Show.S01E01.mkv", "Pilot"), ("Show.S01E02.mkv", "Next"));
        var executor = new RenameExecutor(
            _planner,
            new FailWhenDestinationContains("S01E02 - Next"),
            NullLogger<RenameExecutor>.Instance,
            Path.Combine(folder.Path, "history"));

        var result = await executor.ExecuteAsync(plan);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RolledBack);
        Assert.Equal("Pilot", await File.ReadAllTextAsync(Path.Combine(folder.Path, "Show.S01E01.mkv")));
        Assert.Equal("Next", await File.ReadAllTextAsync(Path.Combine(folder.Path, "Show.S01E02.mkv")));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.reelclean.tmp"));
    }

    [Fact]
    public async Task UndoAsync_RefusesToOverwriteAnUnrelatedFile()
    {
        using var folder = new TempFolder();
        var plan = Plan(folder, ("Show.S01E01.mkv", "Pilot"));
        var executor = Executor(folder);
        var renamed = await executor.ExecuteAsync(plan);
        folder.Write("Show.S01E01.mkv", "unrelated");

        var undone = await executor.UndoAsync(renamed.Value.ManifestPath!);

        Assert.Equal(ResultStatus.Invalid, undone.Status);
        Assert.Equal("Pilot", await File.ReadAllTextAsync(Path.Combine(folder.Path, "Show - S01E01 - Pilot.mkv")));
        Assert.Equal("unrelated", await File.ReadAllTextAsync(Path.Combine(folder.Path, "Show.S01E01.mkv")));
    }

    private RenamePlan Plan(TempFolder folder, params (string Name, string Title)[] files)
    {
        var matches = files.Select((file, index) =>
        {
            string path = folder.Write(file.Name, file.Title);
            return Confirmed(path, index + 1, file.Title);
        }).ToList();
        return _planner.CreatePlan(folder.Path, Series, matches, RenameTemplates.PatternFor(RenameTemplates.PlexName), true);
    }

    private EpisodeMatch Confirmed(string path, int episode, string title)
    {
        var file = new VideoFile(path, Path.GetFileName(path), Path.GetExtension(path), 4);
        return _matcher.Confirm(file, _parser.Parse(file.FileName), [new TvEpisode(episode, 1, 1, episode, title, null)]);
    }

    private RenameExecutor Executor(TempFolder folder) =>
        new(_planner, new SystemFileMover(), NullLogger<RenameExecutor>.Instance, Path.Combine(folder.Path, "history"));

    private sealed class FailWhenDestinationContains(string fragment) : IFileMover
    {
        private readonly SystemFileMover _inner = new();

        public void Move(string source, string destination)
        {
            if (destination.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Simulated failure.");
            }

            _inner.Move(source, destination);
        }
    }
}
