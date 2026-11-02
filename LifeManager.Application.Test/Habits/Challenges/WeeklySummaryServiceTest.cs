using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits.Challenges
{
    [Collection("ApplicationServices")]
    public class WeeklySummaryServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        // The summarized week runs from Monday 2026-11-02 to Sunday 2026-11-08; the summary shows from Monday 2026-11-09.
        private static readonly DateOnly LastMonday = new(2026, 11, 2);
        private static readonly DateOnly Monday = LastMonday.AddDays(7);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly WeeklySummaryService _service;
        private readonly ChallengeService _challenges;
        private readonly FakeTimeProvider _clock;

        public WeeklySummaryServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<WeeklySummaryService>();
            _challenges = ServiceProvider.GetRequiredService<ChallengeService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.SetToday(Monday);

            WeeklyChallengeSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            WeeklyChallengeRepositoryMock.BeforeComplete = null;
            RankingGroupSingleton.Instance.Clear();
            RankingGroupMemberSingleton.Instance.Clear();
            RankingWeekResultSingleton.Instance.Clear();
        }

        private static Habit SeedHabit(HabitKind kind = HabitKind.Positive)
        {
            var id = HabitSingleton.Instance.Count + 1;
            var habit = Habit.FromPersistence(
                id, UserId.Value, $"Habit {id}", null, null, kind, HabitDifficulty.Medium, HabitFrequencyType.Daily,
                HabitWeekDays.None, null, new DateOnly(2026, 9, 1), CreatedAt, null, 0, 0, Monday.AddDays(-1), null, null, null);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void Seed(Habit habit, HabitCheckInStatus status, params DateOnly[] dates)
        {
            foreach (var date in dates)
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, status, CreatedAt, 0, 0, 0));
        }

        private static void SeedProfile(DateOnly? summarySeenWeek = null)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(
                1, UserId.Value, 0, GameRules.MaxHp, GameRules.MaxHp, 0, 0, summarySeenWeek: summarySeenWeek));

        /// <summary>Creates a challenge during the summarized week (on its Tuesday).</summary>
        private async Task CreateLastWeek(Habit habit, int target)
        {
            _clock.SetToday(LastMonday.AddDays(1));
            var created = await _challenges.CreateAsync(UserId, new CreateChallengeDto(habit.Id!.Value, ChallengeType.Frequency, target, ChallengeWeek.Current), CancellationToken.None);
            Assert.True(created.IsSuccess);
            _clock.SetToday(Monday);
        }

        [Fact]
        public async Task GetAsync_ShouldSummarizeLastWeekNextToTheWeekBefore()
        {
            SeedProfile();
            var read = SeedHabit();
            var walk = SeedHabit();
            var smoke = SeedHabit(HabitKind.Negative);
            await CreateLastWeek(read, 2);
            await CreateLastWeek(walk, 5);
            Seed(read, HabitCheckInStatus.Done, LastMonday, LastMonday.AddDays(6));
            Seed(walk, HabitCheckInStatus.Done, LastMonday.AddDays(2));
            Seed(smoke, HabitCheckInStatus.Clean, LastMonday, LastMonday.AddDays(1));
            Seed(read, HabitCheckInStatus.Done, LastMonday.AddDays(-7));

            var summary = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(LastMonday, summary.WeekStart);
            Assert.Equal(LastMonday.AddDays(6), summary.WeekEnd);
            Assert.Equal(3, summary.Week.CheckIns);
            Assert.Equal(2, summary.Week.CleanDays);
            Assert.Equal(1, summary.Week.ChallengesCompleted);
            var completed = Assert.Single(summary.Challenges, challenge => challenge.Status == ChallengeStatus.Completed);
            Assert.Equal(completed.RewardCoins, summary.Week.Coins);
            Assert.Contains(summary.Challenges, challenge => challenge.HabitId == walk.Id!.Value && challenge.Status == ChallengeStatus.Active);
            Assert.Equal(new WeeklySummaryFiguresDto(1, 0, 0, 0), summary.PreviousWeek);
            Assert.Empty(summary.Ranking);
        }

        [Fact]
        public async Task GetAsync_ShouldShowOnceAndStopAfterSeen()
        {
            SeedProfile();
            Seed(SeedHabit(), HabitCheckInStatus.Done, LastMonday);

            Assert.NotNull(await _service.GetAsync(UserId, CancellationToken.None));

            var seen = await _service.MarkSeenAsync(UserId, new WeeklySummarySeenDto(LastMonday), CancellationToken.None);

            Assert.True(seen.IsSuccess);
            Assert.Equal(LastMonday, PlayerProfileSingleton.Instance.Single().SummarySeenWeek);
            Assert.Null(await _service.GetAsync(UserId, CancellationToken.None));

            // The next week brings the next summary.
            Seed(HabitSingleton.Instance.Single(), HabitCheckInStatus.Done, Monday);
            _clock.SetToday(Monday.AddDays(7));
            Assert.NotNull(await _service.GetAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAsync_ShouldSkipAPlayerWhoDidNotPlayLastWeek()
        {
            SeedProfile();
            Seed(SeedHabit(), HabitCheckInStatus.Done, LastMonday.AddDays(-7), Monday);
            Seed(SeedHabit(), HabitCheckInStatus.Missed, LastMonday);

            Assert.Null(await _service.GetAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task GetAsync_ShouldSkipAPlayerWithoutProfile()
        {
            Seed(SeedHabit(), HabitCheckInStatus.Done, LastMonday);

            Assert.Null(await _service.GetAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task MarkSeenAsync_ShouldRefuseTheCurrentWeekAndANonMonday()
        {
            SeedProfile();

            Assert.Equal(ChallengeErrors.InvalidSummaryWeek, (await _service.MarkSeenAsync(UserId, new WeeklySummarySeenDto(Monday), CancellationToken.None)).Error);
            Assert.Equal(ChallengeErrors.InvalidSummaryWeek, (await _service.MarkSeenAsync(UserId, new WeeklySummarySeenDto(LastMonday.AddDays(1)), CancellationToken.None)).Error);
            Assert.Null(PlayerProfileSingleton.Instance.Single().SummarySeenWeek);
        }

        [Fact]
        public async Task MarkSeenAsync_ShouldNotGoBackToAnEarlierWeek()
        {
            SeedProfile(summarySeenWeek: LastMonday);

            await _service.MarkSeenAsync(UserId, new WeeklySummarySeenDto(LastMonday.AddDays(-7)), CancellationToken.None);

            Assert.Equal(LastMonday, PlayerProfileSingleton.Instance.Single().SummarySeenWeek);
        }
    }
}
