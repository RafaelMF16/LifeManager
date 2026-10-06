using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Budgets
{
    /// <summary>What to store so a goal's versions reflect a change: versions to delete, to rewrite and to add.</summary>
    public record BudgetTimelineChange(IReadOnlyList<BudgetId> DeletedIds, IReadOnlyList<Budget> Updated, Budget? Added);

    /// <summary>
    /// The versions of one goal (same user, type and category) never overlap, so each month has at most one goal.
    /// A change applies "from this month on": later versions are replaced and earlier months keep the goal they had.
    /// </summary>
    public static class BudgetTimeline
    {
        /// <summary>Makes <paramref name="newVersion"/>'s amount the goal from its first month on.</summary>
        /// <param name="versions">Every stored version of the same goal as <paramref name="newVersion"/>.</param>
        public static BudgetTimelineChange SetFrom(IReadOnlyList<Budget> versions, Budget newVersion)
        {
            var from = newVersion.FromMonth;
            var deletedIds = DeleteFrom(versions, from.AddMonths(1));

            var startingThen = versions.SingleOrDefault(version => version.FromMonth.Equals(from));
            if (startingThen is not null)
            {
                startingThen.ChangeAmount(newVersion.Amount);
                startingThen.EndAt(null);
                return new BudgetTimelineChange(deletedIds, [startingThen], null);
            }

            var covering = Covering(versions, from);
            if (covering is null)
                return new BudgetTimelineChange(deletedIds, [], newVersion);

            // Same amount as the goal already in force: keep that version going instead of splitting it.
            if (covering.Amount.Equals(newVersion.Amount))
            {
                covering.EndAt(null);
                return new BudgetTimelineChange(deletedIds, [covering], null);
            }

            covering.EndAt(from.AddMonths(-1));
            return new BudgetTimelineChange(deletedIds, [covering], newVersion);
        }

        /// <summary>Removes the goal from <paramref name="from"/> on; earlier months keep it.</summary>
        /// <returns><see cref="BudgetErrors.NotFound"/> when there is no goal in or after that month.</returns>
        public static Result<BudgetTimelineChange> RemoveFrom(IReadOnlyList<Budget> versions, YearMonth from)
        {
            var deletedIds = DeleteFrom(versions, from);

            var covering = Covering(versions, from);
            covering?.EndAt(from.AddMonths(-1));

            if (deletedIds.Count == 0 && covering is null)
                return BudgetErrors.NotFound;

            return new BudgetTimelineChange(deletedIds, covering is null ? [] : [covering], null);
        }

        /// <summary>The version in force in <paramref name="month"/>, if any.</summary>
        public static Budget? GoalIn(IEnumerable<Budget> versions, YearMonth month)
            => versions.FirstOrDefault(version => version.Covers(month));

        private static IReadOnlyList<BudgetId> DeleteFrom(IReadOnlyList<Budget> versions, YearMonth month)
            => [.. versions.Where(version => version.FromMonth.Ordinal >= month.Ordinal).Select(version => version.Id!)];

        /// <summary>The version that started before <paramref name="month"/> and is still in force then.</summary>
        private static Budget? Covering(IReadOnlyList<Budget> versions, YearMonth month)
            => versions.SingleOrDefault(version => version.FromMonth.Ordinal < month.Ordinal && version.Covers(month));
    }
}
