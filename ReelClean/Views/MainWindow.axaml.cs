using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReelClean.ViewModels;

namespace ReelClean.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void UseSelectedEpisodes_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AssignEpisodes(SelectedEpisodes(), SelectedFiles());
    }

    private void CheckSelectedEpisodes_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CheckEpisodes(SelectedEpisodes(), check: true);
        }
    }

    private void UncheckSelectedEpisodes_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CheckEpisodes(SelectedEpisodes(), check: false);
        }
    }

    private void CheckSelectedFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CheckFiles(SelectedFiles(), check: true);
        }
    }

    private void UncheckSelectedFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CheckFiles(SelectedFiles(), check: false);
        }
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

    private List<EpisodeChoice> SelectedEpisodes() =>
        EpisodeList.SelectedItems?.OfType<EpisodeChoice>().ToList() ?? [];

    private List<MatchRow> SelectedFiles() =>
        MatchGrid.SelectedItems?.OfType<MatchRow>().ToList() ?? [];
}
