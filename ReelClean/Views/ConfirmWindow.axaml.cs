using Avalonia.Controls;
using Avalonia.Interactivity;
using ReelClean;

namespace ReelClean.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
        : this("Rename files", "Rename the files in this plan?")
    {
    }

    public ConfirmWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        Opened += (_, _) => WindowCaption.UseDarkFrame(this);
        try
        {
            Icon = Branding.Icon;
        }
        catch (Exception)
        {
            // A missing brand asset should not stop the dialog from opening.
        }
    }

    public bool Confirmed { get; private set; }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
