namespace ReelClean.Services.Renaming;

public interface IRenameExecutor
{
    Task<Result<RenameExecutionResult>> ExecuteAsync(RenamePlan plan, CancellationToken cancellationToken = default);

    Task<Result<RenameExecutionResult>> UndoAsync(string manifestPath, CancellationToken cancellationToken = default);
}
