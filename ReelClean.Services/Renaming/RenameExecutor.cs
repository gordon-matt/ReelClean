using System.Text.Json;
using ReelClean.Services.Planning;

namespace ReelClean.Services.Renaming;

public sealed class RenameExecutor(
    IRenamePlanner planner,
    IFileMover mover,
    ILogger<RenameExecutor> logger,
    string historyDirectory) : IRenameExecutor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<Result<RenameExecutionResult>> ExecuteAsync(RenamePlan plan, CancellationToken cancellationToken = default)
    {
        var revalidated = planner.Revalidate(plan);
        if (!revalidated.IsSuccess)
        {
            return Result<RenameExecutionResult>.Invalid(revalidated.ValidationErrors.ToList());
        }

        cancellationToken.ThrowIfCancellationRequested();

        var work = new List<PendingRename>();
        foreach (var operation in plan.Operations.Where(operation => operation.Status == PlanStatus.Ready))
        {
            if (PathComparison.SameExactPath(operation.Match.File.FullPath, operation.DestinationPath))
            {
                continue;
            }

            string? sourceDirectory = Path.GetDirectoryName(operation.Match.File.FullPath);
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                return Failure(plan, ["Could not rename " + operation.Match.File.FileName + "."], rolledBack: false);
            }

            string temp = TempPath(sourceDirectory);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                mover.Move(operation.Match.File.FullPath, temp);
                work.Add(new PendingRename(operation.Match.File.FullPath, operation.DestinationPath, temp));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogError(ex, "Could not start rename of {Source}", operation.Match.File.FullPath);
                var restoreErrors = Restore(work);
                return Failure(plan, restoreErrors.Prepend(ex.Message).ToList(), rolledBack: restoreErrors.Count == 0);
            }
        }

        foreach (var item in work)
        {
            string? destinationDirectory = Path.GetDirectoryName(item.Destination);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (File.Exists(item.Destination) || Directory.Exists(item.Destination))
                {
                    throw new IOException($"{Path.GetFileName(item.Destination)} already exists.");
                }

                mover.Move(item.Temp, item.Destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogError(ex, "Rename failed for {Source}", item.Source);
                var errors = Restore(work);
                bool rolledBack = errors.Count == 0;
                return Failure(plan, errors.Prepend(ex.Message).ToList(), rolledBack);
            }
        }

        var succeeded = work.Select(item => new ExecutedRename(item.Source, item.Destination)).ToList();
        if (succeeded.Count == 0)
        {
            return Result<RenameExecutionResult>.Success(new RenameExecutionResult(
                plan.Id,
                null,
                [],
                [],
                false,
                "Those files already have the proposed names."));
        }

        var manifest = new RenameManifest(
            Guid.NewGuid(),
            plan.Id,
            DateTimeOffset.Now,
            succeeded.Select(item => new RenameManifestEntry(item.SourcePath, item.DestinationPath)).ToList());
        string manifestPath = await WriteManifestAsync(manifest, cancellationToken);
        return Result<RenameExecutionResult>.Success(new RenameExecutionResult(
            plan.Id,
            manifestPath,
            succeeded,
            [],
            false,
            $"Renamed {succeeded.Count} file(s)."));
    }

    public async Task<Result<RenameExecutionResult>> UndoAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return Result<RenameExecutionResult>.NotFound("That rename history could not be found.");
        }

        RenameManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<RenameManifest>(stream, Json, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not read rename manifest {Path}", manifestPath);
            return Result<RenameExecutionResult>.Error("That rename history could not be read.");
        }

        if (manifest is null || manifest.Entries.Count == 0)
        {
            return Result<RenameExecutionResult>.Error("That rename history is empty.");
        }

        var problems = ValidateUndo(manifest);
        if (problems.Count > 0)
        {
            return Result<RenameExecutionResult>.Invalid(problems);
        }

        var pending = new List<PendingRename>();
        foreach (var entry in manifest.Entries)
        {
            string? directory = Path.GetDirectoryName(entry.NewPath);
            if (string.IsNullOrEmpty(directory))
            {
                return Result<RenameExecutionResult>.Error("The rename history has an invalid path.");
            }

            string temp = TempPath(directory);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                mover.Move(entry.NewPath, temp);
                pending.Add(new PendingRename(entry.NewPath, entry.OriginalPath, temp));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogError(ex, "Undo could not move {Path}", entry.NewPath);
                Restore(pending);
                return Result<RenameExecutionResult>.Error("Undo stopped and put the moved files back. " + ex.Message);
            }
        }

        foreach (var item in pending)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? directory = Path.GetDirectoryName(item.Destination);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                mover.Move(item.Temp, item.Destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                logger.LogError(ex, "Undo failed while restoring {Path}", item.Destination);
                Restore(pending);
                return Result<RenameExecutionResult>.Error("Undo did not finish. " + ex.Message);
            }
        }

        var restored = pending.Select(item => new ExecutedRename(item.Source, item.Destination)).ToList();
        return Result<RenameExecutionResult>.Success(new RenameExecutionResult(
            manifest.PlanId,
            manifestPath,
            restored,
            [],
            false,
            $"Restored {restored.Count} file(s)."));
    }

    private static List<ValidationError> ValidateUndo(RenameManifest manifest)
    {
        var currentPaths = manifest.Entries.Select(entry => PathComparison.Full(entry.NewPath)).ToHashSet(PathComparison.Comparer);
        var problems = new List<ValidationError>();
        foreach (var entry in manifest.Entries)
        {
            if (!File.Exists(entry.NewPath))
            {
                problems.Add(new ValidationError(entry.NewPath, $"{Path.GetFileName(entry.NewPath)} is no longer where the rename left it."));
                continue;
            }

            if (!File.Exists(entry.OriginalPath))
            {
                continue;
            }

            bool occupiedByThisRename = currentPaths.Contains(PathComparison.Full(entry.OriginalPath));
            bool caseOnly = PathComparison.Equal(entry.OriginalPath, entry.NewPath);
            if (!occupiedByThisRename && !caseOnly)
            {
                problems.Add(new ValidationError(entry.OriginalPath, $"{Path.GetFileName(entry.OriginalPath)} is in the way, so the undo was not started."));
            }
        }

        return problems;
    }

    private async Task<string> WriteManifestAsync(RenameManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(historyDirectory);
        string path = Path.Combine(historyDirectory, manifest.Id.ToString("N") + ".json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, Json, cancellationToken);
        return path;
    }

    /// <summary>
    /// Puts a batch back by returning finished files to their temp names first.
    /// That frees swap targets before anything is moved to its original path.
    /// </summary>
    private List<string> Restore(IReadOnlyList<PendingRename> work)
    {
        var errors = new List<string>();
        foreach (var item in work)
        {
            TryMove(item.Destination, item.Temp, item.Source, errors);
        }

        foreach (var item in work)
        {
            TryMove(item.Temp, item.Source, item.Source, errors);
        }

        return errors;
    }

    private void TryMove(string from, string to, string label, List<string> errors)
    {
        try
        {
            if (File.Exists(from) && !File.Exists(to))
            {
                mover.Move(from, to);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not restore {Source}", label);
            errors.Add($"Could not restore {Path.GetFileName(label)}. Look for a .reelclean.tmp file beside it.");
        }
    }

    private static Result<RenameExecutionResult> Failure(RenamePlan plan, IReadOnlyList<string> errors, bool rolledBack)
    {
        string summary = rolledBack
            ? "The rename was rolled back. No files were left under the new names."
            : "The rename failed and not every file could be put back. Look for .reelclean.tmp files.";
        if (errors.Count > 0)
        {
            summary = summary + " " + errors[0];
        }

        var failed = errors.Select(error => new RenameFailure(string.Empty, string.Empty, error)).ToList();
        return Result<RenameExecutionResult>.Success(new RenameExecutionResult(plan.Id, null, [], failed, rolledBack, summary));
    }

    private static string TempPath(string directory) =>
        Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".reelclean.tmp");

    private sealed record PendingRename(string Source, string Destination, string Temp);
}
