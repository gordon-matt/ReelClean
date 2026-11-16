using CommunityToolkit.Mvvm.ComponentModel;

namespace ReelClean.ViewModels;

public sealed class MatchRow : ObservableObject
{
    private string _proposedFileName = string.Empty;
    private PlanStatus _status;
    private string _messages = string.Empty;

    public MatchRow(EpisodeMatch match)
    {
        Match = match;
    }

    public EpisodeMatch Match { get; private set; }

    public string FileName => Match.File.FileName;

    public string SeasonText => Match.Episodes.Count > 0
        ? Match.Episodes[0].SeasonNumber.ToString("00")
        : Match.Parsed.SeasonNumber?.ToString("00") ?? string.Empty;

    public string EpisodeText
    {
        get
        {
            var numbers = Match.Episodes.Count > 0
                ? Match.Episodes.Select(episode => episode.EpisodeNumber).ToList()
                : Match.Parsed.EpisodeNumbers.ToList();
            if (numbers.Count == 0)
            {
                return string.Empty;
            }

            var ordered = numbers.Order().ToList();
            if (ordered.Count == 1)
            {
                return ordered[0].ToString("00");
            }

            bool contiguous = ordered[^1] - ordered[0] == ordered.Count - 1;
            return contiguous
                ? $"{ordered[0]:00}-E{ordered[^1]:00}"
                : string.Join('+', ordered.Select(number => number.ToString("00")));
        }
    }

    public string Title => Match.Episodes.Count > 0
        ? string.Join(" + ", Match.Episodes.Select(episode => episode.Name))
        : Match.Parsed.DetectedEpisodeTitle ?? string.Empty;

    public string ConfidenceText => Match.Status == MatchStatus.Ignored ? string.Empty : Match.Confidence.ToString();

    public string StatusText => Status switch
    {
        PlanStatus.Ready => "Ready",
        PlanStatus.NeedsReview => "Review",
        PlanStatus.Invalid => "Invalid",
        PlanStatus.Ignored => "Ignored",
        _ => Status.ToString(),
    };

    public string ProposedFileName
    {
        get => _proposedFileName;
        private set => SetProperty(ref _proposedFileName, value);
    }

    public PlanStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string Messages
    {
        get => _messages;
        private set
        {
            if (SetProperty(ref _messages, value))
            {
                OnPropertyChanged(nameof(HasMessages));
            }
        }
    }

    public bool HasMessages => Messages.Length > 0;

    public void Update(EpisodeMatch match, RenameOperation operation)
    {
        Match = match;
        ProposedFileName = operation.ProposedFileName;
        Status = operation.Status;
        Messages = string.Join(" ", operation.Messages);
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(SeasonText));
        OnPropertyChanged(nameof(EpisodeText));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConfidenceText));
    }
}

public sealed record SeriesChoice(TvSeries Series)
{
    public string Label => Series.FirstAirYear is int year ? $"{Series.Name} ({year})" : Series.Name;
}

public sealed record EpisodeChoice(TvEpisode Episode)
{
    public string Label => $"S{Episode.SeasonNumber:00}E{Episode.EpisodeNumber:00}  {Episode.Name}";
}
