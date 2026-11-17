using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ReelClean.ViewModels;

public sealed partial class MappingRow : ObservableObject
{
    public MappingRow(EpisodeMatch match)
    {
        Match = match;
        Destination = Describe(match);
    }

    public EpisodeMatch Match { get; private set; }

    public string Source => Match.File.FileName;

    public string Destination { get; private set; }

    public string Confidence => Match.Confidence.ToString();

    [ObservableProperty]
    public partial string Proposed { get; set; } = string.Empty;

    public void Apply(RenameOperation operation)
    {
        Match = operation.Match;
        Destination = Describe(operation.Match);
        Proposed = operation.ProposedFileName;
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(Destination));
        OnPropertyChanged(nameof(Confidence));
    }

    private static string Describe(EpisodeMatch match)
    {
        var episodes = match.Episodes.OrderBy(episode => episode.SeasonNumber).ThenBy(episode => episode.EpisodeNumber).ToList();
        if (episodes.Count == 0)
        {
            return string.Empty;
        }

        bool contiguous = episodes.Count == 1
            || (episodes[^1].EpisodeNumber - episodes[0].EpisodeNumber == episodes.Count - 1
                && episodes.All(episode => episode.SeasonNumber == episodes[0].SeasonNumber));
        string numbers = episodes.Count == 1
            ? $"S{episodes[0].SeasonNumber:00}E{episodes[0].EpisodeNumber:00}"
            : contiguous
                ? $"S{episodes[0].SeasonNumber:00}E{episodes[0].EpisodeNumber:00}-E{episodes[^1].EpisodeNumber:00}"
                : string.Join(", ", episodes.Select(episode => $"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}"));
        return $"{numbers}  {string.Join(" + ", episodes.Select(episode => episode.Name))}";
    }
}

public sealed partial class SeriesChoice : ObservableObject
{
    public SeriesChoice(TvSeries series)
    {
        Series = series;
        Label = series.FirstAirYear is int year ? $"{series.Name} ({year})" : series.Name;
    }

    public TvSeries Series { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed partial class EpisodeChoice : ObservableObject
{
    public EpisodeChoice(TvEpisode episode)
    {
        Episode = episode;
        Label = $"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00}  {episode.Name}";
    }

    public TvEpisode Episode { get; }

    public string Label { get; }
}
