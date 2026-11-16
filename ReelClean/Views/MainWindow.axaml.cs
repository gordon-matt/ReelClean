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

    private void SeriesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox list)
        {
            viewModel.SelectSeries(list.SelectedItem as SeriesChoice);
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

        var episodes = EpisodeList.SelectedItems?.OfType<EpisodeChoice>().Select(choice => choice.Episode).ToList() ?? [];
        viewModel.AssignEpisodes(episodes);
    }
}
