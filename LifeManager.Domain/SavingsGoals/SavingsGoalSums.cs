namespace LifeManager.Domain.SavingsGoals
{
    /// <summary>A goal's linked investments, summed: all of them, and those dated from the pace window's first day.</summary>
    public sealed record SavingsGoalSums(decimal Total, decimal Recent)
    {
        public static readonly SavingsGoalSums Zero = new(0m, 0m);
    }
}
