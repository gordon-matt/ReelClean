namespace ReelClean;

/// <summary>What the user has decided about a file, distinct from <see cref="MatchConfidence"/>.</summary>
public enum MatchStatus
{
    Unmatched = 0,
    Suggested = 1,
    Confirmed = 2,
    Ignored = 3,
}
