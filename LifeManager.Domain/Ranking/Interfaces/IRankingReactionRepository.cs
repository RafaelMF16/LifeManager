using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Domain.Ranking.Interfaces
{
    public interface IRankingReactionRepository
    {
        Task<IReadOnlyList<RankingReaction>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken);

        /// <summary>
        /// Stores the reaction, or changes the kind of the pair's reaction that week. False when either member left the
        /// group meanwhile (their row is gone, so the foreign key refuses it).
        /// </summary>
        Task<bool> SetAsync(RankingReaction reaction, CancellationToken cancellationToken);

        /// <summary>Removes the pair's reaction that week, if any.</summary>
        Task RemoveAsync(RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId, CancellationToken cancellationToken);
    }
}
