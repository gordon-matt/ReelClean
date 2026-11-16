namespace ReelClean.Services.Naming;

public interface IFilenameSanitizer
{
    /// <summary>Makes one path segment safe on Windows, macOS, and Linux.</summary>
    string SanitizeFileName(string name);
}
