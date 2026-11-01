using LifeManager.Application.Ranking.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Ranking
{
    [Collection("ApplicationServices")]
    public class WeeklyScoreServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        // Wednesday; its week runs from Monday 2026-11-02 to Sunday 2026-11-08.
        private static readonly DateOnly Today = new(2026, 11, 4);
        private static readonly DateOnly Monday = new(2026, 11, 2);
        private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly WeeklyScoreService _service;

        public WeeklyScoreServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<WeeklyScoreService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
        }

        internal static Habit SeedHabit(UserId userId, HabitKind kind = HabitKind.Positive, DateTimeOffset? archivedAt = null)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, userId.Value, $"Habit {HabitSingleton.Instance.Count + 1}", null, null, kind,
                HabitDifficulty.Hard, HabitFrequencyType.Daily, HabitWeekDays.None, null, new DateOnly(2026, 10, 1), CreatedAt,
                archivedAt, 0, 0, Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        internal static void SeedDay(Habit habit, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, status, CreatedAt, 0, 0, 0));

        [Fact]
        public async Task GetMineAsync_ShouldScoreTheCurrentWeekUpToToday_WhenNoWeekIsGiven()
        {
            var habit = SeedHabit(UserId);
            SeedDay(habit, Monday);
            SeedDay(habit, Today);
            // Last week and another player's day don't count.
            SeedDay(habit, Monday.AddDays(-1));
            SeedDay(SeedHabit(OtherUserId), Monday);

            var result = await _service.GetMineAsync(UserId, null, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var score = result.Value!;
            Assert.Equal(Monday, score.WeekStart);
            Assert.Equal(Monday.AddDays(6), score.WeekEnd);
            Assert.Equal(40, score.Points);
            Assert.Equal(2, score.PerfectDays);
            Assert.Equal(7, score.Days.Count);
            Assert.False(score.Days[1].Perfect);
        }

        [Fact]
        public async Task GetMineAsync_ShouldScoreAPastWeek()
        {
            var habit = SeedHabit(UserId);
            SeedDay(habit, Monday.AddDays(-1));

            var result = await _service.GetMineAsync(UserId, Monday.AddDays(-7), CancellationToken.None);

            Assert.Equal(20, result.Value!.Points);
        }

        [Fact]
        public async Task GetMineAsync_ShouldReturnZeros_ForAFutureWeek()
        {
            SeedHabit(UserId);

            var result = await _service.GetMineAsync(UserId, Monday.AddDays(7), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value!.Points);
        }

        [Fact]
        public async Task GetMineAsync_ShouldRefuseAWeekThatDoesNotStartOnMonday()
        {
            var result = await _service.GetMineAsync(UserId, Today, CancellationToken.None);

            Assert.Equal(RankingErrors.InvalidWeekStart, result.Error);
        }

        [Fact]
        public async Task GetMineAsync_ShouldStillExpectAHabitArchivedToday()
        {
            var kept = SeedHabit(UserId);
            SeedHabit(UserId, archivedAt: new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero));
            SeedDay(kept, Today);

            var result = await _service.GetMineAsync(UserId, null, CancellationToken.None);

            Assert.False(result.Value!.Days[2].Perfect);
        }

        [Fact]
        public async Task GetForUsersAsync_ShouldReturnOneResultPerUser_ZerosForAPlayerWithoutHabits()
        {
            SeedDay(SeedHabit(UserId), Monday);

            var scores = await _service.GetForUsersAsync([UserId, OtherUserId], Monday, Today, CancellationToken.None);

            Assert.Equal(20, scores[UserId].Points);
            Assert.Equal(0, scores[OtherUserId].Points);
        }
    }
}
