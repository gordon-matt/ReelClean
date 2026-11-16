namespace ReelClean;

public interface IFolderPicker
{
    Task<string?> PickAsync();
}
