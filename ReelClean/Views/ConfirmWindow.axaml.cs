using Avalonia.Controls;
using Avalonia.Interactivity;

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
