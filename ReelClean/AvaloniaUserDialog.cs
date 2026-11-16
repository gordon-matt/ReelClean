using Avalonia.Controls;
using ReelClean.Views;

namespace ReelClean;

public sealed class AvaloniaUserDialog : IUserDialog
{
    public Window? Owner { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        if (Owner is null)
        {
            return false;
        }

        var window = new ConfirmWindow(title, message);
        await window.ShowDialog(Owner);
        return window.Confirmed;
    }
}
