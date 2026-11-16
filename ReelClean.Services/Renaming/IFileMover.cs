namespace ReelClean.Services.Renaming;

/// <summary>The only filesystem write the rename executor performs.</summary>
public interface IFileMover
{
    void Move(string source, string destination);
}
