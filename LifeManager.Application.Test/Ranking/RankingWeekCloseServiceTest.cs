using LifeManager.Application.Ranking.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Ranking.Mocks;
using LifeManager.Application.Test.RankingGroups;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Ranking
{
    [Collection("ApplicationServices")]
    public class RankingWeekCloseServiceTest : BaseTest
    {
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        // The group's first week runs from Monday 2026-10-26; "last week" is Monday 2026-11-02 to Sunday 2026-11-08.
        private static readonly DateOnly FirstMonday = new(2026, 10, 26);
        private static readonly DateOnly LastMonday = new(2026, 11, 2);
        private static readonly DateOnly LastSunday = LastMonday.AddDays(6);
        private static readonly DateOnly Tuesday = new(2026, 11, 10);
        private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        private static readonly RankingGroupId GroupId = new(1);

        private readonly RankingWeekCloseService _service;
        private readonly FakeTimeProvider _timeProvider;

        public RankingWeekCloseServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<RankingWeekCloseService>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Tuesday);

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

            SeedGroup(GroupId, new DateOnly(2026, 10, 28));
            SeedMember(GroupId, Ana, new DateOnly(2026, 10, 28));
            SeedMember(GroupId, Bia, new DateOnly(2026, 10, 28));
        }

        private static void SeedGroup(RankingGroupId id, DateOnly createdOn)
            => RankingGroupSingleton.Instance.Add(RankingGroup.FromPersistence(
                id.Value, $"Group {id.Value}", Ana.Value, $"ABCDEFG{id.Value}", createdOn.ToDateTime(TimeOnly.MinValue), createdOn));

        private static void SeedMember(RankingGroupId groupId, UserId userId, DateOnly joinedOn)
            => RankingGroupMemberSingleton.Instance.Add(RankingGroupMember.FromPersistence(
                RankingGroupMemberSingleton.Instance.Count + 1, groupId.Value, userId.Value, joinedOn.ToDateTime(TimeOnly.MinValue), joinedOn));

        private static Habit SeedHabit(UserId userId, DateOnly evaluatedUntil, DateTimeOffset? archivedAt = null)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, userId.Value, $"Habit {HabitSingleton.Instance.Count + 1}", null, null, HabitKind.Positive,
                HabitDifficulty.Hard, HabitFrequencyType.Daily, HabitWeekDays.None, null, new DateOnly(2026, 10, 1), CreatedAt,
                archivedAt, 0, 0, evaluatedUntil);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedDays(Habit habit, DateOnly from, int days)
        {
            for (var offset = 0; offset < days; offset++)
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, from.AddDays(offset), HabitCheckInStatus.Done, CreatedAt, 0, 0, 0));
        }

        private static List<RankingWeekResult> Week(DateOnly weekStart)
            => [.. RankingWeekResultSingleton.Instance.Where(result => result.GroupId == GroupId && result.WeekStart == weekStart)];

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldStoreEverySettledWeekFromTheGroupsCreation()
        {
            SeedDays(SeedHabit(Ana, LastSunday), LastMonday, 3);
            SeedDays(SeedHabit(Bia, LastSunday), LastMonday, 7);

            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Equal([FirstMonday, LastMonday], outcome.Closed);
            Assert.Empty(outcome.Forced);
            Assert.Null(outcome.Waiting);

            var week = Week(LastMonday);
            var bia = week.Single(result => result.UserId == Bia);
            var ana = week.Single(result => result.UserId == Ana);
            Assert.Equal((1, 140, 7, 7), (bia.Position, bia.Points, bia.PerfectDays, bia.KeptDays));
            Assert.Equal((2, 60, 3, 3), (ana.Position, ana.Points, ana.PerfectDays, ana.KeptDays));
            Assert.All(week, result => Assert.Equal((2, 2), (result.MemberCount, result.EligibleCount)));

            // Nobody scored in the first week: a shared first place.
            Assert.All(Week(FirstMonday), result => Assert.Equal((1, 0), (result.Position, result.Points)));
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldNotCloseTheCurrentWeekOrLastWeekOnMonday()
        {
            SeedHabit(Ana, LastSunday);
            _timeProvider.SetToday(LastSunday.AddDays(1));

            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Equal([FirstMonday], outcome.Closed);
            Assert.Empty(Week(LastMonday));
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldWait_WhileAMembersDayCloseIsBehind()
        {
            SeedHabit(Ana, LastSunday);
            SeedHabit(Bia, LastSunday.AddDays(-1));

            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Equal([FirstMonday], outcome.Closed);
            Assert.Equal(LastMonday, outcome.Waiting);
            Assert.Empty(Week(LastMonday));
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldIgnoreArchivedHabits_WhenWaitingForTheDayClose()
        {
            SeedHabit(Ana, LastSunday);
            SeedHabit(Bia, LastMonday, archivedAt: CreatedAt);

            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Contains(LastMonday, outcome.Closed);
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldCloseWithWhatIsStored_AfterTheGracePeriod()
        {
            SeedHabit(Ana, LastSunday);
            SeedHabit(Bia, LastSunday.AddDays(-3));

            // Tuesday + 3 days is still waiting; the day after closes anyway.
            _timeProvider.SetToday(Tuesday.AddDays(RankingRules.CloseGraceDays));
            Assert.Equal(LastMonday, (await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None)).Waiting);

            _timeProvider.SetToday(Tuesday.AddDays(RankingRules.CloseGraceDays + 1));
            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Equal([LastMonday], outcome.Closed);
            Assert.Equal([LastMonday], outcome.Forced);
            Assert.Equal(2, Week(LastMonday).Count);
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldNeverRewriteAStoredWeek()
        {
            var habit = SeedHabit(Ana, LastSunday);
            SeedDays(habit, LastMonday, 2);
            await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            // An undone check-in after the close, and a second run.
            HabitCheckInSingleton.Instance.RemoveAt(0);
            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Empty(outcome.Closed);
            Assert.Equal(2, RankingWeekResultRepositoryMock.AddCalls);
            Assert.Equal(40, Week(LastMonday).Single(result => result.UserId == Ana).Points);
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldLeaveTheMembersWhoJoinedAfterTheWeekOut_AndMarkTheLateOnesIneligible()
        {
            SeedMember(GroupId, Caio, LastMonday.AddDays(2));
            SeedMember(GroupId, new UserId(4), LastSunday.AddDays(1));

            await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Equal(2, Week(FirstMonday).Count);
            var week = Week(LastMonday);
            Assert.Equal(3, week.Count);
            Assert.False(week.Single(result => result.UserId == Caio).Eligible);
            Assert.All(week, result => Assert.Equal((3, 2), (result.MemberCount, result.EligibleCount)));
        }

        [Fact]
        public async Task CloseDueWeeksAsync_ShouldStop_WhenAnotherRunStoredTheWeekFirst()
        {
            RankingWeekResultRepositoryMock.BeforeNextAdd = () => RankingWeekResultSingleton.Instance.Add(RankingWeekResult.FromPersistence(
                99, GroupId.Value, FirstMonday, Ana.Value, 0, 0, 0, 1, true, 2, 2, CreatedAt));

            var outcome = await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            Assert.Empty(outcome.Closed);
            Assert.Single(Week(FirstMonday));
            Assert.Empty(Week(LastMonday));
        }

        [Fact]
        public async Task GetGroupsToCloseAsync_ShouldListTheGroupsWithoutTheLastSettledWeek()
        {
            SeedGroup(new RankingGroupId(2), LastMonday);
            SeedGroup(new RankingGroupId(3), LastSunday.AddDays(1));
            await _service.CloseDueWeeksAsync(GroupId, CancellationToken.None);

            var ids = await _service.GetGroupsToCloseAsync(CancellationToken.None);

            Assert.Equal([2], ids.Select(id => id.Value));
        }
    }
}
