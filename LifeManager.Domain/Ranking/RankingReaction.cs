using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.Ranking.ValueObjects;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Ranking
{
    /// <summary>
    /// A member's 👏 or 🔥 to another member's week in a group: one per pair, week and group (reacting again changes the
    /// kind). Kept by member, not by user, so leaving the group takes the reactions sent and received with it.
    /// </summary>
    public class RankingReaction
    {
        public RankingReactionId? Id { get; private set; }
        public RankingGroupId GroupId { get; }
        public DateOnly WeekStart { get; }
        public RankingGroupMemberId FromMemberId { get; }
        public RankingGroupMemberId ToMemberId { get; }
        public RankingReactionKind Kind { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        private RankingReaction(
            RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId,
            RankingReactionKind kind, DateTimeOffset createdAt)
        {
            GroupId = groupId;
            WeekStart = weekStart;
            FromMemberId = fromMemberId;
            ToMemberId = toMemberId;
            Kind = kind;
            CreatedAt = createdAt;
        }

        public static Result<RankingReaction> Create(
            RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId,
            RankingReactionKind kind, DateTimeOffset createdAt)
        {
            if (!Enum.IsDefined(kind))
                return RankingErrors.UnknownReaction;
            if (fromMemberId == toMemberId)
                return RankingErrors.CannotReactToSelf;

            return new RankingReaction(groupId, weekStart, fromMemberId, toMemberId, kind, createdAt);
        }

        /// <summary>Reactions are open on the current week and the one before (last week's podium card).</summary>
        public static bool IsInWindow(DateOnly weekStart, DateOnly today)
        {
            var currentWeek = GameWeek.Start(today);

            return weekStart == currentWeek || weekStart == currentWeek.AddDays(-GameWeek.Days);
        }

        /// <summary>Reacting again to the same week: only the kind (and when) changes.</summary>
        public void ChangeKind(RankingReactionKind kind, DateTimeOffset changedAt)
        {
            Kind = kind;
            CreatedAt = changedAt;
        }

        public void AssignId(int id)
        {
            Id = new RankingReactionId(id);
        }
    }
}
