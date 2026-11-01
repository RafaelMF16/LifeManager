using LifeManager.Application.Ranking.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Ranking.Mocks;
using LifeManager.Application.Test.RankingGroups;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Ranking
{
    [Collection("ApplicationServices")]
    public class GroupRankingServiceTest : BaseTest
    {
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        private static readonly UserId Outsider = new(4);
        private static readonly DateOnly CreatedOn = new(2026, 10, 28);
        private static readonly DateOnly FirstMonday = new(2026, 10, 26);
        private static readonly DateOnly LastMonday = new(2026, 11, 2);
        private static readonly DateOnly Monday = new(2026, 11, 9);
        // Wednesday of the current week.
        private static readonly DateOnly Today = Monday.AddDays(2);
        private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        private const int GroupId = 1;

        private readonly GroupRankingService _service;
        private readonly FakeTimeProvider _timeProvider;

        public GroupRankingServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<GroupRankingService>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            RankingGroupSingleton.Instance.Clear();
            RankingGroupMemberSingleton.Instance.Clear();
            RankingReactionSingleton.Instance.Clear();
            RankingWeekResultSingleton.Instance.Clear();
            RankingWeekResultRepositoryMock.Reset();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            RankingGroupServiceTest.SeedUsers(5);

            RankingGroupSingleton.Instance.Add(RankingGroup.FromPersistence(
                GroupId, "Família", Ana.Value, "ABCDEFGH", CreatedOn.ToDateTime(TimeOnly.MinValue), CreatedOn));
            SeedMember(Ana, CreatedOn);
            SeedMember(Bia, CreatedOn);
        }

        private static void SeedMember(UserId userId, DateOnly joinedOn, int groupId = GroupId)
            => RankingGroupMemberSingleton.Instance.Add(RankingGroupMember.FromPersistence(
                RankingGroupMemberSingleton.Instance.Count + 1, groupId, userId.Value, joinedOn.ToDateTime(TimeOnly.MinValue), joinedOn));

        private static Habit SeedHabit(UserId userId)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, userId.Value, $"Habit {HabitSingleton.Instance.Count + 1}", null, null, HabitKind.Positive,
                HabitDifficulty.Hard, HabitFrequencyType.Daily, HabitWeekDays.None, null, new DateOnly(2026, 10, 1), CreatedAt,
                null, 0, 0, Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedDone(Habit habit, params DateOnly[] dates)
        {
            foreach (var date in dates)
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, HabitCheckInStatus.Done, CreatedAt, 0, 0, 0));
        }

        private static void SeedResult(UserId userId, DateOnly weekStart, int points, int position, int eligibleCount = 2)
            => RankingWeekResultSingleton.Instance.Add(RankingWeekResult.FromPersistence(
                RankingWeekResultSingleton.Instance.Count + 1, GroupId, weekStart, userId.Value, points, 0, 0, position, true, 2, eligibleCount, CreatedAt));

        private static int MemberIdOf(UserId userId)
            => RankingGroupMemberSingleton.Instance.Single(member => member.GroupId.Value == GroupId && member.UserId == userId).Id!.Value;

        [Fact]
        public async Task GetAsync_ShouldRankTheCurrentWeekLive_TheSameForEveryMember()
        {
            SeedDone(SeedHabit(Bia), Monday, Monday.AddDays(1));
            SeedDone(SeedHabit(Ana), Monday);

            var forAna = (await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!;
            var forBia = (await _service.GetAsync(GroupId, Bia, null, CancellationToken.None)).Value!;

            Assert.Equal(RankingWeekStatus.Open, forAna.Status);
            Assert.Equal((Monday, Monday.AddDays(6), FirstMonday), (forAna.WeekStart, forAna.WeekEnd, forAna.FirstWeekStart));
            Assert.False(forAna.CountsForAchievements);
            Assert.Equal(["Bia", "Ana"], forAna.Entries.Select(entry => entry.Name));
            Assert.Equal([1, 2], forAna.Entries.Select(entry => entry.Position));
            Assert.Equal([40, 20], forAna.Entries.Select(entry => entry.Points));
            Assert.Equal(forAna.Entries.Select(entry => (entry.MemberId, entry.Points, entry.Position)), forBia.Entries.Select(entry => (entry.MemberId, entry.Points, entry.Position)));
            Assert.Equal([false, true], forAna.Entries.Select(entry => entry.IsMe));
            Assert.Equal(MemberIdOf(Bia), forAna.Entries[0].MemberId);

            Assert.Equal((2, 20, 20, "Bia"), (forAna.Me.Position, forAna.Me.Points, forAna.Me.GapToNext, forAna.Me.NextName));
            Assert.Equal(7, forAna.Me.Days.Count);
            Assert.Equal(20, forAna.Me.Days[0].Points);
            Assert.Null(forBia.Me.GapToNext);
        }

        [Fact]
        public async Task GetAsync_ShouldShareThePositionOnATie_AndOrderByName()
        {
            SeedMember(Caio, CreatedOn);

            var ranking = (await _service.GetAsync(GroupId, Caio, null, CancellationToken.None)).Value!;

            Assert.Equal(["Ana", "Bia", "Caio"], ranking.Entries.Select(entry => entry.Name));
            Assert.All(ranking.Entries, entry => Assert.Equal(1, entry.Position));
            Assert.True(ranking.CountsForAchievements);
            Assert.Null(ranking.Me.GapToNext);
        }

        [Fact]
        public async Task GetAsync_ShouldMarkWhoJoinedMidWeekAsNotEligible()
        {
            SeedMember(Caio, Monday.AddDays(1));

            var ranking = (await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!;

            Assert.False(ranking.Entries.Single(entry => entry.Name == "Caio").Eligible);
            Assert.False(ranking.CountsForAchievements);
        }

        [Fact]
        public async Task GetAsync_ShouldListWhoPassedMeSinceYesterday()
        {
            SeedDone(SeedHabit(Ana), Monday);
            var bia = SeedHabit(Bia);
            SeedDone(bia, Today);
            SeedDone(SeedHabit(Bia), Today);

            var ranking = (await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!;

            var overtaker = Assert.Single(ranking.OvertakenBy);
            Assert.Equal((MemberIdOf(Bia), "Bia"), (overtaker.MemberId, overtaker.Name));
            Assert.Empty((await _service.GetAsync(GroupId, Bia, null, CancellationToken.None)).Value!.OvertakenBy);
        }

        [Fact]
        public async Task GetAsync_ShouldNotListOvertakes_OnMondayOrInPastWeeks()
        {
            SeedDone(SeedHabit(Bia), Monday, LastMonday.AddDays(6));
            _timeProvider.SetToday(Monday);

            Assert.Empty((await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!.OvertakenBy);
            Assert.Empty((await _service.GetAsync(GroupId, Ana, LastMonday, CancellationToken.None)).Value!.OvertakenBy);
        }

        [Fact]
        public async Task GetAsync_ShouldScoreAnUnstoredPastWeekAsClosing()
        {
            SeedDone(SeedHabit(Bia), LastMonday);

            var ranking = (await _service.GetAsync(GroupId, Ana, LastMonday, CancellationToken.None)).Value!;

            Assert.Equal(RankingWeekStatus.Closing, ranking.Status);
            Assert.Equal([20, 0], ranking.Entries.Select(entry => entry.Points));
            Assert.Null(ranking.Previous);
        }

        [Fact]
        public async Task GetAsync_ShouldShowTheStoredWeek_WithWhoLeftTheGroup()
        {
            SeedResult(Caio, LastMonday, 300, 1, eligibleCount: 3);
            SeedResult(Ana, LastMonday, 100, 2, eligibleCount: 3);
            SeedResult(Bia, LastMonday, 0, 3, eligibleCount: 3);
            // A check-in the stored result doesn't see.
            SeedDone(SeedHabit(Bia), LastMonday);

            var ranking = (await _service.GetAsync(GroupId, Ana, LastMonday, CancellationToken.None)).Value!;

            Assert.Equal(RankingWeekStatus.Final, ranking.Status);
            Assert.True(ranking.CountsForAchievements);
            Assert.Equal([300, 100, 0], ranking.Entries.Select(entry => entry.Points));
            var caio = ranking.Entries[0];
            Assert.Equal(("Caio", null, true), (caio.Name, caio.MemberId, caio.LeftGroup));
            Assert.Equal((2, 200, "Caio"), (ranking.Me.Position!.Value, ranking.Me.GapToNext!.Value, ranking.Me.NextName));
        }

        [Fact]
        public async Task GetAsync_ShouldBringLastWeeksPodium_WithTheCurrentWeek()
        {
            SeedResult(Bia, LastMonday, 200, 1);
            SeedResult(Ana, LastMonday, 100, 2);

            var previous = (await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!.Previous!;

            Assert.Equal((LastMonday, RankingWeekStatus.Final, 2, 100), (previous.WeekStart, previous.Status, previous.MyPosition!.Value, previous.MyPoints!.Value));
            Assert.Equal(["Bia", "Ana"], previous.Podium.Select(entry => entry.Name));
        }

        [Fact]
        public async Task GetAsync_ShouldHaveNoPrevious_InTheGroupsFirstWeek()
        {
            _timeProvider.SetToday(CreatedOn);

            Assert.Null((await _service.GetAsync(GroupId, Ana, null, CancellationToken.None)).Value!.Previous);
        }

        [Fact]
        public async Task GetAsync_ShouldRefuseOutsiders_AndWeeksOutOfRange()
        {
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.GetAsync(GroupId, Outsider, null, CancellationToken.None)).Error);
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.GetAsync(99, Ana, null, CancellationToken.None)).Error);
            Assert.Equal(RankingErrors.InvalidWeekStart, (await _service.GetAsync(GroupId, Ana, Today, CancellationToken.None)).Error);
            Assert.Equal(RankingErrors.WeekOutOfRange, (await _service.GetAsync(GroupId, Ana, Monday.AddDays(7), CancellationToken.None)).Error);
            Assert.Equal(RankingErrors.WeekOutOfRange, (await _service.GetAsync(GroupId, Ana, FirstMonday.AddDays(-7), CancellationToken.None)).Error);
            Assert.True((await _service.GetAsync(GroupId, Ana, FirstMonday, CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task GetGroupsAsync_ShouldBringMyPlaceInEachGroup()
        {
            RankingGroupSingleton.Instance.Add(RankingGroup.FromPersistence(2, "Amigos", Caio.Value, "BCDEFGHJ", CreatedAt, CreatedOn));
            SeedMember(Caio, CreatedOn, groupId: 2);
            SeedMember(Ana, CreatedOn, groupId: 2);
            SeedDone(SeedHabit(Ana), Monday);
            SeedDone(SeedHabit(Caio), Monday, Monday.AddDays(1));

            var groups = await _service.GetGroupsAsync(Ana, CancellationToken.None);

            Assert.Equal([("Amigos", 2, 20), ("Família", 1, 20)], groups.Select(group => (group.Name, group.MyPosition!.Value, group.MyPoints)));
        }
    }
}
