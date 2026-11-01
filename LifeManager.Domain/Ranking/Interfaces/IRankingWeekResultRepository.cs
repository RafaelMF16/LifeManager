using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Ranking.Interfaces
{
    public interface IRankingWeekResultRepository
    {
        /// <summary>The week's stored lines, best first; empty when the week isn't closed.</summary>
        Task<IReadOnlyList<RankingWeekResultView>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken);

        /// <summary>The Monday of the group's latest closed week, or null before its first close.</summary>
        Task<DateOnly?> GetLatestClosedWeekAsync(RankingGroupId groupId, CancellationToken cancellationToken);

        /// <summary>
        /// Groups that may have weeks to close up to <paramref name="lastSettledWeekStart"/>: created by its Sunday and
        /// with no result for it yet.
        /// </summary>
        Task<IReadOnlyList<RankingGroupId>> GetGroupsToCloseAsync(DateOnly lastSettledWeekStart, CancellationToken cancellationToken);

        /// <summary>Whether the day close judged <paramref name="day"/> for every active habit of <paramref name="userIds"/>.</summary>
        Task<bool> AreDaysClosedAsync(IReadOnlyCollection<UserId> userIds, DateOnly day, CancellationToken cancellationToken);

        /// <summary>
        /// Stores a week's lines at once. False when that week was already stored (another run got there first): the
        /// unique (group, week, user) index refuses the whole batch.
        /// </summary>
        Task<bool> AddWeekAsync(IReadOnlyList<RankingWeekResult> results, CancellationToken cancellationToken);
    }
}
