using ReelClean.Services.Naming;

namespace ReelClean.Services.Planning;

public sealed class RenamePlanner(IRenameTemplateFormatter formatter, IFilenameSanitizer sanitizer) : IRenamePlanner
{
    public RenamePlan CreatePlan(
        string folderPath,
        TvSeries series,
        IReadOnlyList<EpisodeMatch> matches,
        string template,
        bool includeEpisodeTitles)
    {
        var operations = new List<RenameOperation>(matches.Count);
        foreach (var match in matches)
        {
            operations.Add(Build(folderPath, series, match, template, includeEpisodeTitles));
        }

        MarkDuplicateDestinations(operations);
        return new RenamePlan(Guid.NewGuid(), folderPath, template, operations);
    }

    public Result<RenamePlan> Revalidate(RenamePlan plan)
    {
        if (!plan.CanExecute)
        {
            return Result<RenamePlan>.Invalid(new ValidationError(
                "plan",
                "Resolve review and invalid items before renaming. Ignored files are left as they are."));
        }

        var problems = new List<ValidationError>();
        var sources = plan.Operations
            .Where(operation => operation.Status == PlanStatus.Ready)
            .Select(operation => PathComparison.Full(operation.Match.File.FullPath))
            .ToHashSet(PathComparison.Comparer);

        var destinations = new HashSet<string>(PathComparison.Comparer);
        foreach (var operation in plan.Operations.Where(operation => operation.Status == PlanStatus.Ready))
        {
            string source = operation.Match.File.FullPath;
            string name = Path.GetFileName(source);
            if (!File.Exists(source))
            {
                problems.Add(new ValidationError(source, $"{name} is no longer in the folder."));
                continue;
            }

            string destination = PathComparison.Full(operation.DestinationPath);
            if (!destinations.Add(destination))
            {
                problems.Add(new ValidationError(source, $"{name} would collide with another renamed file."));
                continue;
            }

            if (PathComparison.Equal(source, operation.DestinationPath))
            {
                continue;
            }

            bool occupied = File.Exists(operation.DestinationPath) || Directory.Exists(operation.DestinationPath);
            if (occupied && !sources.Contains(destination))
            {
                problems.Add(new ValidationError(source, $"{Path.GetFileName(operation.DestinationPath)} already exists."));
            }
        }

        return problems.Count == 0
            ? Result<RenamePlan>.Success(plan)
            : Result<RenamePlan>.Invalid(problems);
    }

    private RenameOperation Build(
        string folderPath,
        TvSeries series,
        EpisodeMatch match,
        string template,
        bool includeEpisodeTitles)
    {
        if (match.Status == MatchStatus.Ignored)
        {
            return new RenameOperation(match, match.File.FullPath, match.File.FileName, PlanStatus.Ignored, []);
        }

        if (match.Episodes.Count == 0 || match.Status == MatchStatus.Unmatched)
        {
            return Invalid(match, match.File.FileName, match.Explanation);
        }

        string relative = formatter.Format(template, series, match.Episodes, match.File.FileName, includeEpisodeTitles);
        var rawSegments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawSegments.Length == 0 || rawSegments.Any(segment => segment is "." or ".."))
        {
            return Invalid(match, match.File.FileName, "The template did not produce a usable file name.");
        }

        var segments = rawSegments.Select(sanitizer.SanitizeFileName).ToList();
        string extension = match.File.Extension;
        if (extension.Length > 0 && !segments[^1].EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            segments[^1] = sanitizer.SanitizeFileName(segments[^1] + extension);
        }

        string? directory = Path.GetDirectoryName(match.File.FullPath);
        if (string.IsNullOrEmpty(directory))
        {
            return Invalid(match, segments[^1], "The source file does not have a folder.");
        }

        string destination = directory;
        foreach (string segment in segments)
        {
            if (Path.IsPathRooted(segment))
            {
                return Invalid(match, segments[^1], "The template path is not allowed.");
            }

            destination = Path.Combine(destination, segment);
        }

        string proposed = Path.GetRelativePath(folderPath, destination);
        if (!PathComparison.IsInside(directory, destination))
        {
            return Invalid(match, proposed, "The destination would leave the source folder.");
        }

        if (destination.Length > Constants.MaxPathLength)
        {
            return Invalid(match, proposed, "The destination path is too long.");
        }

        var messages = new List<string>();
        var status = StatusFor(match);
        if (status == PlanStatus.NeedsReview)
        {
            messages.Add(match.Explanation);
        }

        return new RenameOperation(match, destination, proposed, status, messages);
    }

    private static void MarkDuplicateDestinations(List<RenameOperation> operations)
    {
        var sources = operations
            .Where(operation => operation.Status is PlanStatus.Ready or PlanStatus.NeedsReview)
            .Select(operation => PathComparison.Full(operation.Match.File.FullPath))
            .ToHashSet(PathComparison.Comparer);

        var seen = new Dictionary<string, int>(PathComparison.Comparer);
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            if (operation.Status is PlanStatus.Ignored or PlanStatus.Invalid)
            {
                continue;
            }

            string destination = PathComparison.Full(operation.DestinationPath);
            if (seen.TryGetValue(destination, out int first))
            {
                operations[first] = WithProblem(operations[first], "Another file would be renamed to the same path.");
                operations[index] = WithProblem(operation, "Another file would be renamed to the same path.");
                continue;
            }

            seen[destination] = index;
            if (PathComparison.Equal(operation.Match.File.FullPath, operation.DestinationPath))
            {
                continue;
            }

            bool occupied = File.Exists(operation.DestinationPath) || Directory.Exists(operation.DestinationPath);
            if (occupied && !sources.Contains(destination))
            {
                operations[index] = WithProblem(operation, $"{Path.GetFileName(operation.DestinationPath)} already exists.");
            }
        }
    }

    private static PlanStatus StatusFor(EpisodeMatch match) => match.Status switch
    {
        MatchStatus.Ignored => PlanStatus.Ignored,
        MatchStatus.Unmatched => PlanStatus.Invalid,
        MatchStatus.Confirmed => PlanStatus.Ready,
        MatchStatus.Suggested when match.Confidence == MatchConfidence.High => PlanStatus.Ready,
        MatchStatus.Suggested => PlanStatus.NeedsReview,
        _ => PlanStatus.Invalid,
    };

    private static RenameOperation Invalid(EpisodeMatch match, string proposed, string message) =>
        new(match, match.File.FullPath, proposed, PlanStatus.Invalid, [message]);

    private static RenameOperation WithProblem(RenameOperation operation, string message)
    {
        var messages = operation.Messages.Append(message).ToList();
        return operation with { Status = PlanStatus.Invalid, Messages = messages };
    }
}
