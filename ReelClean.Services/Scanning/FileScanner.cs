namespace ReelClean.Services.Scanning;

public sealed class FileScanner : IFileScanner
{
    public Task<Result<IReadOnlyList<VideoFile>>> ScanAsync(
        string folderPath,
        bool includeSubdirectories,
        bool includeHiddenFiles,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(folderPath, includeSubdirectories, includeHiddenFiles, cancellationToken), cancellationToken);
    }

    private static Result<IReadOnlyList<VideoFile>> Scan(
        string folderPath,
        bool includeSubdirectories,
        bool includeHiddenFiles,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Result<IReadOnlyList<VideoFile>>.Invalid(new ValidationError(nameof(folderPath), "Choose a folder to scan."));
        }

        if (!Directory.Exists(folderPath))
        {
            return Result<IReadOnlyList<VideoFile>>.NotFound("That folder does not exist.");
        }

        try
        {
            var files = new List<VideoFile>();
            Walk(new DirectoryInfo(folderPath), includeSubdirectories, includeHiddenFiles, files, cancellationToken, reportErrors: true);
            files.Sort((left, right) => string.Compare(left.FullPath, right.FullPath, StringComparison.OrdinalIgnoreCase));
            return Result<IReadOnlyList<VideoFile>>.Success(files);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<VideoFile>>.Error("That folder could not be read.");
        }
    }

    private static void Walk(
        DirectoryInfo directory,
        bool recursive,
        bool includeHiddenFiles,
        List<VideoFile> files,
        CancellationToken cancellationToken,
        bool reportErrors)
    {
        if (!reportErrors && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        IEnumerable<FileInfo> entries;
        try
        {
            entries = directory.EnumerateFiles();
        }
        catch (Exception ex) when (!reportErrors && ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (!includeHiddenFiles && file.Attributes.HasFlag(FileAttributes.Hidden))
            {
                continue;
            }

            if (!Constants.VideoExtensions.Contains(file.Extension))
            {
                continue;
            }

            files.Add(new VideoFile(file.FullName, file.Name, file.Extension, file.Length));
        }

        if (!recursive)
        {
            return;
        }

        IEnumerable<DirectoryInfo> children;
        try
        {
            children = directory.EnumerateDirectories();
        }
        catch (Exception ex) when (!reportErrors && ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (!includeHiddenFiles && child.Attributes.HasFlag(FileAttributes.Hidden))
            {
                continue;
            }

            Walk(child, recursive, includeHiddenFiles, files, cancellationToken, reportErrors: false);
        }
    }
}
