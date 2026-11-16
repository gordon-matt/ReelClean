namespace ReelClean;

/// <summary>How reliable a filename-to-episode match is. Separate from whether the user has accepted it.</summary>
public enum MatchConfidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}
