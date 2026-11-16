namespace ReelClean.Services.Naming;

public interface IRenameTemplateFormatter
{
    /// <summary>
    /// Expands a template into a relative path. Forward slashes separate folders.
    /// Token values never contain path separators.
    /// </summary>
    string Format(
        string template,
        TvSeries series,
        IReadOnlyList<TvEpisode> episodes,
        string originalFileName,
        bool includeEpisodeTitles);
}
