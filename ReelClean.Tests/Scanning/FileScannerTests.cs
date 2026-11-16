using ReelClean.Services.Scanning;

namespace ReelClean.Tests.Scanning;

public class FileScannerTests
{
    private readonly FileScanner _scanner = new();

    [Fact]
    public async Task ScanAsync_ReturnsVideoFilesOnly()
    {
        using var folder = new TempFolder();
        folder.Write("Pilot.mkv", "video");
        folder.Write("notes.txt", "text");
        folder.Write("nested\\Later.mp4", "video");

        var top = await _scanner.ScanAsync(folder.Path, includeSubdirectories: false, includeHiddenFiles: false);
        var recursive = await _scanner.ScanAsync(folder.Path, includeSubdirectories: true, includeHiddenFiles: false);

        Assert.Equal("Pilot.mkv", Assert.Single(top.Value).FileName);
        Assert.Equal(2, recursive.Value.Count);
    }

    [Fact]
    public async Task ScanAsync_MissingFolder_IsNotFound()
    {
        var result = await _scanner.ScanAsync(Path.Combine(Path.GetTempPath(), "reelclean-missing-" + Guid.NewGuid().ToString("N")), false, false);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
