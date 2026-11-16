namespace ReelClean.Models;

public sealed record RenameOperation(
    EpisodeMatch Match,
    string DestinationPath,
    string ProposedFileName,
    PlanStatus Status,
    IReadOnlyList<string> Messages);

/// <summary>A snapshot of proposed renames. Creating a plan does not touch the disk.</summary>
public sealed record RenamePlan(
    Guid Id,
    string FolderPath,
    string Template,
    IReadOnlyList<RenameOperation> Operations)
{
    public int ReadyCount => Operations.Count(operation => operation.Status == PlanStatus.Ready);

    public int ReviewCount => Operations.Count(operation => operation.Status == PlanStatus.NeedsReview);

    public int InvalidCount => Operations.Count(operation => operation.Status == PlanStatus.Invalid);

    public int IgnoredCount => Operations.Count(operation => operation.Status == PlanStatus.Ignored);

    public bool CanExecute =>
        Operations.Any(operation => operation.Status == PlanStatus.Ready)
        && Operations.All(operation => operation.Status is PlanStatus.Ready or PlanStatus.Ignored);
}

public sealed record ExecutedRename(string SourcePath, string DestinationPath);

public sealed record RenameFailure(string SourcePath, string DestinationPath, string Error);

public sealed record RenameExecutionResult(
    Guid PlanId,
    string? ManifestPath,
    IReadOnlyList<ExecutedRename> Succeeded,
    IReadOnlyList<RenameFailure> Failed,
    bool RolledBack,
    string Summary);

public sealed record RenameManifest(
    Guid Id,
    Guid PlanId,
    DateTimeOffset ExecutedAt,
    IReadOnlyList<RenameManifestEntry> Entries);

public sealed record RenameManifestEntry(string OriginalPath, string NewPath);

/// <summary>Series details cached with the season numbers TMDB returned for it.</summary>
public sealed record SeriesCacheEntry(TvSeries Series, IReadOnlyList<int> SeasonNumbers);
