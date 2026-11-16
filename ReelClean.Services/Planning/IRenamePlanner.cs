namespace ReelClean.Services.Planning;

public interface IRenamePlanner
{
    RenamePlan CreatePlan(
        string folderPath,
        TvSeries series,
        IReadOnlyList<EpisodeMatch> matches,
        string template,
        bool includeEpisodeTitles);

    /// <summary>Checks that a previously built plan is still safe to run. Does not change files.</summary>
    Result<RenamePlan> Revalidate(RenamePlan plan);
}
