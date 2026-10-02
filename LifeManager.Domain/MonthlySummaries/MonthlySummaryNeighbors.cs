namespace LifeManager.Domain.MonthlySummaries
{
    /// <summary>The user's closest months before and after a given one; null when there is none.</summary>
    public record MonthlySummaryNeighbors(int? PreviousId, int? NextId);
}
