# ReelClean --- Project Plan and Implementation Brief

**Product tagline:** Your media, properly named.\
**Project type:** Cross-platform desktop application for identifying and
renaming TV-series video files.\
**Initial platforms:** Windows, macOS, and Linux.\
**Primary metadata provider:** The Movie Database (TMDB).

------------------------------------------------------------------------

## 1. Product vision

ReelClean is a desktop application inspired by the classic FileBot
workflow. It scans a folder of TV episodes, identifies episodes from
their filenames, matches them against TMDB metadata, previews proposed
filenames, and safely renames files after user confirmation.

The first release should focus on **one TV series and one selected
folder at a time**. The architecture should allow future multi-series
scanning without requiring a rewrite.

### Core principles

1.  **Preview before changing files.** Scanning and matching must never
    rename or move files.
2.  **No silent guesses.** Ambiguous or low-confidence matches require
    user review.
3.  **Safe filesystem operations.** Detect collisions, revalidate before
    execution, log changes, and support best-effort rollback/undo.
4.  **Cross-platform from day one.** Avoid Windows-only APIs and
    assumptions.
5.  **Keep business logic independent of the UI.** The matching engine
    should be testable without Avalonia, TMDB, or real media files.
6.  **Keep the first release focused.** Do not over-engineer persistence
    or add unrelated features prematurely.

------------------------------------------------------------------------

## 2. Recommended technology stack

  Area                   Choice
  ---------------------- ------------------------------------------------
  Language               C#
  Runtime                .NET 10 LTS
  Desktop UI             Avalonia UI
  UI pattern             MVVM
  MVVM toolkit           CommunityToolkit.Mvvm
  Dependency injection   Microsoft.Extensions.DependencyInjection
  HTTP                   IHttpClientFactory / Microsoft.Extensions.Http
  JSON                   System.Text.Json
  Metadata               TMDB API
  Configuration          System.Text.Json
  Logging                Microsoft.Extensions.Logging
  Unit testing           xUnit
  Mocking                NSubstitute (optional)
  Initial persistence    JSON files and in-memory state

Use current compatible stable versions of packages. Keep package
versions centrally managed where practical, for example with
`Directory.Packages.props`.

Do not introduce Entity Framework, a database, a web server, or a
complex plugin architecture in the MVP.

------------------------------------------------------------------------

## 3. Solution structure

Start with three production projects and test projects:

``` text
ReelClean/
├── ReelClean.sln
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
├── docs/
│   └── architecture.md
├── src/
│   ├── ReelClean.App/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Services/
│   │   ├── Converters/
│   │   ├── Assets/
│   │   └── App.axaml
│   ├── ReelClean.Core/
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   ├── Parsing/
│   │   ├── Matching/
│   │   ├── Planning/
│   │   └── Services/
│   └── ReelClean.Infrastructure/
│       ├── Tmdb/
│       ├── FileSystem/
│       ├── Configuration/
│       └── Logging/
└── tests/
    ├── ReelClean.Core.Tests/
    └── ReelClean.Infrastructure.Tests/
```

### Dependency direction

-   `ReelClean.Core` has no dependency on Avalonia or Infrastructure.
-   `ReelClean.Infrastructure` references Core and implements Core
    interfaces.
-   `ReelClean.App` references Core and Infrastructure, and composes the
    application through dependency injection.
-   Test projects reference the projects they test.

Avoid circular dependencies. Do not put TMDB DTOs, Avalonia types, or
filesystem implementation details into Core.

------------------------------------------------------------------------

## 4. User workflow

The primary workflow is a guided sequence:

1.  **Select folder**
    -   Choose a folder containing video files.
    -   Recursive scanning is optional and off by default.
    -   Display discovered files and basic information.
2.  **Select series**
    -   Search TMDB.
    -   Display matching series, including title and first-air year.
    -   Require the user to select the intended series.
3.  **Load episode metadata**
    -   Retrieve available season numbers.
    -   Retrieve episode lists for the relevant seasons.
    -   Cache metadata locally.
4.  **Match episodes**
    -   Parse filenames.
    -   Match season and episode numbers to TMDB episodes.
    -   Show confidence and match status.
    -   Allow manual corrections and unmatched files to be ignored.
5.  **Preview rename plan**
    -   Apply the selected naming template.
    -   Show original and proposed paths.
    -   Validate invalid names, duplicate destinations, and existing
        destination files.
    -   Do not execute if there are unresolved validation errors.
6.  **Execute**
    -   Ask for confirmation.
    -   Revalidate the plan immediately before execution.
    -   Perform safe batch renames.
    -   Report successes and failures.
    -   Save an operation manifest for possible undo.

------------------------------------------------------------------------

## 5. Core domain models

Use immutable records where appropriate. Adjust naming and nullability
as implementation details become clearer, but keep domain models
independent of external API DTOs.

### VideoFile

Represents a discovered local video file.

Suggested fields:

-   `FullPath`
-   `FileName`
-   `Extension`
-   `FileSize`

### TvSeries

Represents a selected series.

Suggested fields:

-   `TmdbId`
-   `Name`
-   `FirstAirYear`

### TvEpisode

Represents an episode retrieved from the metadata provider.

Suggested fields:

-   `TmdbId`
-   `SeasonNumber`
-   `EpisodeNumber`
-   `Name`
-   `AirDate`

Use `DateOnly?` for an optional air date if supported by the chosen
target framework.

### ParsedFilename

Represents information extracted from a filename.

Suggested fields:

-   `OriginalFileName`
-   `SeasonNumber` (nullable)
-   `EpisodeNumbers` (collection, to support multi-episode files)
-   `DetectedEpisodeTitle` (nullable)
-   Parsing diagnostics or pattern identifier

Do not make parsing confidence and matching confidence the same concept.
Parsing describes how reliably the filename was interpreted; matching
describes how reliably that interpretation maps to metadata.

### EpisodeMatchResult

Represents the outcome of matching a parsed filename to metadata.

Suggested fields:

-   Matched episode collection
-   Confidence
-   Match status
-   Explanation/diagnostic
-   Whether user confirmation is required

### RenameOperation

Represents one proposed file operation.

Suggested fields:

-   `SourcePath`
-   `DestinationPath`
-   Matched episode collection
-   Confidence
-   Validation status
-   Validation errors

### RenamePlan

Contains all proposed operations and plan-level validation results. The
plan should be a snapshot of the intended work, not an instruction to
immediately mutate the filesystem.

### RenameExecutionResult

Contains successful and failed operations, error details, and an
execution/manifest identifier.

------------------------------------------------------------------------

## 6. Core interfaces

Define abstractions in Core and implement them in Infrastructure.

Illustrative starting contracts:

``` csharp
public interface IFileScanner
{
    Task<IReadOnlyList<VideoFile>> ScanAsync(
        string folderPath,
        CancellationToken cancellationToken = default);
}

public interface ITvMetadataProvider
{
    Task<IReadOnlyList<TvSeries>> SearchSeriesAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetSeasonNumbersAsync(
        int seriesId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TvEpisode>> GetSeasonEpisodesAsync(
        int seriesId,
        int seasonNumber,
        CancellationToken cancellationToken = default);
}

public interface IFilenameParser
{
    ParsedFilename Parse(string filename);
}

public interface IEpisodeMatcher
{
    EpisodeMatchResult Match(
        ParsedFilename parsedFilename,
        IReadOnlyList<TvEpisode> episodes);
}

public interface IRenamePlanner
{
    RenamePlan CreatePlan(
        IReadOnlyList<VideoFile> files,
        IReadOnlyList<EpisodeMatchResult> matches,
        RenameTemplate template);
}

public interface IRenameExecutor
{
    Task<RenameExecutionResult> ExecuteAsync(
        RenamePlan plan,
        CancellationToken cancellationToken = default);
}
```

These are initial design examples, not a requirement to force every
implementation into exactly these signatures. Refine contracts as the
domain model becomes concrete.

Additional useful abstractions:

-   `IFilenameSanitizer`
-   `IRenameTemplateFormatter`
-   `ISettingsStore`
-   `IMetadataCache`

------------------------------------------------------------------------

## 7. Filename parsing and episode matching

This is a core feature and should be developed and tested before
substantial UI work.

### Initial filename formats

Support these common patterns in the MVP:

  Format                    Example
  ------------------------- -----------------------
  Standard season/episode   `Show.S01E02.mkv`
  Repeated episode marker   `Show.S01E02E03.mkv`
  Episode range             `Show.S01E02-E04.mkv`
  Alternate x notation      `Show.1x02.mkv`
  Compact numbering         `Show.102.mkv`
  Episode number only       `Show.02.mkv`

Implement separate parsing strategies rather than one enormous regular
expression. For example:

-   `StandardSeasonEpisodePattern`
-   `AlternateSeasonEpisodePattern`
-   `CompactEpisodePattern`
-   `EpisodeNumberOnlyPattern`

Try more explicit patterns before ambiguous patterns.

### Important parsing rules

-   Strip or ignore common release information such as resolution,
    codecs, source, and release groups where appropriate.
-   Do not mistake years, resolutions, or unrelated numbers for episode
    numbers.
-   Treat compact numbering as ambiguous unless there is enough context.
-   Do not automatically assign episodes based on alphabetical file
    order.
-   Support multiple episodes in a single file.
-   Preserve the original filename and path for display and recovery.

### Confidence and user review

Suggested initial behaviour:

  Match type                                             Suggested status
  ------------------------------------------------------ ------------------
  Explicit `SxxExx` and exact metadata match             High
  Explicit multi-episode range with all episodes found   High
  Compact numbering                                      Medium or lower
  Episode-title-only match                               Low
  No reliable interpretation                             Unmatched

Confidence should be explainable. The UI should be able to show why a
match was suggested.

High confidence can be preselected, but the user must still review the
rename preview before execution. Medium and low confidence matches
should be visibly flagged for manual review.

### Manual correction

Users should be able to:

-   Select an episode from a searchable episode list.
-   Change the season/episode assignment.
-   Clear a match.
-   Ignore a file.
-   Match multiple selected files where useful.

------------------------------------------------------------------------

## 8. TMDB integration

Create a dedicated TMDB client in Infrastructure. Use `HttpClient` via
`IHttpClientFactory` and `System.Text.Json`.

Suggested structure:

``` text
ReelClean.Infrastructure/
└── Tmdb/
    ├── TmdbClient.cs
    ├── TmdbOptions.cs
    ├── Dtos/
    │   ├── TmdbSeriesDto.cs
    │   ├── TmdbSeasonDto.cs
    │   └── TmdbEpisodeDto.cs
    └── Mapping/
        └── TmdbMappingExtensions.cs
```

Useful API operations:

-   Search TV series: `/search/tv`
-   Series details: `/tv/{series_id}`
-   Season details: `/tv/{series_id}/season/{season_number}`
-   Episode details if needed:
    `/tv/{series_id}/season/{season_number}/episode/{episode_number}`

Official documentation: https://developer.themoviedb.org/docs

### TMDB implementation requirements

-   Keep TMDB DTOs inside Infrastructure.
-   Map API DTOs into Core domain models.
-   Handle timeouts, cancellation, non-success responses, and malformed
    responses.
-   Respect API rate limits and avoid unnecessary calls.
-   Cache series and season metadata as JSON initially.
-   Provide a way to refresh cached metadata.
-   Keep credentials out of source control.
-   Read the current TMDB authentication documentation and use an
    appropriate supported authentication method.
-   Review TMDB attribution and branding requirements before public
    distribution.

Do not assume every season has conventional numbering. Account for
specials (often season 0) and missing/unknown air dates.

------------------------------------------------------------------------

## 9. Rename templates

Introduce a `RenameTemplate` model and a formatter service. Do not
hardcode the final filename format in the UI or matching engine.

Suggested default:

``` text
{SeriesName} - S{Season:00}E{Episode:00} - {EpisodeName}
```

Suggested tokens:

-   `{SeriesName}`
-   `{Year}`
-   `{Season}`
-   `{Episode}`
-   `{EpisodeName}`
-   `{AirDate}`
-   `{OriginalFilename}`
-   `{Extension}`

Initial presets:

-   Plex-style
-   Jellyfin-style
-   Kodi-style
-   Minimal
-   Custom

Example output:

``` text
The Office (US) - S03E04 - Grief Counseling.mkv
```

For a multi-episode file, support a predictable format such as:

``` text
The Office (US) - S01E01-E02 - Pilot + The First Day.mkv
```

Keep multi-episode formatting configurable if practical, but avoid
building a complex template language in the MVP.

Support optional season folders later, for example:

``` text
The Office (US)/
└── Season 03/
    └── The Office (US) - S03E04 - Grief Counseling.mkv
```

------------------------------------------------------------------------

## 10. Rename planning and filesystem safety

Treat planning and execution as separate operations.

### Planning stage: no filesystem changes

The planner should:

1.  Take the discovered files and their match results.
2.  Apply the selected template.
3.  Generate destination paths.
4.  Sanitize names for cross-platform compatibility.
5.  Detect duplicate destination paths within the plan.
6.  Detect existing destination files.
7.  Detect invalid or unsupported operations.
8.  Return a complete preview with validation messages.

The UI should distinguish between:

-   Ready
-   Needs review
-   Invalid
-   Ignored

Do not enable execution while unresolved validation errors remain.

### Execution stage

Before changing files:

1.  Revalidate that source files still exist.
2.  Revalidate destination collisions.
3.  Check that the plan has not become stale.
4.  Check cancellation before starting and between safe operation
    boundaries.

For batch renames, use unique temporary filenames in the relevant
directories to avoid name swaps and overlapping destinations. Then move
temporary files to final destinations.

Record each successful step in an execution manifest.

If an operation fails, attempt best-effort rollback. Do not claim
filesystem operations are fully transactional: permissions, locked
files, network shares, disk errors, or process termination can interrupt
a batch.

### Undo

Store enough information to reverse successful operations:

-   Original path
-   New path
-   Execution timestamp
-   Success/failure state

Before undoing, validate that the new file still exists and that the
original destination is available. Never overwrite an unrelated file
during undo.

### Additional safety requirements

-   Never overwrite by default.
-   Never delete source files as part of renaming.
-   Never rename during scanning or matching.
-   Do not follow symbolic links recursively without an explicit,
    considered policy.
-   Handle read-only files and inaccessible directories gracefully.
-   Log errors in a way users can understand.

------------------------------------------------------------------------

## 11. Cross-platform filesystem behaviour

Use `System.IO` and platform-neutral APIs. Avoid hardcoded Windows paths
and assumptions.

Use the appropriate OS application-data locations for settings, cache,
logs, and history. Do not hardcode `C:\Users\...`, `/home/...`, or
similar paths.

Create an `IFilenameSanitizer` implementation that uses a conservative
cross-platform set of invalid characters. Consider:

-   Windows reserved characters and reserved device names.
-   Trailing spaces and periods on Windows.
-   Unicode filenames and normalization differences.
-   Case-sensitive versus case-insensitive filesystems.
-   Maximum path/name lengths.
-   Network shares and removable drives.

Do not assume that the filesystem's case sensitivity can be inferred
solely from the operating system. Validate actual destination conflicts
as safely as possible.

Initial application settings:

  Setting                                         Default
  ----------------------------------------------- ---------
  Scan subdirectories                             Off
  Include hidden files                            Off
  Rename subtitle files                           Off
  Preserve original extension                     On
  Include episode titles                          On
  Automatically suggest high-confidence matches   On
  Require preview and confirmation                On

------------------------------------------------------------------------

## 12. Avalonia UI

Use a desktop-oriented UI with a guided workflow. Keep ViewModels
focused on orchestration and presentation state; put business logic in
Core services.

### Screen 1: Folder selection and series selection

-   Folder picker.
-   Optional recursive scanning setting.
-   Scan button and progress indicator.
-   List of discovered video files.
-   TMDB series search.
-   Search results showing series title and year.
-   Explicit series selection.

Do not silently infer and select a series from the folder name. It can
be offered as a suggestion later.

### Screen 2: Episode matching

Use a DataGrid or equivalent table with columns:

-   Status
-   Original filename
-   Season
-   Episode
-   Episode title
-   Confidence
-   Proposed filename

Allow manual episode selection, clearing matches, ignoring files, and
filtering to unmatched or low-confidence items.

### Screen 3: Rename preview

Show original path/name and proposed destination path/name side by side.

Include:

-   Validation status and clear error messages.
-   Collision warnings.
-   Template selection/editing.
-   A summary of ready, review, invalid, and ignored files.
-   Execute button disabled when the plan is invalid.

After execution, show successful and failed operations and offer undo
where safe.

### MVVM

Use CommunityToolkit.Mvvm for observable properties and commands. Use
cancellation tokens for long-running scans and API requests. Ensure
UI-bound collections are updated on the appropriate UI thread.

Use Avalonia's supported folder picker APIs rather than
platform-specific dialogs.

------------------------------------------------------------------------

## 13. Dependency injection and configuration

Use Microsoft.Extensions.DependencyInjection and
Microsoft.Extensions.Logging. Register infrastructure implementations at
application startup.

A conceptual registration might look like:

``` csharp
services.AddLogging();

services.AddHttpClient<ITvMetadataProvider, TmdbClient>();

services.AddSingleton<IFileScanner, FileScanner>();
services.AddSingleton<IFilenameParser, FilenameParser>();
services.AddSingleton<IEpisodeMatcher, EpisodeMatcher>();
services.AddSingleton<IRenamePlanner, RenamePlanner>();
services.AddSingleton<IRenameExecutor, RenameExecutor>();
services.AddSingleton<IFilenameSanitizer, FilenameSanitizer>();
```

Register ViewModels with appropriate lifetimes. Avoid turning every
class into a singleton by default.

Use options/configuration for TMDB settings and application preferences.
Store secrets securely where feasible for the target platform; never
commit personal API credentials.

------------------------------------------------------------------------

## 14. Testing strategy

Testing is especially important for parsing, matching, planning, and
filesystem execution.

### Core unit tests

Filename parser test cases should include:

``` text
Show.S01E01.mkv
Show.S01E01E02.mkv
Show.S01E01-E03.mkv
Show.1x01.mkv
Show.101.mkv
Show.Pilot.mkv
Show.1080p.mkv
Show.2024.mkv
```

Test valid and invalid cases, ambiguous numbers, release metadata,
multi-episode files, and unusual punctuation.

Matching tests should cover:

-   Valid season/episode.
-   Missing season or episode.
-   Specials/season 0.
-   Multiple episodes in one file.
-   Duplicate episode assignments.
-   Ambiguous filenames.
-   Missing episode titles.
-   Manual overrides.

Planning tests should cover:

-   Correct template expansion.
-   Filename sanitization.
-   Duplicate destinations.
-   Existing destination files.
-   Invalid paths.
-   Multi-episode naming.

### Filesystem integration tests

Use temporary directories and disposable test files. Cover:

-   Successful rename.
-   Existing destination.
-   Duplicate destinations.
-   Two files swapping names.
-   Source file disappearing after planning.
-   Read-only or locked files where testable.
-   Failure partway through a batch.
-   Best-effort rollback.
-   Case-only renames on relevant filesystems.

Keep a regression corpus of real-world filenames. Every fixed parsing
bug should gain a test.

------------------------------------------------------------------------

## 15. Development roadmap

Implement in this order:

### Phase 1 --- Foundation

-   Create solution and projects.
-   Configure project references and dependency injection.
-   Add domain models and interfaces.
-   Set up xUnit.
-   Establish formatting, nullable reference types, and analyzers.

**Deliverable:** A compiling solution with a clean Core project and test
setup.

### Phase 2 --- Filename parsing

-   Implement explicit filename parsing strategies.
-   Support common season/episode formats.
-   Support multi-episode files.
-   Add confidence/diagnostic information.
-   Build parser regression tests.

**Deliverable:** A parser that converts filenames into structured
results.

### Phase 3 --- TMDB integration

-   Implement series search.
-   Retrieve season numbers and episode lists.
-   Map DTOs into domain models.
-   Add error handling and cancellation.
-   Add JSON metadata caching.

**Deliverable:** Select a series and retrieve its episode metadata.

### Phase 4 --- Matching engine

-   Match parsed filenames to episode metadata.
-   Produce confidence and explanations.
-   Handle unmatched and ambiguous files.
-   Support multi-episode matches and manual overrides.
-   Add comprehensive tests.

**Deliverable:** A collection of proposed episode matches independent of
the UI.

### Phase 5 --- Rename planning

-   Implement naming templates.
-   Generate destination paths.
-   Sanitize filenames.
-   Detect collisions and invalid operations.
-   Produce dry-run plans.

**Deliverable:** A complete, validated rename preview without changing
files.

### Phase 6 --- Avalonia UI

-   Build folder selection.
-   Build TMDB series search.
-   Build matching DataGrid.
-   Add manual corrections and filters.
-   Build rename preview and settings.

**Deliverable:** A usable application that prepares rename plans.

### Phase 7 --- Execution and recovery

-   Implement safe batch renaming.
-   Add temporary-name handling for swaps.
-   Add operation manifests and logging.
-   Implement best-effort rollback and safe undo.
-   Test failure scenarios.

**Deliverable:** End-to-end renaming with safeguards.

### Phase 8 --- Cross-platform release

-   Test on Windows, macOS, and Linux.
-   Test filesystem edge cases on each platform.
-   Add application icons and metadata.
-   Create platform-specific packages/installers.
-   Document installation and usage.

**Deliverable:** First distributable release.

------------------------------------------------------------------------

## 16. Explicitly out of scope for the MVP

Do not implement these unless the core workflow is stable:

-   Movie identification and renaming.
-   Anime-specific numbering and matching.
-   Subtitle renaming.
-   MediaInfo integration.
-   Multiple metadata providers.
-   Multi-series parent-folder scanning.
-   Automatic folder organisation.
-   Advanced custom regex rules.
-   Database-backed history.
-   Command-line interface.

Keep the architecture extensible enough to support these later, but do
not build speculative abstractions for them now.

------------------------------------------------------------------------

## 17. Future-proofing decisions

A few choices should make later expansion straightforward:

-   Keep `ITvMetadataProvider` as an abstraction so another provider can
    be added.
-   Keep parsing strategies modular so new naming conventions can be
    introduced.
-   Represent a file's matched episodes as a collection, not a single
    episode.
-   Keep the Core engine independent of Avalonia so a CLI or alternative
    UI could be added.
-   Keep scanning separate from series identification so multi-series
    scanning can be added later.
-   Keep rename planning separate from execution.
-   Keep settings and cache behind interfaces if their implementations
    may change.

Do not build a plugin system or general-purpose media framework in the
MVP.

------------------------------------------------------------------------

## 18. Instructions for the coding assistant

When implementing this plan:

1.  First inspect the existing repository and its current state. Do not
    overwrite existing work.
2.  Confirm the installed .NET SDK and use a compatible Avalonia
    template/package version.
3.  Implement incrementally, in the roadmap order.
4.  Before adding a package, check whether the framework already
    provides the required functionality.
5.  Prefer small, cohesive classes and clear interfaces over generic
    frameworks.
6.  Enable nullable reference types and use async APIs with cancellation
    tokens for I/O.
7.  Add tests alongside each Core feature, not only at the end.
8.  Do not hardcode TMDB credentials or personal secrets.
9.  Do not implement destructive filesystem behaviour without tests and
    explicit validation.
10. Keep the application buildable after each phase.
11. Explain significant architectural decisions and update the README as
    the project evolves.
12. If a requirement is ambiguous, choose the simplest safe behaviour
    and document the decision rather than silently guessing.

### First implementation task

Start with **Phase 1 --- Foundation** only:

-   Inspect the repository and available .NET SDK.
-   Create the solution and project structure if it does not already
    exist.
-   Configure references and nullable reference types.
-   Add the initial Core domain models and interface placeholders.
-   Add the Core test project.
-   Ensure `dotnet build` and `dotnet test` succeed.

Do not attempt to implement the entire application in one pass. Once
Phase 1 is complete, summarize the files created, decisions made,
commands run, and build/test results before proceeding to Phase 2.
