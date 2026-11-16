namespace ReelClean.Services.Renaming;

public sealed class SystemFileMover : IFileMover
{
    public void Move(string source, string destination)
    {
        try
        {
            File.Move(source, destination);
        }
        catch (IOException) when (IsReadOnly(source))
        {
            File.SetAttributes(source, File.GetAttributes(source) & ~FileAttributes.ReadOnly);
            File.Move(source, destination);
        }
    }

    private static bool IsReadOnly(string path) =>
        File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
}
