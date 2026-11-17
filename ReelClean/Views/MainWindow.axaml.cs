using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReelClean;
using ReelClean.ViewModels;

namespace ReelClean.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => WindowCaption.UseDarkFrame(this);
        try
        {
            Icon = Branding.Icon;
        }
        catch (Exception)
        {
            // A missing brand asset should not stop the window from opening.
        }
    }

    private void Poster_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SeriesChoice choice } && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ChooseSeries(choice);
        }
    }

    private void SearchQuery_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel viewModel && viewModel.SearchSeriesCommand.CanExecute(null))
        {
            viewModel.SearchSeriesCommand.Execute(null);
        }
    }

    private void AddMapping_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var episodes = DestinationList.SelectedItems?.OfType<EpisodeChoice>().Select(choice => choice.Episode).ToList() ?? [];
        viewModel.AddMapping(SourceList.SelectedItem as VideoFile, episodes);
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        SettingsLayer.IsVisible = true;
    }

    private void CloseSettings_Click(object? sender, RoutedEventArgs e)
    {
        SettingsLayer.IsVisible = false;
    }

    private void SettingsScrim_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        SettingsLayer.IsVisible = false;
    }

    private void Token_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string token } || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        int caret = viewModel.IsCustomTemplate ? CustomTemplateBox.CaretIndex : viewModel.CurrentPattern.Length;
        viewModel.InsertToken(token, caret);
        CustomTemplateBox.CaretIndex = Math.Min(caret + token.Length, viewModel.CustomTemplate.Length);
        CustomTemplateBox.Focus();
    }

}
