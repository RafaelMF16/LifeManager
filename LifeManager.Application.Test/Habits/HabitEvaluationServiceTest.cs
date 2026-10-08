using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class HabitEvaluationServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // Wednesday: the last closed day is Monday 2026-10-05 (the day before yesterday).
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly LastClosedDay = new(2026, 10, 5);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitEvaluationService _service;

        public HabitEvaluationServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitEvaluationService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        /// <param name="evaluatedUntil">The cursor; defaults to the day before the one being closed (one day to judge).</param>
        private static Habit SeedHabit(
            DateOnly? evaluatedUntil = null,
            HabitKind kind = HabitKind.Positive,
            HabitDifficulty difficulty = HabitDifficulty.Easy,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            DateOnly? startDate = null,
            UserId? userId = null,
            bool archived = false,
            string name = "Habit")
        {
            var cursor = evaluatedUntil ?? LastClosedDay.AddDays(-1);
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, name, null, null, kind, difficulty,
                frequencyType, weekDays, timesPerWeek, startDate ?? new DateOnly(2026, 9, 1), CreatedAt,
                archived ? CreatedAt : null, currentStreak: 5, longestStreak: 5, evaluatedUntil: cursor);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedCheckIn(Habit habit, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, status, CreatedAt, 0, 0, 0));

        private static void SeedProfile(int hp = GameRules.MaxHp, int coins = 0, int streakFreezes = 0)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, 0, hp, GameRules.MaxHp, coins, streakFreezes));

        private Task<Application.Habits.DTOs.HabitEvaluationSummaryDto> Evaluate()
            => _service.EvaluateUserAsync(UserId, CancellationToken.None);

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private static Habit Stored(Habit habit) => HabitSingleton.Instance.Single(stored => stored.Id == habit.Id);

        [Fact]
        public async Task EvaluateUserAsync_ShouldRecordAMissAndDealDamage_WhenADueDayWasNotDone()
        {
            SeedProfile();
            var habit = SeedHabit(difficulty: HabitDifficulty.Medium);

            var summary = await Evaluate();

            Assert.Equal(1, summary.DaysEvaluated);
            var checkIn = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal((LastClosedDay, HabitCheckInStatus.Missed), (checkIn.Date, checkIn.Status));
            Assert.Equal(GameRules.MaxHp - 8, Profile().Hp);
            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.HabitMissed, entry.Kind);
            Assert.Equal(-8, entry.HpDelta);
            Assert.Equal(habit.Id, entry.HabitId);
            Assert.Equal(LastClosedDay, Stored(habit).EvaluatedUntil);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldResetTheStreak_AfterAMiss()
        {
            var habit = SeedHabit();

            await Evaluate();

            Assert.Equal(0, Stored(habit).CurrentStreak);
            Assert.Equal(5, Stored(habit).LongestStreak);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotCharge_ADayThatWasCheckedIn()
        {
            SeedProfile();
            var habit = SeedHabit();
            SeedCheckIn(habit, LastClosedDay);

            var summary = await Evaluate();

            Assert.Equal(1, summary.DaysEvaluated);
            Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
            Assert.Equal(LastClosedDay, Stored(habit).EvaluatedUntil);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldCatchUpSeveralDays_InOrder()
        {
            SeedProfile();
            var habit = SeedHabit(evaluatedUntil: LastClosedDay.AddDays(-4));
            SeedCheckIn(habit, LastClosedDay.AddDays(-2));

            var summary = await Evaluate();

            Assert.Equal(4, summary.DaysEvaluated);
            var missed = HabitCheckInSingleton.Instance.Where(checkIn => checkIn.Status == HabitCheckInStatus.Missed).Select(checkIn => checkIn.Date);
            Assert.Equal([LastClosedDay.AddDays(-3), LastClosedDay.AddDays(-1), LastClosedDay], missed.Order());
            Assert.Equal(GameRules.MaxHp - 3 * 5, Profile().Hp);
            Assert.Equal(LastClosedDay, Stored(habit).EvaluatedUntil);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotJudgeADayTwice_WhenRunAgain()
        {
            SeedProfile();
            SeedHabit();
            await Evaluate();

            var summary = await Evaluate();

            Assert.Equal(0, summary.DaysEvaluated);
            Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(GameRules.MaxHp - 5, Profile().Hp);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotCharge_DaysTheHabitIsNotScheduledOn()
        {
            SeedProfile();
            // Fridays only; the days judged are Saturday 3 to Monday 5.
            var habit = SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 2), frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Friday);

            var summary = await Evaluate();

            Assert.Equal(3, summary.DaysEvaluated);
            Assert.Empty(HabitCheckInSingleton.Instance);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(LastClosedDay, Stored(habit).EvaluatedUntil);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldUseAStreakFreeze_InsteadOfDealingDamage()
        {
            SeedProfile(streakFreezes: 1);
            var habit = SeedHabit();
            SeedCheckIn(habit, LastClosedDay.AddDays(-1));

            await Evaluate();

            var frozen = HabitCheckInSingleton.Instance.Single(checkIn => checkIn.Date == LastClosedDay);
            Assert.Equal(HabitCheckInStatus.Frozen, frozen.Status);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(0, Profile().StreakFreezes);
            Assert.Equal(GameLedgerEntryKind.FreezeUsed, Assert.Single(GameLedgerEntrySingleton.Instance).Kind);
            // Monday frozen and Sunday done: the streak goes on.
            Assert.Equal(2, Stored(habit).CurrentStreak);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldChargeEachMissingCheckIn_WhenAWeeklyHabitsWeekClosesShort()
        {
            SeedProfile();
            // 3× a week; the week from Monday 2026-09-28 closes on Sunday 2026-10-04, judged now.
            var habit = SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 3), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            SeedCheckIn(habit, new DateOnly(2026, 9, 29));

            await Evaluate();

            Assert.Equal(GameRules.MaxHp - 2 * 5, Profile().Hp);
            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal((GameLedgerEntryKind.HabitMissed, new DateOnly(2026, 10, 4)), (entry.Kind, entry.OccurredOn));
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldFreezeTheMissingDays_WhenAWeeklyHabitHasAStreakFreeze()
        {
            SeedProfile(streakFreezes: 2);
            var habit = SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 3), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);
            SeedCheckIn(habit, new DateOnly(2026, 9, 28));

            await Evaluate();

            var frozen = HabitCheckInSingleton.Instance.Where(checkIn => checkIn.Status == HabitCheckInStatus.Frozen).Select(checkIn => checkIn.Date);
            Assert.Equal([new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 30)], frozen.Order());
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(1, Profile().StreakFreezes);
            Assert.Equal(1, Stored(habit).CurrentStreak);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotCharge_AWeekThatStartedBeforeTheHabit()
        {
            SeedProfile();
            SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 1), frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3,
                startDate: new DateOnly(2026, 10, 2));

            await Evaluate();

            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldRewardACleanDay_LikeACheckIn()
        {
            SeedProfile(hp: 50);
            var habit = SeedHabit(kind: HabitKind.Negative, difficulty: HabitDifficulty.Hard);

            await Evaluate();

            var clean = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(HabitCheckInStatus.Clean, clean.Status);
            Assert.Equal((20, 40, 51), (Profile().Coins, Profile().TotalXp, Profile().Hp));
            Assert.Equal(new GameDelta(20, 40, 1), clean.Awarded);
            Assert.Equal(GameLedgerEntryKind.CleanDay, Assert.Single(GameLedgerEntrySingleton.Instance).Kind);
            Assert.Equal(1, Stored(habit).CurrentStreak);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldLeaveTheFreeDaysOfAHabitToAvoidAlone()
        {
            SeedProfile();
            // Avoided on weekdays; Saturday 3 and Sunday 4 are free, Monday 5 is clean.
            SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 2), kind: HabitKind.Negative, frequencyType: HabitFrequencyType.WeekDays,
                weekDays: HabitWeekDays.Monday | HabitWeekDays.Tuesday | HabitWeekDays.Wednesday | HabitWeekDays.Thursday | HabitWeekDays.Friday);

            await Evaluate();

            var clean = Assert.Single(HabitCheckInSingleton.Instance);
            Assert.Equal(LastClosedDay, clean.Date);
            Assert.Equal(5, Profile().Coins);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotRewardADayWithARelapse()
        {
            SeedProfile();
            var habit = SeedHabit(kind: HabitKind.Negative);
            SeedCheckIn(habit, LastClosedDay, HabitCheckInStatus.Relapse);

            await Evaluate();

            Assert.Equal(0, Profile().Coins);
            Assert.Single(HabitCheckInSingleton.Instance);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldKnockOutOnceAndForgiveTheRestOfTheDamage()
        {
            SeedProfile(hp: 12, coins: 100);
            // Two hard habits (12 HP each) missed for three days: the first miss knocks out, the rest is forgiven.
            var first = SeedHabit(evaluatedUntil: LastClosedDay.AddDays(-3), difficulty: HabitDifficulty.Hard, name: "A");
            SeedHabit(evaluatedUntil: LastClosedDay.AddDays(-3), difficulty: HabitDifficulty.Hard, name: "B");

            var summary = await Evaluate();

            Assert.Equal(1, summary.Knockouts);
            Assert.Equal(6, summary.DaysEvaluated);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(80, Profile().Coins);
            Assert.Equal(6, HabitCheckInSingleton.Instance.Count(checkIn => checkIn.Status == HabitCheckInStatus.Missed));
            Assert.Single(GameLedgerEntrySingleton.Instance, entry => entry.Kind == GameLedgerEntryKind.Knockout);
            Assert.Equal(0, Stored(first).CurrentStreak);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldSkipArchivedHabits()
        {
            SeedProfile();
            var habit = SeedHabit(archived: true);

            var summary = await Evaluate();

            Assert.Equal(0, summary.DaysEvaluated);
            Assert.Equal(LastClosedDay.AddDays(-1), Stored(habit).EvaluatedUntil);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldNotTouchDaysThatCanStillBeCheckedIn()
        {
            var habit = SeedHabit(evaluatedUntil: LastClosedDay);

            var summary = await Evaluate();

            Assert.Equal(0, summary.DaysEvaluated);
            Assert.Equal(LastClosedDay, Stored(habit).EvaluatedUntil);
        }

        [Fact]
        public async Task GetUserIdsToEvaluateAsync_ShouldListUsersWithActiveHabitsBehind()
        {
            SeedHabit();
            SeedHabit(userId: OtherUserId, evaluatedUntil: LastClosedDay);
            SeedHabit(userId: new UserId(3), archived: true);

            var userIds = await _service.GetUserIdsToEvaluateAsync(CancellationToken.None);

            Assert.Equal([UserId], userIds);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldPayTheMilestoneAndEarnAFreeze_WhenACleanDayReachesSeven()
        {
            SeedProfile();
            var habit = SeedHabit(kind: HabitKind.Negative);
            foreach (var daysAgo in Enumerable.Range(1, 6))
                SeedCheckIn(habit, LastClosedDay.AddDays(-daysAgo), HabitCheckInStatus.Clean);

            await Evaluate();

            Assert.Equal(7, Stored(habit).CurrentStreak);
            Assert.Equal(1, Profile().StreakFreezes);
            Assert.Single(GameLedgerEntrySingleton.Instance, entry => entry.Kind == GameLedgerEntryKind.StreakMilestone);
            var clean = HabitCheckInSingleton.Instance.Single(checkIn => checkIn.Date == LastClosedDay);
            Assert.Equal(6 + 25, clean.CoinsAwarded);
            Assert.True(clean.FreezeAwarded);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldGiveNoPrize_ForADayAFreezeProtected()
        {
            SeedProfile(streakFreezes: 1);
            var habit = SeedHabit();
            // Six done days before: the frozen 7th keeps the streak but earns no milestone nor freeze.
            foreach (var daysAgo in Enumerable.Range(1, 6))
                SeedCheckIn(habit, LastClosedDay.AddDays(-daysAgo));

            await Evaluate();

            Assert.Equal(7, Stored(habit).CurrentStreak);
            Assert.Equal(0, Profile().StreakFreezes);
            Assert.Equal(0, Profile().Coins);
            Assert.DoesNotContain(GameLedgerEntrySingleton.Instance, entry => entry.Kind == GameLedgerEntryKind.StreakMilestone);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldPayEachRelapseFreeDay_WhenAWeeklyLimitWasKept()
        {
            SeedProfile();
            // Limit 2, two relapses in the week from Monday 2026-09-28; its Sunday (10-04) is judged now.
            var habit = SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 3), kind: HabitKind.Negative, difficulty: HabitDifficulty.Easy,
                frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedCheckIn(habit, new DateOnly(2026, 9, 28), HabitCheckInStatus.Relapse);
            SeedCheckIn(habit, new DateOnly(2026, 9, 30), HabitCheckInStatus.Relapse);

            await Evaluate();

            var clean = HabitCheckInSingleton.Instance.Where(checkIn => checkIn.Status == HabitCheckInStatus.Clean).ToList();
            Assert.Equal(5, clean.Count);
            // 5 clean days × 5 coins, the 1-week streak's +10% (5 → 6 each, 30) and its 7-day milestone (25).
            Assert.Equal(5 * 6 + 25, Profile().Coins);
            Assert.Equal(5 * 10, Profile().TotalXp);
            Assert.Equal(1, Stored(habit).CurrentStreak);
            Assert.Equal(1, Profile().StreakFreezes);
            var rewarded = Assert.Single(clean, checkIn => checkIn.CoinsAwarded > 0);
            Assert.Equal(new DateOnly(2026, 10, 4), rewarded.Date);
        }

        [Fact]
        public async Task EvaluateUserAsync_ShouldPayNothing_WhenAWeeklyLimitWasPassed()
        {
            SeedProfile();
            var habit = SeedHabit(evaluatedUntil: new DateOnly(2026, 10, 3), kind: HabitKind.Negative,
                frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 1);
            SeedCheckIn(habit, new DateOnly(2026, 9, 28), HabitCheckInStatus.Relapse);
            SeedCheckIn(habit, new DateOnly(2026, 9, 30), HabitCheckInStatus.Relapse);

            await Evaluate();

            Assert.Equal(0, Profile().Coins);
            Assert.DoesNotContain(HabitCheckInSingleton.Instance, checkIn => checkIn.Status == HabitCheckInStatus.Clean);
            Assert.Equal(new DateOnly(2026, 10, 5), Stored(habit).EvaluatedUntil);
        }
    }
}
