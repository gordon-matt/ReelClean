using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Media.Imaging;
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
    private readonly PosterLoader _posters;
    private readonly ReelCleanOptions _options;
    private readonly List<TvEpisode> _catalog = [];
    private readonly List<EpisodeChoice> _episodeChoices = [];
    private CancellationTokenSource? _run;
    private int _runId;
    private SeriesChoice? _selectedSeries;
    private MappingRow? _selectedMapping;
    private VideoFile? _selectedSourceFile;
    private string _templateName;
    private string _customTemplate;
    private bool _includeEpisodeTitles;
    private string _episodeFilter = string.Empty;
    private int _step;
    private bool _episodesReady;
    private CancellationTokenSource? _saveDelay;

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
        PosterLoader posters,
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
        _posters = posters;
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

    public ObservableCollection<VideoFile> SourceFiles { get; } = [];

    public ObservableCollection<EpisodeChoice> DestinationEpisodes { get; } = [];

    public ObservableCollection<MappingRow> Mappings { get; } = [];

    public IReadOnlyList<string> TemplateNames { get; } = RenameTemplates.Names;

    public IReadOnlyList<TemplateToken> TemplateTokens { get; } = RenameTemplates.Tokens;

    public SeriesChoice? SelectedSeries => _selectedSeries;

    public string SelectedSeriesLabel => _selectedSeries?.Label ?? "No series selected";

    public Bitmap? SelectedPoster => _selectedSeries?.Poster;

    public int Step => _step;

    public bool IsFolderStep => _step == 0;

    public bool IsSeriesStep => _step == 1;

    public bool IsMatchStep => _step == 2;

    public bool IsRenameStep => _step == 3;

    public string StepTitle => _step switch
    {
        0 => "1 of 4  ·  Folder",
        1 => "2 of 4  ·  Series",
        2 => "3 of 4  ·  Match",
        _ => "4 of 4  ·  Rename",
    };

    public VideoFile? SelectedSourceFile
    {
        get => _selectedSourceFile;
        set => SetProperty(ref _selectedSourceFile, value);
    }

    public MappingRow? SelectedMapping
    {
        get => _selectedMapping;
        set => SetProperty(ref _selectedMapping, value);
    }

    public bool IsCustomTemplate => TemplateName == RenameTemplates.CustomName;

    public string CurrentPattern => RenameTemplates.PatternFor(TemplateName, CustomTemplate);

    public bool HasSeriesDetails => _selectedSeries is not null;

    public string SelectedOverview => string.IsNullOrWhiteSpace(_selectedSeries?.Series.Overview)
        ? "No description from TMDB."
        : _selectedSeries.Series.Overview!;

    public string SelectedFacts
    {
        get
        {
            if (_selectedSeries is null)
            {
                return "Select a cover to see the year and description.";
            }

            TvSeries series = _selectedSeries.Series;
            string when = series.FirstAirDate?.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
                ?? (series.FirstAirYear is int year ? year.ToString(CultureInfo.CurrentCulture) : "No first air date");
            return $"{when}  ·  TV series";
        }
    }

    public string TemplateName
    {
        get => _templateName;
        set
        {
            if (SetProperty(ref _templateName, value))
            {
                OnPropertyChanged(nameof(IsCustomTemplate));
                OnPropertyChanged(nameof(CurrentPattern));
                RebuildPlan();
                RememberPreferences();
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
                OnPropertyChanged(nameof(CurrentPattern));
                RebuildPlan();
                RememberPreferences();
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
                RememberPreferences();
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
                RefreshDestinations();
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

    private bool CanGoBack() => _step > 0;

    private bool CanGoNext() => !IsBusy && _step switch
    {
        0 => Files.Count > 0,
        1 => _episodesReady,
        2 => true,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Previous() => MoveTo(_step - 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => MoveTo(_step + 1);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void Navigate(string? step)
    {
        if (!TryStep(step, out int index) || index == _step || !CanNavigate(step))
        {
            return;
        }

        MoveTo(index);
    }

    private bool CanNavigate(string? step)
        => TryStep(step, out int index)
            && index is >= 0 and <= 3
            && (index <= _step || (index == _step + 1 && CanGoNext()));

    private static bool TryStep(string? step, out int index)
        => int.TryParse(step, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);

    private void MoveTo(int step)
    {
        if (step < 0 || step > 3 || step == _step)
        {
            return;
        }

        _step = step;
        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(IsFolderStep));
        OnPropertyChanged(nameof(IsSeriesStep));
        OnPropertyChanged(nameof(IsMatchStep));
        OnPropertyChanged(nameof(IsRenameStep));
        OnPropertyChanged(nameof(StepTitle));
        NotifyNavigation();
    }

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

        ClearSeriesResults();
        SelectSeries(null);
        foreach (var series in result.Value)
        {
            var choice = new SeriesChoice(series);
            SeriesResults.Add(choice);
            _ = LoadPosterAsync(choice, cancellationToken);
        }

        StatusMessage = result.Value.Count == 0
            ? "No series matched that search."
            : $"Found {result.Value.Count} series. Pick the cover that matches these files.";
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
        StatusMessage = "Settings saved in your user profile, outside this project.";
        RebuildPlan();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void AutoMap()
    {
        int added = AutoMapRemaining();
        RebuildPlan();
        StatusMessage = added == 0
            ? "No further files could be mapped from their names."
            : $"Mapped {added} file(s). {SourceFiles.Count} still unmapped.";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void RemoveMapping()
    {
        if (SelectedMapping is null)
        {
            StatusMessage = "Select a mapping to remove.";
            return;
        }

        MappingRow mapping = SelectedMapping;
        SelectedMapping = null;
        Mappings.Remove(mapping);
        InsertFile(mapping.Match.File);
        RefreshDestinations();
        RebuildPlan();
        StatusMessage = $"Removed {mapping.Source}.";
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

    public void ChooseSeries(SeriesChoice choice)
    {
        foreach (var item in SeriesResults)
        {
            item.IsSelected = ReferenceEquals(item, choice);
        }

        SelectSeries(choice);
    }

    public void SelectSeries(SeriesChoice? choice)
    {
        if (ReferenceEquals(_selectedSeries, choice))
        {
            return;
        }

        _selectedSeries = choice;
        _episodesReady = false;
        OnPropertyChanged(nameof(SelectedSeries));
        OnPropertyChanged(nameof(SelectedSeriesLabel));
        OnPropertyChanged(nameof(SelectedPoster));
        OnPropertyChanged(nameof(HasSeriesDetails));
        OnPropertyChanged(nameof(SelectedOverview));
        OnPropertyChanged(nameof(SelectedFacts));
        NotifyNavigation();
        if (choice is null)
        {
            ClearMatches();
            return;
        }

        _ = LoadSeriesAsync(choice, refresh: false);
    }

    public void AddMapping(VideoFile? file, IReadOnlyList<TvEpisode> episodes)
    {
        if (file is null)
        {
            StatusMessage = "Select a file on the left.";
            return;
        }

        if (episodes.Count == 0)
        {
            StatusMessage = "Select an episode on the right.";
            return;
        }

        ParsedFilename parsed = _parser.Parse(file.FileName);
        EpisodeMatch match = _matcher.Confirm(file, parsed, episodes);
        if (match.Status != MatchStatus.Confirmed)
        {
            StatusMessage = match.Explanation;
            return;
        }

        Commit(match);
        RefreshDestinations();
        RebuildPlan();
        StatusMessage = $"Mapped {file.FileName}.";
    }

    public void InsertToken(string token, int caret)
    {
        if (TemplateName != RenameTemplates.CustomName)
        {
            _customTemplate = RenameTemplates.PatternFor(TemplateName, CustomTemplate);
            OnPropertyChanged(nameof(CustomTemplate));
            TemplateName = RenameTemplates.CustomName;
        }

        string current = CustomTemplate ?? string.Empty;
        caret = Math.Clamp(caret, 0, current.Length);
        CustomTemplate = current.Insert(caret, token);
    }

    [RelayCommand]
    private void OpenSeriesOnTmdb()
    {
        if (_selectedSeries is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo($"https://www.themoviedb.org/tv/{_selectedSeries.Series.TmdbId}")
        {
            UseShellExecute = true,
        });
    }

    private Task LoadSeriesAsync(SeriesChoice choice, bool refresh) => RunAsync(async cancellationToken =>
    {
        _episodesReady = false;
        NotifyNavigation();
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
        cancellationToken.ThrowIfCancellationRequested();
        MatchFiles();
        _episodesReady = true;
        NotifyNavigation();
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
        string? guessed = SeriesNameGuess.FromFolderAndFiles(FolderPath, result.Value.Select(file => file.FullPath).ToList());
        if (!string.IsNullOrWhiteSpace(guessed))
        {
            SearchQuery = guessed;
        }
        StatusMessage = guessed is null
            ? FileSummary + " Choose Next to pick the series."
            : FileSummary + " Series search filled in.";
        if (_selectedSeries is null)
        {
            ClearMatches();
            return;
        }

        MatchFiles();
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
        Mappings.Clear();
        SourceFiles.Clear();
        foreach (VideoFile file in Files.OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase))
        {
            SourceFiles.Add(file);
        }

        _episodeChoices.Clear();
        _episodeChoices.AddRange(_catalog
            .OrderBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .Select(episode => new EpisodeChoice(episode)));
        RefreshDestinations();
        int added = AutoMapRemaining();
        RebuildPlan();
        StatusMessage = added == 0
            ? $"No files were mapped from their names. {SourceFiles.Count} are still on the left."
            : $"Mapped {added} file(s). {SourceFiles.Count} still unmapped.";
    }

    private int AutoMapRemaining()
    {
        var mappedIds = Mappings.SelectMany(mapping => mapping.Match.Episodes).Select(episode => episode.TmdbId).ToHashSet();
        var available = _episodeChoices
            .Select(choice => choice.Episode)
            .Where(episode => !mappedIds.Contains(episode.TmdbId))
            .ToList();
        var accepted = new List<EpisodeMatch>();
        foreach (VideoFile file in SourceFiles.ToList())
        {
            ParsedFilename parsed = _parser.Parse(file.FileName);
            EpisodeMatch match = _matcher.Match(file, parsed, available);
            if (match.Episodes.Count == 0)
            {
                continue;
            }

            foreach (TvEpisode episode in match.Episodes)
            {
                available.RemoveAll(candidate => candidate.TmdbId == episode.TmdbId);
            }

            accepted.Add(match with { Status = MatchStatus.Confirmed });
        }

        foreach (EpisodeMatch match in accepted)
        {
            Commit(match);
        }

        if (accepted.Count > 0)
        {
            RefreshDestinations();
        }

        return accepted.Count;
    }

    private void Commit(EpisodeMatch match)
    {
        SourceFiles.Remove(match.File);
        Mappings.Add(new MappingRow(match));
    }

    private void InsertFile(VideoFile file)
    {
        int index = 0;
        while (index < SourceFiles.Count
            && string.Compare(SourceFiles[index].FileName, file.FileName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }

        SourceFiles.Insert(index, file);
    }

    private void RebuildPlan()
    {
        if (_selectedSeries is null || Mappings.Count == 0)
        {
            Plan = null;
            PlanSummary = Mappings.Count == 0
                ? "Map a file to an episode before renaming."
                : "Select a series to match these files.";
            NotifyPlan();
            return;
        }

        Plan = _planner.CreatePlan(
            FolderPath,
            _selectedSeries.Series,
            Mappings.Select(mapping => mapping.Match).ToList(),
            CurrentTemplate(),
            IncludeEpisodeTitles);
        foreach (RenameOperation operation in Plan.Operations)
        {
            MappingRow? mapping = Mappings.FirstOrDefault(item => PathComparison.Equal(item.Match.File.FullPath, operation.Match.File.FullPath));
            mapping?.Apply(operation);
        }

        PlanSummary = SourceFiles.Count == 0
            ? $"{Plan.ReadyCount} ready to rename."
            : $"{Plan.ReadyCount} ready to rename. {SourceFiles.Count} file(s) are not mapped and will be left as they are.";
        if (Plan.InvalidCount > 0)
        {
            PlanSummary += $" {Plan.InvalidCount} mapping(s) need to be removed.";
        }

        NotifyPlan();
    }

    private void RefreshDestinations()
    {
        var mappedIds = Mappings.SelectMany(mapping => mapping.Match.Episodes).Select(episode => episode.TmdbId).ToHashSet();
        DestinationEpisodes.Clear();
        IEnumerable<EpisodeChoice> choices = _episodeChoices.Where(choice => !mappedIds.Contains(choice.Episode.TmdbId));
        if (!string.IsNullOrWhiteSpace(EpisodeFilter))
        {
            string term = EpisodeFilter.Trim();
            choices = choices.Where(choice => choice.Label.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (EpisodeChoice choice in choices)
        {
            DestinationEpisodes.Add(choice);
        }
    }

    private void ClearMatches()
    {
        _catalog.Clear();
        _episodeChoices.Clear();
        SourceFiles.Clear();
        DestinationEpisodes.Clear();
        Mappings.Clear();
        SelectedSourceFile = null;
        SelectedMapping = null;
        Plan = null;
        _episodesReady = false;
        PlanSummary = Files.Count == 0 ? "Scan a folder to begin." : "Select a series to match these files.";
        NotifyPlan();
        NotifyNavigation();
    }

    private void ClearSeriesResults()
    {
        foreach (var choice in SeriesResults)
        {
            choice.Poster?.Dispose();
            choice.Poster = null;
        }

        SeriesResults.Clear();
    }

    private async Task LoadPosterAsync(SeriesChoice choice, CancellationToken cancellationToken)
    {
        Bitmap? poster = await _posters.LoadAsync(choice.Series.PosterUrl, cancellationToken);
        if (poster is null || cancellationToken.IsCancellationRequested || !SeriesResults.Contains(choice))
        {
            poster?.Dispose();
            return;
        }

        Bitmap? previous = choice.Poster;
        choice.Poster = poster;
        previous?.Dispose();
        if (ReferenceEquals(choice, _selectedSeries))
        {
            OnPropertyChanged(nameof(SelectedPoster));
        }
    }

    private void RememberPreferences()
    {
        ApplyOptions();
        _saveDelay?.Cancel();
        var delay = new CancellationTokenSource();
        _saveDelay = delay;
        _ = SavePreferencesAsync(delay.Token);
    }

    private async Task SavePreferencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
            await _settings.SaveAsync(_options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer edit replaced this save.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Could not save your rename template.";
        }
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
        AutoMapCommand.NotifyCanExecuteChanged();
        RemoveMappingCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        NotifyNavigation();
    }

    private void NotifyNavigation()
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        NavigateCommand.NotifyCanExecuteChanged();
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
