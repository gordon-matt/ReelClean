namespace ReelClean;

/// <summary>Whether a planned rename can run.</summary>
public enum PlanStatus
{
    Ready = 0,
    NeedsReview = 1,
    Invalid = 2,
    Ignored = 3,
}
