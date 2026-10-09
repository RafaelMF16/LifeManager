using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitStatsServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitStatsService _statsService;
        private readonly GameLedgerService _ledgerService;

        public HabitStatsServiceTest()
        {
            _statsService = ServiceProvider.GetRequiredService<HabitStatsService>();
            _ledgerService = ServiceProvider.GetRequiredService<GameLedgerService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        private static Habit SeedHabit(UserId? userId = null, DateOnly? startDate = null)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, "Read", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null, startDate ?? new DateOnly(2026, 1, 1), CreatedAt, null, 2, 5, Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedDay(Habit habit, DateOnly date, HabitCheckInStatus status)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, status, CreatedAt, 0, 0, 0));

        private static void SeedEntry(GameLedgerEntryKind kind, int coins, DateTimeOffset createdAt, HabitId? habitId = null, RewardId? rewardId = null, UserId? userId = null)
        {
            var outcome = new GameOutcome(new GameDelta(coins, 0, 0), 0, 0, false, 0, 0);
            var entry = GameLedgerEntry.FromOutcome(userId ?? UserId, kind, DateOnly.FromDateTime(createdAt.Date), createdAt, "Read", outcome, habitId, rewardId)[0];
            entry.AssignId(GameLedgerEntrySingleton.Instance.Count + 1);
            GameLedgerEntrySingleton.Instance.Add(entry);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnTheHeatmapConsistencyAndDaysKept()
        {
            var habit = SeedHabit();
            SeedDay(habit, Today.AddDays(-2), HabitCheckInStatus.Done);
            SeedDay(habit, Today.AddDays(-3), HabitCheckInStatus.Missed);
            SeedDay(habit, Today, HabitCheckInStatus.Done);
            // Long ago: outside the heatmap, still counted as kept.
            SeedDay(habit, new DateOnly(2026, 2, 1), HabitCheckInStatus.Done);
            // Another habit's day doesn't leak in.
            var other = SeedHabit();
            SeedDay(other, Today.AddDays(-1), HabitCheckInStatus.Done);

            var result = await _statsService.GetAsync(habit.Id!.Value, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stats = result.Value;
            Assert.Equal(Today, stats.Today);
            Assert.Equal("Read", stats.Habit.Name);
            Assert.Equal(5, stats.Habit.LongestStreak);
            Assert.Equal(HabitStats.HeatmapDays, stats.Days.Count);
            Assert.Equal(HabitDayState.Done, stats.Days[^1].State);
            Assert.Equal(HabitDayState.Pending, stats.Days[^2].State);
            Assert.Equal(HabitDayState.Missed, stats.Days[^4].State);
            Assert.Equal(new HabitConsistencyDto(2, 3, 67, HabitConsistencyUnit.Days), stats.Consistency);
            Assert.Equal(3, stats.TotalKept);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnNotFound_ForAnotherUsersHabit()
        {
            var habit = SeedHabit(userId: OtherUserId);

            var result = await _statsService.GetAsync(habit.Id!.Value, UserId, CancellationToken.None);

            Assert.Equal(HabitErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldListTheUsersEntriesNewestFirst()
        {
            var habitId = new HabitId(1);
            SeedEntry(GameLedgerEntryKind.HabitDone, 10, CreatedAt, habitId);
            SeedEntry(GameLedgerEntryKind.RewardRedeemed, -50, CreatedAt.AddDays(2), rewardId: new RewardId(4));
            SeedEntry(GameLedgerEntryKind.CleanDay, 5, CreatedAt.AddDays(1), new HabitId(2));
            SeedEntry(GameLedgerEntryKind.HabitDone, 99, CreatedAt.AddDays(3), habitId, userId: OtherUserId);

            var result = await _ledgerService.GetPagedAsync(new GameLedgerListQueryDto { PageSize = 2 }, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.TotalCount);
            Assert.Equal(2, result.Value.TotalPages);
            Assert.Equal([GameLedgerEntryKind.RewardRedeemed, GameLedgerEntryKind.CleanDay], result.Value.Items.Select(item => item.Kind));
            Assert.Equal(-50, result.Value.Items[0].CoinsDelta);
            Assert.Equal(4, result.Value.Items[0].RewardId);
            Assert.Null(result.Value.Items[0].HabitId);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByHabit()
        {
            SeedEntry(GameLedgerEntryKind.HabitDone, 10, CreatedAt, new HabitId(1));
            SeedEntry(GameLedgerEntryKind.HabitMissed, 0, CreatedAt.AddDays(1), new HabitId(2));

            var result = await _ledgerService.GetPagedAsync(new GameLedgerListQueryDto { HabitId = 1 }, UserId, CancellationToken.None);

            var entry = Assert.Single(result.Value.Items);
            Assert.Equal(1, entry.HabitId);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldRejectAnInvalidPageSize()
        {
            var result = await _ledgerService.GetPagedAsync(new GameLedgerListQueryDto { PageSize = PageRequest.MaxPageSize + 1 }, UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
        }
    }
}
