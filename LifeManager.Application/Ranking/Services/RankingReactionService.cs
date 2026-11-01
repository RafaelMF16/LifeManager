using LifeManager.Application.Ranking.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Ranking.Services
{
    /// <summary>👏/🔥 to another member's week, on the current week and the one before. Both have to be in the group now.</summary>
    public class RankingReactionService(
        IRankingReactionRepository rankingReactionRepository,
        IRankingGroupRepository rankingGroupRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IRankingReactionRepository _rankingReactionRepository = rankingReactionRepository;
        private readonly IRankingGroupRepository _rankingGroupRepository = rankingGroupRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>Reacts to <paramref name="memberId"/>'s week, or changes the kind of the user's reaction to it.</summary>
        public async Task<Result<RankingReactionsDto>> SetAsync(
            int groupId, int memberId, RankingReactionRequestDto request, UserId userId, CancellationToken cancellationToken)
        {
            var target = await ResolveAsync(groupId, memberId, request.WeekStart, userId, cancellationToken);
            if (!target.IsSuccess)
                return target.Error!;

            var (me, to, weekStart) = target.Value!;
            var reaction = RankingReaction.Create(new RankingGroupId(groupId), weekStart, me, to, request.Kind, _timeProvider.GetUtcNow());
            if (!reaction.IsSuccess)
                return reaction.Error!;

            // One of the two left the group meanwhile.
            if (!await _rankingReactionRepository.SetAsync(reaction.Value!, cancellationToken))
                return RankingErrors.MemberNotFound;

            return await ReactionsOfAsync(groupId, weekStart, to, me, cancellationToken);
        }

        public async Task<Result<RankingReactionsDto>> RemoveAsync(
            int groupId, int memberId, DateOnly? weekStart, UserId userId, CancellationToken cancellationToken)
        {
            var target = await ResolveAsync(groupId, memberId, weekStart, userId, cancellationToken);
            if (!target.IsSuccess)
                return target.Error!;

            var (me, to, week) = target.Value!;
            await _rankingReactionRepository.RemoveAsync(new RankingGroupId(groupId), week, me, to, cancellationToken);

            return await ReactionsOfAsync(groupId, week, to, me, cancellationToken);
        }

        /// <summary>The user's and the target's member ids, after every check shared by reacting and undoing.</summary>
        private async Task<Result<(RankingGroupMemberId Me, RankingGroupMemberId To, DateOnly WeekStart)>> ResolveAsync(
            int groupId, int memberId, DateOnly? weekStart, UserId userId, CancellationToken cancellationToken)
        {
            var group = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (group is null)
                return RankingGroupErrors.NotFound;

            if (weekStart is not { } week || !GameWeek.IsWeekStart(week))
                return RankingErrors.InvalidWeekStart;
            if (!RankingReaction.IsInWindow(week, _appClock.Today()))
                return RankingErrors.ReactionOutOfWindow;

            var me = group.Members.Single(member => member.UserId == userId);
            var to = group.Members.SingleOrDefault(member => member.MemberId.Value == memberId);
            if (to is null)
                return RankingErrors.MemberNotFound;
            if (to.MemberId == me.MemberId)
                return RankingErrors.CannotReactToSelf;

            return (me.MemberId, to.MemberId, week);
        }

        private async Task<RankingReactionsDto> ReactionsOfAsync(
            int groupId, DateOnly weekStart, RankingGroupMemberId to, RankingGroupMemberId me, CancellationToken cancellationToken)
        {
            var reactions = await _rankingReactionRepository.GetWeekAsync(new RankingGroupId(groupId), weekStart, cancellationToken);

            return RankingReactionsDto.For(reactions, to, me);
        }
    }
}
