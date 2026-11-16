using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ReelClean;

public sealed class AvaloniaFolderPicker : IFolderPicker
{
    public Window? Owner { get; set; }

    public async Task<string?> PickAsync()
    {
        if (Owner?.StorageProvider is not { } storage)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder of episodes",
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
