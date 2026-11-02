using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RankingGroups.Mocks;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Application.Test.Ranking.Mocks
{
    /// <summary>Mirrors RankingReactionRepository over the singletons: the pair's upsert and the members' cascade.</summary>
    public class RankingReactionRepositoryMock : IRankingReactionRepository
    {
        private static readonly object Gate = new();

        static RankingReactionRepositoryMock()
        {
            // The member rows are gone by then: drop the reactions that pointed at them.
            RankingGroupRepositoryMock.MemberLeft += (_, _) => RemoveOrphans();
            RankingGroupRepositoryMock.GroupDeleted += _ => RemoveOrphans();
        }

        public Task<IReadOnlyList<RankingReaction>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            IReadOnlyList<RankingReaction> reactions = [.. RankingReactionSingleton.Instance
                .Where(reaction => reaction.GroupId == groupId && reaction.WeekStart == weekStart)
                .OrderBy(reaction => reaction.CreatedAt)
                .ThenBy(reaction => reaction.Id!.Value)];

            return Task.FromResult(reactions);
        }

        public Task<bool> SetAsync(RankingReaction reaction, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                if (!MemberExists(reaction.FromMemberId) || !MemberExists(reaction.ToMemberId))
                    return Task.FromResult(false);

                var stored = Find(reaction.GroupId, reaction.WeekStart, reaction.FromMemberId, reaction.ToMemberId);
                if (stored is not null)
                {
                    stored.ChangeKind(reaction.Kind, reaction.CreatedAt);
                    return Task.FromResult(true);
                }

                reaction.AssignId(RankingReactionSingleton.Instance.Select(existing => existing.Id!.Value).DefaultIfEmpty(0).Max() + 1);
                RankingReactionSingleton.Instance.Add(reaction);

                return Task.FromResult(true);
            }
        }

        public Task RemoveAsync(
            RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var stored = Find(groupId, weekStart, fromMemberId, toMemberId);
                if (stored is not null)
                    RankingReactionSingleton.Instance.Remove(stored);

                return Task.CompletedTask;
            }
        }

        private static RankingReaction? Find(RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId)
            => RankingReactionSingleton.Instance.SingleOrDefault(reaction => reaction.GroupId == groupId
                && reaction.WeekStart == weekStart
                && reaction.FromMemberId == fromMemberId
                && reaction.ToMemberId == toMemberId);

        private static bool MemberExists(RankingGroupMemberId memberId)
            => RankingGroupMemberSingleton.Instance.Any(member => member.Id == memberId);

        private static void RemoveOrphans()
            => RankingReactionSingleton.Instance.RemoveAll(reaction => !MemberExists(reaction.FromMemberId) || !MemberExists(reaction.ToMemberId));
    }
}
