using LifeManager.Application.Ranking.DTOs;
using LifeManager.Application.Ranking.Services;
using LifeManager.Application.RankingGroups.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RankingGroups;
using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Ranking
{
    [Collection("ApplicationServices")]
    public class RankingReactionServiceTest : BaseTest
    {
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        private static readonly UserId Outsider = new(4);
        private static readonly DateOnly CreatedOn = new(2026, 10, 28);
        private static readonly DateOnly Monday = new(2026, 11, 9);
        private static readonly DateOnly Today = Monday.AddDays(2);
        private const int GroupId = 1;

        private readonly RankingReactionService _service;
        private readonly GroupRankingService _rankingService;

        public RankingReactionServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<RankingReactionService>();
            _rankingService = ServiceProvider.GetRequiredService<GroupRankingService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            RankingGroupSingleton.Instance.Clear();
            RankingGroupMemberSingleton.Instance.Clear();
            RankingWeekResultSingleton.Instance.Clear();
            RankingReactionSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            RankingGroupServiceTest.SeedUsers(5);

            RankingGroupSingleton.Instance.Add(RankingGroup.FromPersistence(
                GroupId, "Família", Ana.Value, "ABCDEFGH", CreatedOn.ToDateTime(TimeOnly.MinValue), CreatedOn));
            foreach (var userId in new[] { Ana, Bia, Caio })
                RankingGroupMemberSingleton.Instance.Add(RankingGroupMember.FromPersistence(
                    userId.Value, GroupId, userId.Value, CreatedOn.ToDateTime(TimeOnly.MinValue), CreatedOn));
        }

        private static int MemberOf(UserId userId) => userId.Value;

        private Task<LifeManager.Domain.Shared.Results.Result<RankingReactionsDto>> ReactAsync(UserId from, UserId to, RankingReactionKind kind, DateOnly? week = null)
            => _service.SetAsync(GroupId, MemberOf(to), new RankingReactionRequestDto(week ?? Monday, kind), from, CancellationToken.None);

        [Fact]
        public async Task SetAsync_ShouldReact_AndChangeTheKindOnASecondTap()
        {
            var first = await ReactAsync(Ana, Bia, RankingReactionKind.Clap);
            await ReactAsync(Caio, Bia, RankingReactionKind.Fire);
            var second = await ReactAsync(Ana, Bia, RankingReactionKind.Fire);

            Assert.Equal(new RankingReactionsDto(1, 0, RankingReactionKind.Clap), first.Value);
            Assert.Equal(new RankingReactionsDto(0, 2, RankingReactionKind.Fire), second.Value);
            Assert.Equal(2, RankingReactionSingleton.Instance.Count);
        }

        [Fact]
        public async Task RemoveAsync_ShouldRemoveMyReaction_AndBeIdempotent()
        {
            await ReactAsync(Ana, Bia, RankingReactionKind.Clap);
            await ReactAsync(Caio, Bia, RankingReactionKind.Clap);

            var removed = await _service.RemoveAsync(GroupId, MemberOf(Bia), Monday, Ana, CancellationToken.None);
            var again = await _service.RemoveAsync(GroupId, MemberOf(Bia), Monday, Ana, CancellationToken.None);

            Assert.Equal(new RankingReactionsDto(1, 0, null), removed.Value);
            Assert.True(again.IsSuccess);
        }

        [Fact]
        public async Task SetAsync_ShouldAcceptLastWeek_AndRefuseOlderOrFutureWeeks()
        {
            Assert.True((await ReactAsync(Ana, Bia, RankingReactionKind.Clap, Monday.AddDays(-7))).IsSuccess);
            Assert.Equal(RankingErrors.ReactionOutOfWindow, (await ReactAsync(Ana, Bia, RankingReactionKind.Clap, Monday.AddDays(-14))).Error);
            Assert.Equal(RankingErrors.ReactionOutOfWindow, (await ReactAsync(Ana, Bia, RankingReactionKind.Clap, Monday.AddDays(7))).Error);
            Assert.Equal(RankingErrors.InvalidWeekStart, (await ReactAsync(Ana, Bia, RankingReactionKind.Clap, Today)).Error);
            Assert.Equal(RankingErrors.InvalidWeekStart, (await _service.SetAsync(
                GroupId, MemberOf(Bia), new RankingReactionRequestDto(null, RankingReactionKind.Clap), Ana, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task SetAsync_ShouldRefuseMyselfOutsidersUnknownMembersAndKinds()
        {
            Assert.Equal(RankingErrors.CannotReactToSelf, (await ReactAsync(Ana, Ana, RankingReactionKind.Clap)).Error);
            Assert.Equal(RankingGroupErrors.NotFound, (await ReactAsync(Outsider, Bia, RankingReactionKind.Clap)).Error);
            Assert.Equal(RankingErrors.MemberNotFound, (await _service.SetAsync(
                GroupId, 99, new RankingReactionRequestDto(Monday, RankingReactionKind.Clap), Ana, CancellationToken.None)).Error);
            Assert.Equal(RankingErrors.UnknownReaction, (await ReactAsync(Ana, Bia, (RankingReactionKind)7)).Error);
            Assert.Empty(RankingReactionSingleton.Instance);
        }

        [Fact]
        public async Task LeavingTheGroup_ShouldTakeTheReactionsSentAndReceived()
        {
            await ReactAsync(Ana, Bia, RankingReactionKind.Clap);
            await ReactAsync(Bia, Caio, RankingReactionKind.Fire);
            await ReactAsync(Ana, Caio, RankingReactionKind.Fire);

            await ServiceProvider.GetRequiredService<RankingGroupService>().LeaveAsync(GroupId, Bia, CancellationToken.None);

            var reaction = Assert.Single(RankingReactionSingleton.Instance);
            Assert.Equal((MemberOf(Ana), MemberOf(Caio)), (reaction.FromMemberId.Value, reaction.ToMemberId.Value));
        }

        [Fact]
        public async Task TheRanking_ShouldCountEachLinesReactions_AndShowWhoReactedOnlyOnMine()
        {
            await ReactAsync(Ana, Bia, RankingReactionKind.Clap);
            await ReactAsync(Caio, Bia, RankingReactionKind.Fire);
            await ReactAsync(Bia, Ana, RankingReactionKind.Fire);

            var forBia = (await _rankingService.GetAsync(GroupId, Bia, null, CancellationToken.None)).Value!;
            var forAna = (await _rankingService.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!;

            var biaSeenByBia = forBia.Entries.Single(entry => entry.IsMe);
            Assert.Equal(new RankingReactionsDto(1, 1, null), biaSeenByBia.Reactions);
            Assert.Equal([("Ana", RankingReactionKind.Clap), ("Caio", RankingReactionKind.Fire)], biaSeenByBia.ReactedBy!.Select(by => (by.Name, by.Kind)));
            Assert.All(forBia.Entries.Where(entry => !entry.IsMe), entry => Assert.Null(entry.ReactedBy));

            var biaSeenByAna = forAna.Entries.Single(entry => entry.MemberId == MemberOf(Bia));
            Assert.Equal(new RankingReactionsDto(1, 1, RankingReactionKind.Clap), biaSeenByAna.Reactions);
            Assert.Null(biaSeenByAna.ReactedBy);
        }
    }
}
