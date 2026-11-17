using ReelClean.Services.Naming;

namespace ReelClean.Services;

public sealed class ReelCleanOptions
{
    public TmdbOptions Tmdb { get; set; } = new();

    public bool ScanSubdirectories { get; set; }

    public bool IncludeHiddenFiles { get; set; }

    public bool IncludeEpisodeTitles { get; set; } = true;

    public bool AlwaysAutoMap { get; set; }

    public string TemplateName { get; set; } = RenameTemplates.PlexName;

    public string CustomTemplate { get; set; } = string.Empty;
}

/// <summary>TMDB v3 key. Create one at themoviedb.org. The same key ReelSoon stores under Catalog:Tmdb:ApiKey.</summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string ApiKey { get; set; } = string.Empty;
}
