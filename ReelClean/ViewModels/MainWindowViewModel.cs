using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReelClean.Services;
using ReelClean.Services.Catalog;
using ReelClean.Services.Matching;
using ReelClean.Services.Naming;
using ReelClean.Services.Parsing;
using ReelClean.Services.Planning;
using ReelClean.Services.Renaming;
using ReelClean.Services.Scanning;
using ReelClean.Services.Settings;

namespace ReelClean.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileScanner _scanner;
    private readonly ITvMetadataProvider _metadata;
    private readonly IFilenameParser _parser;
    private readonly IEpisodeMatcher _matcher;
    private readonly IRenamePlanner _planner;
    private readonly IRenameExecutor _executor;
    private readonly ISettingsStore _settings;
    private readonly IFolderPicker _folders;
    private readonly IUserDialog _dialogs;
    private readonly ReelCleanOptions _options;
    private readonly List<TvEpisode> _catalog = [];
    private readonly List<EpisodeChoice> _episodeChoices = [];
    private CancellationTokenSource? _run;
    private int _runId;
    private SeriesChoice? _selectedSeries;
    private MatchRow? _selectedRow;
    private string _templateName;
    private string _customTemplate;
    private bool _includeEpisodeTitles;
    private bool _showNeedsReviewOnly;
    private string _episodeFilter = string.Empty;

    public MainWindowViewModel(
        IFileScanner scanner,
        ITvMetadataProvider metadata,
        IFilenameParser parser,
        IEpisodeMatcher matcher,
        IRenamePlanner planner,
        IRenameExecutor executor,
        ISettingsStore settings,
        IFolderPicker folders,
        IUserDialog dialogs,
        ReelCleanOptions options)
    {
        _scanner = scanner;
        _metadata = metadata;
        _parser = parser;
        _matcher = matcher;
        _planner = planner;
        _executor = executor;
        _settings = settings;
        _folders = folders;
        _dialogs = dialogs;
        _options = options;
        _templateName = string.IsNullOrWhiteSpace(options.TemplateName) ? RenameTemplates.PlexName : options.TemplateName;
        _customTemplate = options.CustomTemplate;
        _includeEpisodeTitles = options.IncludeEpisodeTitles;
        ApiKey = options.Tmdb.ApiKey;
        ScanSubdirectories = options.ScanSubdirectories;
        IncludeHiddenFiles = options.IncludeHiddenFiles;
    }

    public ObservableCollection<VideoFile> Files { get; } = [];

    public ObservableCollection<SeriesChoice> SeriesResults { get; } = [];

    public ObservableCollection<MatchRow> Rows { get; } = [];

    public ObservableCollection<MatchRow> VisibleRows { get; } = [];

    public ObservableCollection<EpisodeChoice> FilteredEpisodes { get; } = [];

    public IReadOnlyList<string> TemplateNames { get; } = RenameTemplates.Names;

    public SeriesChoice? SelectedSeries => _selectedSeries;

    public string SelectedSeriesLabel => _selectedSeries?.Label ?? "No series selected";

    public MatchRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                OnPropertyChanged(nameof(SelectedMessage));
            }
        }
    }

    public string SelectedMessage => _selectedRow?.Messages ?? string.Empty;

    public bool IsCustomTemplate => TemplateName == RenameTemplates.CustomName;

    public string TemplateName
    {
        get => _templateName;
        set
        {
            if (SetProperty(ref _templateName, value))
            {
                OnPropertyChanged(nameof(IsCustomTemplate));
                RebuildPlan();
            }
        }
    }

    public string CustomTemplate
    {
        get => _customTemplate;
        set
        {
            if (SetProperty(ref _customTemplate, value))
            {
                RebuildPlan();
            }
        }
    }

    public bool IncludeEpisodeTitles
    {
        get => _includeEpisodeTitles;
        set
        {
            if (SetProperty(ref _includeEpisodeTitles, value))
            {
                RebuildPlan();
            }
        }
    }

    public bool ShowNeedsReviewOnly
    {
        get => _showNeedsReviewOnly;
        set
        {
            if (SetProperty(ref _showNeedsReviewOnly, value))
            {
                RefreshVisible();
            }
        }
    }

    public string EpisodeFilter
    {
        get => _episodeFilter;
        set
        {
            if (SetProperty(ref _episodeFilter, value))
            {
                RefreshEpisodes();
            }
        }
    }

    [ObservableProperty]
    public partial string FolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ScanSubdirectories { get; set; }

    [ObservableProperty]
    public partial bool IncludeHiddenFiles { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileSummary { get; set; } = "No folder scanned.";

    [ObservableProperty]
    public partial string PlanSummary { get; set; } = "Scan a folder, then pick the series.";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Choose a folder of TV episodes.";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? LastManifest { get; set; }

    public RenamePlan? Plan { get; private set; }

    private bool CanRun() => !IsBusy;

    private bool CanRename() => !IsBusy && Plan?.CanExecute == true;

    private bool CanUndo() => !IsBusy && !string.IsNullOrWhiteSpace(LastManifest);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BrowseFolderAsync()
    {
        string? folder = await _folders.PickAsync();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        FolderPath = folder;
        await ScanAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ScanAsync() => RunAsync(ScanCoreAsync);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task SearchSeriesAsync() => RunAsync(async cancellationToken =>
    {
        ApplyOptions();
        var result = await _metadata.SearchSeriesAsync(SearchQuery, cancellationToken);
        if (!result.IsSuccess)
        {
            StatusMessage = Describe(result);
            return;
        }

        SeriesResults.Clear();
        SelectSeries(null);
        foreach (var series in result.Value)
        {
            SeriesResults.Add(new SeriesChoice(series));
        }

        StatusMessage = result.Value.Count == 0
            ? "No series matched that search."
            : $"Found {result.Value.Count} series. Select the one these files belong to.";
        await _settings.SaveAsync(_options, cancellationToken);
    });

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ReloadMetadataAsync()
    {
        if (_selectedSeries is null)
        {
            StatusMessage = "Select a series first.";
            return Task.CompletedTask;
        }

        return LoadSeriesAsync(_selectedSeries, refresh: true);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveSettingsAsync()
    {
        ApplyOptions();
        await _settings.SaveAsync(_options);
        StatusMessage = "Settings saved.";
        RebuildPlan();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void IgnoreSelected()
    {
        if (SelectedRow is null)
        {
            StatusMessage = "Select a file first.";
            return;
        }

        Replace(SelectedRow, _matcher.Ignore(SelectedRow.Match.File, SelectedRow.Match.Parsed));
        StatusMessage = $"Ignored {SelectedRow.FileName}.";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ClearSelected()
    {
        if (SelectedRow is null)
        {
            StatusMessage = "Select a file first.";
            return;
        }

        Replace(SelectedRow, _matcher.Clear(SelectedRow.Match.File, SelectedRow.Match.Parsed));
        StatusMessage = $"Cleared the match for {SelectedRow.FileName}.";
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private Task RenameAsync() => RunAsync(RenameCoreAsync);

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync() => RunAsync(async cancellationToken =>
    {
        if (string.IsNullOrWhiteSpace(LastManifest))
        {
            return;
        }

        var result = await _executor.UndoAsync(LastManifest, cancellationToken);
        StatusMessage = result.IsSuccess ? result.Value.Summary : Describe(result);
        if (result.IsSuccess)
        {
            LastManifest = null;
            await ScanCoreAsync(cancellationToken);
        }
    });

    [RelayCommand]
    private void Cancel()
    {
        _run?.Cancel();
    }

    public void SelectSeries(SeriesChoice? choice)
    {
        if (ReferenceEquals(_selectedSeries, choice))
        {
            return;
        }

        _selectedSeries = choice;
        OnPropertyChanged(nameof(SelectedSeries));
        OnPropertyChanged(nameof(SelectedSeriesLabel));
        if (choice is null)
        {
            ClearMatches();
            return;
        }

        _ = LoadSeriesAsync(choice, refresh: false);
    }

    public void AssignEpisodes(IReadOnlyList<TvEpisode> episodes)
    {
        if (SelectedRow is null)
        {
            StatusMessage = "Select a file first.";
            return;
        }

        if (episodes.Count == 0)
        {
            StatusMessage = "Select one or more episodes.";
            return;
        }

        var match = _matcher.Confirm(SelectedRow.Match.File, SelectedRow.Match.Parsed, episodes);
        Replace(SelectedRow, match);
        StatusMessage = match.Status == MatchStatus.Confirmed
            ? $"Assigned {SelectedRow.FileName}."
            : match.Explanation;
    }

    private Task LoadSeriesAsync(SeriesChoice choice, bool refresh) => RunAsync(async cancellationToken =>
    {
        ApplyOptions();
        StatusMessage = refresh ? $"Refreshing {choice.Label}..." : $"Loading {choice.Label}...";
        var details = await _metadata.GetSeriesAsync(choice.Series.TmdbId, refresh, cancellationToken);
        if (!details.IsSuccess)
        {
            StatusMessage = Describe(details);
            return;
        }

        var episodes = new List<TvEpisode>();
        foreach (int season in details.Value.SeasonNumbers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StatusMessage = $"Loading {choice.Label}, season {season}...";
            var loaded = await _metadata.GetSeasonEpisodesAsync(choice.Series.TmdbId, season, refresh, cancellationToken);
            if (!loaded.IsSuccess)
            {
                StatusMessage = Describe(loaded);
                return;
            }

            episodes.AddRange(loaded.Value);
        }

        _catalog.Clear();
        _catalog.AddRange(episodes);
        _episodeChoices.Clear();
        _episodeChoices.AddRange(episodes.Select(episode => new EpisodeChoice(episode)));
        RefreshEpisodes();
        cancellationToken.ThrowIfCancellationRequested();
        MatchFiles();
        StatusMessage = $"Loaded {episodes.Count} episodes for {choice.Label}.";
        await _settings.SaveAsync(_options, cancellationToken);
    });

    private async Task ScanCoreAsync(CancellationToken cancellationToken)
    {
        ApplyOptions();
        var result = await _scanner.ScanAsync(FolderPath, ScanSubdirectories, IncludeHiddenFiles, cancellationToken);
        if (!result.IsSuccess)
        {
            StatusMessage = Describe(result);
            return;
        }

        Files.Clear();
        foreach (var file in result.Value)
        {
            Files.Add(file);
        }

        FileSummary = result.Value.Count == 1 ? "1 video file." : $"{result.Value.Count} video files.";
        if (_selectedSeries is null)
        {
            ClearMatches();
            StatusMessage = FileSummary + " Search for the series these belong to.";
            return;
        }

        MatchFiles();
        StatusMessage = $"Scanned {FileSummary.TrimEnd('.')}.";
    }

    private async Task RenameCoreAsync(CancellationToken cancellationToken)
    {
        if (Plan is null || !Plan.CanExecute)
        {
            StatusMessage = "Resolve review and invalid items before renaming.";
            return;
        }

        int count = Plan.Operations.Count(operation =>
            operation.Status == PlanStatus.Ready
            && !PathComparison.SameExactPath(operation.Match.File.FullPath, operation.DestinationPath));
        if (count == 0)
        {
            StatusMessage = "Those files already have the proposed names.";
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Rename files",
            $"Rename {count} file(s)? Nothing is overwritten. You can undo afterwards if the new files are still in place.");
        if (!confirmed)
        {
            StatusMessage = "Rename cancelled.";
            return;
        }

        var result = await _executor.ExecuteAsync(Plan, cancellationToken);
        if (!result.IsSuccess)
        {
            StatusMessage = Describe(result);
            return;
        }

        StatusMessage = result.Value.Summary;
        if (result.Value is { RolledBack: false, ManifestPath: not null })
        {
            LastManifest = result.Value.ManifestPath;
            await ScanCoreAsync(cancellationToken);
        }
    }

    private void MatchFiles()
    {
        Rows.Clear();
        foreach (var file in Files)
        {
            var parsed = _parser.Parse(file.FileName);
            Rows.Add(new MatchRow(_matcher.Match(file, parsed, _catalog)));
        }

        RebuildPlan();
    }

    private void Replace(MatchRow row, EpisodeMatch match)
    {
        int index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var updated = new MatchRow(match);
        Rows[index] = updated;
        SelectedRow = updated;
        RebuildPlan();
    }

    private void RebuildPlan()
    {
        if (_selectedSeries is null || Rows.Count == 0)
        {
            Plan = null;
            PlanSummary = Files.Count == 0 ? "Scan a folder to begin." : "Select a series to match these files.";
            NotifyPlan();
            return;
        }

        Plan = _planner.CreatePlan(FolderPath, _selectedSeries.Series, Rows.Select(row => row.Match).ToList(), CurrentTemplate(), IncludeEpisodeTitles);
        foreach (var operation in Plan.Operations)
        {
            var row = Rows.FirstOrDefault(item => PathComparison.Equal(item.Match.File.FullPath, operation.Match.File.FullPath));
            row?.Update(operation.Match, operation);
        }

        PlanSummary = $"{Plan.ReadyCount} ready, {Plan.ReviewCount} to review, {Plan.InvalidCount} invalid, {Plan.IgnoredCount} ignored.";
        RefreshVisible();
        NotifyPlan();
    }

    private void RefreshVisible()
    {
        var selected = SelectedRow;
        VisibleRows.Clear();
        foreach (var row in Rows)
        {
            if (!ShowNeedsReviewOnly || row.Status is PlanStatus.NeedsReview or PlanStatus.Invalid)
            {
                VisibleRows.Add(row);
            }
        }

        if (selected is not null && VisibleRows.Contains(selected))
        {
            SelectedRow = selected;
        }
    }

    private void RefreshEpisodes()
    {
        FilteredEpisodes.Clear();
        IEnumerable<EpisodeChoice> choices = _episodeChoices;
        if (!string.IsNullOrWhiteSpace(EpisodeFilter))
        {
            string term = EpisodeFilter.Trim();
            choices = choices.Where(choice => choice.Label.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var choice in choices)
        {
            FilteredEpisodes.Add(choice);
        }
    }

    private void ClearMatches()
    {
        _catalog.Clear();
        _episodeChoices.Clear();
        FilteredEpisodes.Clear();
        Rows.Clear();
        VisibleRows.Clear();
        SelectedRow = null;
        Plan = null;
        PlanSummary = Files.Count == 0 ? "Scan a folder to begin." : "Select a series to match these files.";
        NotifyPlan();
    }

    private string CurrentTemplate() => RenameTemplates.PatternFor(TemplateName, CustomTemplate);

    private void ApplyOptions()
    {
        _options.Tmdb.ApiKey = ApiKey.Trim();
        _options.ScanSubdirectories = ScanSubdirectories;
        _options.IncludeHiddenFiles = IncludeHiddenFiles;
        _options.IncludeEpisodeTitles = IncludeEpisodeTitles;
        _options.TemplateName = TemplateName;
        _options.CustomTemplate = CustomTemplate ?? string.Empty;
    }

    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        int run = ++_runId;
        _run?.Cancel();
        _run = new CancellationTokenSource();
        var token = _run.Token;
        IsBusy = true;
        NotifyCommands();
        try
        {
            await work(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (run == _runId)
            {
                StatusMessage = "Cancelled.";
            }
        }
        finally
        {
            if (run == _runId)
            {
                IsBusy = false;
                NotifyCommands();
            }
        }
    }

    private void NotifyPlan()
    {
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(CanRename));
        RenameCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        BrowseFolderCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
        SearchSeriesCommand.NotifyCanExecuteChanged();
        ReloadMetadataCommand.NotifyCanExecuteChanged();
        SaveSettingsCommand.NotifyCanExecuteChanged();
        IgnoreSelectedCommand.NotifyCanExecuteChanged();
        ClearSelectedCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }

    private static string Describe(IResult result)
    {
        var validation = result.ValidationErrors
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();
        if (validation.Count > 0)
        {
            return string.Join(" ", validation);
        }

        var errors = result.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToList();
        return errors.Count > 0 ? string.Join(" ", errors) : "Something went wrong.";
    }
}
