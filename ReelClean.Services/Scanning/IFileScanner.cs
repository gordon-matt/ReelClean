namespace ReelClean.Services.Scanning;

public interface IFileScanner
{
    Task<Result<IReadOnlyList<VideoFile>>> ScanAsync(
        string folderPath,
        bool includeSubdirectories,
        bool includeHiddenFiles,
        CancellationToken cancellationToken = default);
}
