namespace ReelClean.Services.Matching;

public interface IEpisodeMatcher
{
    EpisodeMatch Match(VideoFile file, ParsedFilename parsed, IReadOnlyList<TvEpisode> episodes);

    EpisodeMatch Confirm(VideoFile file, ParsedFilename parsed, IReadOnlyList<TvEpisode> chosen);

    EpisodeMatch Ignore(VideoFile file, ParsedFilename parsed);

    EpisodeMatch Clear(VideoFile file, ParsedFilename parsed);
}
