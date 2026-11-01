using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits.Challenges
{
    [Collection("ApplicationServices")]
    public class ChallengeServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        // Tuesday; the week runs from Monday 2026-11-02 to Sunday 2026-11-08.
        private static readonly DateOnly Monday = new(2026, 11, 2);
        private static readonly DateOnly Tuesday = Monday.AddDays(1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly ChallengeService _service;
        private readonly FakeTimeProvider _clock;

        public ChallengeServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<ChallengeService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.SetToday(Tuesday);

            WeeklyChallengeSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            WeeklyChallengeRepositoryMock.BeforeComplete = null;
        }

        private static Habit SeedHabit(
            UserId? userId = null, HabitKind kind = HabitKind.Positive, int? targetValue = null, DateTimeOffset? archivedAt = null,
            DateOnly? startDate = null)
        {
            var id = HabitSingleton.Instance.Count + 1;
            var habit = Habit.FromPersistence(
                id, (userId ?? UserId).Value, $"Habit {id}", null, null, kind, HabitDifficulty.Medium, HabitFrequencyType.Daily,
                HabitWeekDays.None, null, startDate ?? new DateOnly(2026, 9, 1), CreatedAt, archivedAt, 0, 0, Monday.AddDays(-1),
                targetValue, targetValue is null ? null : "ml", targetValue is null ? null : 250);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void Done(Habit habit, params DateOnly[] dates)
        {
            foreach (var date in dates)
                HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                    HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, HabitCheckInStatus.Done, CreatedAt, 0, 0, 0));
        }

        private static void Log(Habit habit, DateOnly date, int value)
            => HabitProgressEntrySingleton.Instance.Add(HabitProgressEntry.FromPersistence(
                HabitProgressEntrySingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, value, CreatedAt));

        /// <summary>Three days done in each of the four weeks before this one: a baseline of 3.</summary>
        private static void SeedUsualWeeks(Habit habit)
        {
            for (var week = 1; week <= 4; week++)
            {
                var weekStart = Monday.AddDays(-7 * week);
                Done(habit, weekStart, weekStart.AddDays(2), weekStart.AddDays(4));
            }
        }

        private Task<LifeManager.Domain.Shared.Results.Result<ChallengeDto>> Create(Habit habit, int target, ChallengeType type = ChallengeType.Frequency, ChallengeWeek week = ChallengeWeek.Current)
            => _service.CreateAsync(UserId, new CreateChallengeDto(habit.Id!.Value, type, target, week), CancellationToken.None);

        [Fact]
        public async Task CreateAsync_ShouldStoreTheBaselineEffortAndReward()
        {
            var habit = SeedHabit();
            SeedUsualWeeks(habit);
            Done(habit, Monday);

            var result = await Create(habit, 5);

            Assert.True(result.IsSuccess);
            var challenge = result.Value!;
            Assert.Equal(3m, challenge.Baseline);
            Assert.Equal(ChallengeEffort.Stretch, challenge.Effort);
            Assert.Equal(60, challenge.RewardCoins);
            Assert.Equal(1, challenge.Progress);
            Assert.Equal(6, challenge.DaysLeft);
            Assert.Single(WeeklyChallengeSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldMatchThePreview()
        {
            var habit = SeedHabit();
            SeedUsualWeeks(habit);

            var preview = await _service.PreviewAsync(UserId, new CreateChallengeDto(habit.Id!.Value, ChallengeType.Frequency, 4, ChallengeWeek.Next), CancellationToken.None);
            var created = await Create(habit, 4, week: ChallengeWeek.Next);

            Assert.Equal(ChallengeEffort.Steady, preview.Value!.Effort);
            Assert.Equal(preview.Value.RewardCoins, created.Value!.RewardCoins);
            Assert.Equal(preview.Value.RewardXp, created.Value.RewardXp);
            Assert.Equal(Monday.AddDays(7), created.Value.WeekStart);
            Assert.DoesNotContain(WeeklyChallengeSingleton.Instance, challenge => challenge.WeekStart == Monday);
        }

        [Fact]
        public async Task CreateAsync_ShouldRefuseEachBrokenRule()
        {
            var habit = SeedHabit();
            var avoid = SeedHabit(kind: HabitKind.Negative);
            var other = SeedHabit(OtherUserId);
            Done(habit, Monday, Tuesday);

            Assert.Equal(HabitErrors.NotFound, (await Create(other, 3)).Error);
            Assert.Equal(ChallengeErrors.HabitNotEligible, (await Create(avoid, 3)).Error);
            Assert.Equal(ChallengeErrors.NotMeasurable, (await Create(habit, 300, ChallengeType.Volume)).Error);
            Assert.Equal(ChallengeErrors.InvalidTarget, (await Create(habit, 8)).Error);
            Assert.Equal(ChallengeErrors.TargetAlreadyReached, (await Create(habit, 2)).Error);
            Assert.Equal(ChallengeErrors.InvalidWeek, (await Create(habit, 3, week: (ChallengeWeek)7)).Error);

            Assert.True((await Create(habit, 3)).IsSuccess);
            Assert.Equal(ChallengeErrors.AlreadyExists, (await Create(habit, 4)).Error);

            _clock.SetToday(Monday.AddDays(3));
            Assert.Equal(ChallengeErrors.CreationWindowClosed, (await Create(SeedHabit(), 3)).Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldStopAtThreeChallengesAWeek()
        {
            for (var i = 0; i < 3; i++)
                Assert.True((await Create(SeedHabit(), 3)).IsSuccess);

            Assert.Equal(ChallengeErrors.WeekLimitReached, (await Create(SeedHabit(), 3)).Error);
        }

        [Fact]
        public async Task GetWeekAsync_ShouldListTheChallengesWithSuggestionsForWhatIsLeft()
        {
            var challenged = SeedHabit();
            var forgotten = SeedHabit();
            var measured = SeedHabit(targetValue: 2000);
            SeedUsualWeeks(forgotten);
            for (var weeksBack = 1; weeksBack <= 4; weeksBack++)
                Log(measured, Monday.AddDays(-7 * weeksBack), 9000);
            await Create(challenged, 3);

            var result = await _service.GetWeekAsync(UserId, ChallengeWeek.Current, CancellationToken.None);

            var week = result.Value!;
            Assert.True(week.CanCreate);
            Assert.Equal(Monday, week.WeekStart);
            Assert.Single(week.Challenges);
            Assert.Equal(2, week.Habits.Count);
            Assert.Collection(week.Suggestions,
                suggestion => Assert.Equal((forgotten.Id!.Value, ChallengeType.Frequency, 4), (suggestion.HabitId, suggestion.Type, suggestion.Target)),
                suggestion => Assert.Equal((measured.Id!.Value, ChallengeType.Volume, 10_500, "ml"), (suggestion.HabitId, suggestion.Type, suggestion.Target, suggestion.Unit)));
        }

        [Fact]
        public async Task GetWeekAsync_ShouldCloseTheCurrentWeek_AfterWednesday()
        {
            SeedUsualWeeks(SeedHabit());
            _clock.SetToday(Monday.AddDays(3));

            var current = (await _service.GetWeekAsync(UserId, ChallengeWeek.Current, CancellationToken.None)).Value!;
            var next = (await _service.GetWeekAsync(UserId, ChallengeWeek.Next, CancellationToken.None)).Value!;

            Assert.False(current.IsOpen);
            Assert.False(current.CanCreate);
            Assert.Empty(current.Suggestions);
            Assert.True(next.CanCreate);
            Assert.Single(next.Suggestions);
        }

        [Fact]
        public async Task DeleteAsync_ShouldOnlyGiveUpAnActiveChallenge()
        {
            var habit = SeedHabit();
            var created = (await Create(habit, 3)).Value!;

            Assert.Equal(ChallengeErrors.NotFound, (await _service.DeleteAsync(OtherUserId, created.Id, CancellationToken.None)).Error);
            Assert.True((await _service.DeleteAsync(UserId, created.Id, CancellationToken.None)).IsSuccess);
            Assert.Empty(WeeklyChallengeSingleton.Instance);

            var completed = (await Create(habit, 1)).Value!;
            Done(habit, Monday);
            await _service.SyncAsync(UserId, CancellationToken.None);
            Assert.Equal(ChallengeErrors.NotActive, (await _service.DeleteAsync(UserId, completed.Id, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task SyncAsync_ShouldCompleteAndPayOnce()
        {
            var habit = SeedHabit();
            await Create(habit, 2);
            Done(habit, Monday, Tuesday);

            var first = await _service.SyncAsync(UserId, CancellationToken.None);
            var second = await _service.SyncAsync(UserId, CancellationToken.None);

            Assert.Single(first);
            Assert.Empty(second);
            var stored = Assert.Single(WeeklyChallengeSingleton.Instance);
            Assert.Equal(ChallengeStatus.Completed, stored.Status);
            // 80 XP takes a new player up a level, which adds a LevelUp line.
            var entry = Assert.Single(GameLedgerEntrySingleton.Instance, entry => entry.Kind == GameLedgerEntryKind.ChallengeCompleted);
            Assert.Equal(stored.Id, entry.ChallengeId);
            Assert.Equal(habit.Id, entry.HabitId);
            Assert.Equal(stored.RewardCoins, PlayerProfileSingleton.Instance.Single().Coins);
        }

        [Fact]
        public async Task SyncAsync_ShouldPayNothing_WhenAnotherSyncCompletedItFirst()
        {
            var habit = SeedHabit();
            await Create(habit, 1);
            Done(habit, Monday);
            WeeklyChallengeRepositoryMock.BeforeComplete = () =>
            {
                WeeklyChallengeRepositoryMock.BeforeComplete = null;
                WeeklyChallengeSingleton.Instance.Single().Complete(CreatedAt);
            };

            var completed = await _service.SyncAsync(UserId, CancellationToken.None);

            Assert.Empty(completed);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task SyncAsync_ShouldNotUndoACompletion_WhenTheCheckInIsUndone()
        {
            var habit = SeedHabit();
            await Create(habit, 1);
            Done(habit, Monday);
            await _service.SyncAsync(UserId, CancellationToken.None);

            HabitCheckInSingleton.Instance.Clear();
            await _service.SyncAsync(UserId, CancellationToken.None);

            Assert.Equal(ChallengeStatus.Completed, WeeklyChallengeSingleton.Instance.Single().Status);
        }

        [Fact]
        public async Task SyncAsync_ShouldFailFromTuesday_NotOnMonday()
        {
            var habit = SeedHabit();
            await Create(habit, 5);

            _clock.SetToday(Monday.AddDays(7));
            await _service.SyncAsync(UserId, CancellationToken.None);
            Assert.Equal(ChallengeStatus.Active, WeeklyChallengeSingleton.Instance.Single().Status);

            _clock.SetToday(Monday.AddDays(8));
            await _service.SyncAsync(UserId, CancellationToken.None);
            Assert.Equal(ChallengeStatus.Failed, WeeklyChallengeSingleton.Instance.Single().Status);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task SyncAsync_ShouldCompleteOnMonday_WithALateSundayCheckIn()
        {
            var habit = SeedHabit();
            await Create(habit, 1);
            _clock.SetToday(Monday.AddDays(7));
            Done(habit, Monday.AddDays(6));

            Assert.Single(await _service.SyncAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task SyncAsync_ShouldCancelTheChallengesOfAnArchivedHabit()
        {
            var habit = SeedHabit();
            await Create(habit, 3);
            HabitSingleton.Instance[0] = SeedArchivedCopy(habit);

            await _service.SyncAsync(UserId, CancellationToken.None);

            Assert.Equal(ChallengeStatus.Cancelled, WeeklyChallengeSingleton.Instance.Single().Status);
            Assert.Empty((await _service.GetWeekAsync(UserId, ChallengeWeek.Current, CancellationToken.None)).Value!.Challenges);
        }

        [Fact]
        public async Task SyncAsync_ShouldCountVolume_FromEveryEntryOfTheWeek()
        {
            var habit = SeedHabit(targetValue: 2000);
            await Create(habit, 3000, ChallengeType.Volume);
            Log(habit, Monday, 1500);
            Log(habit, Tuesday, 1000);

            Assert.Empty(await _service.SyncAsync(UserId, CancellationToken.None));

            Log(habit, Tuesday, 500);
            Assert.Single(await _service.SyncAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task SyncAndGetUnseenAsync_ShouldListACompletion_UntilSeen()
        {
            var habit = SeedHabit();
            var created = (await Create(habit, 1)).Value!;
            Done(habit, Monday);

            var unseen = await _service.SyncAndGetUnseenAsync(UserId, CancellationToken.None);
            Assert.Equal(ChallengeStatus.Completed, Assert.Single(unseen).Status);
            Assert.Equal(created.Id, unseen[0].Id);
            Assert.Single(await _service.GetCurrentWeekAsync(UserId, CancellationToken.None));

            await _service.MarkSeenAsync(UserId, [created.Id], CancellationToken.None);
            Assert.Empty(await _service.SyncAndGetUnseenAsync(UserId, CancellationToken.None));
        }

        [Fact]
        public async Task GetHistoryAsync_ShouldListPastWeeks_NewestFirst()
        {
            var habit = SeedHabit();
            await Create(habit, 3, week: ChallengeWeek.Next);
            _clock.SetToday(Monday.AddDays(15));
            await Create(habit, 3);

            var page = await _service.GetHistoryAsync(UserId, new ChallengeHistoryQueryDto(), CancellationToken.None);

            var item = Assert.Single(page.Value!.Items);
            Assert.Equal(Monday.AddDays(7), item.WeekStart);
            Assert.Equal(ChallengeStatus.Failed, item.Status);
            Assert.Equal(0, item.DaysLeft);
        }

        private static Habit SeedArchivedCopy(Habit habit)
            => Habit.FromPersistence(
                habit.Id!.Value, habit.UserId.Value, habit.Name.Value, null, null, habit.Kind, habit.Difficulty, habit.FrequencyType,
                habit.WeekDays, habit.TimesPerWeek, habit.StartDate, habit.CreatedAt, CreatedAt, 0, 0, habit.EvaluatedUntil);
    }
}
