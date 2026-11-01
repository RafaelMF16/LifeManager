using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using static LifeManager.Domain.Test.Habits.Challenges.ChallengeTestData;

namespace LifeManager.Domain.Test.Habits.Challenges
{
    public class WeeklyChallengeTests
    {
        private static readonly DateOnly Tuesday = Monday.AddDays(1);

        private static ChallengeDraft Draft(
            Habit? habit = null, ChallengeType type = ChallengeType.Frequency, int target = 5, DateOnly? weekStart = null,
            DateOnly? today = null, int progress = 0, decimal? baseline = 3, IReadOnlyCollection<DateOnly>? doneDates = null)
            => new(habit ?? Habit(), type, target, weekStart ?? Monday, today ?? Tuesday, progress, baseline, doneDates ?? []);

        [Fact]
        public void Create_ShouldWorkOutTheEffortAndTheReward()
        {
            var challenge = WeeklyChallenge.Create(Draft(), Now).Value!;

            Assert.Equal(ChallengeStatus.Active, challenge.Status);
            Assert.Equal(Monday, challenge.WeekStart);
            Assert.Equal(3m, challenge.Baseline);
            Assert.Equal(ChallengeEffort.Stretch, challenge.Effort);
            Assert.Equal(60, challenge.RewardCoins);
            Assert.Equal(120, challenge.RewardXp);
            Assert.Null(challenge.CompletedAt);
        }

        [Fact]
        public void Create_ShouldRefuseAHabitThatIsNotActiveOrNotToBuild()
        {
            Assert.Equal(ChallengeErrors.HabitNotEligible, WeeklyChallenge.Create(Draft(Habit(kind: HabitKind.Negative)), Now).Error);
            Assert.Equal(ChallengeErrors.HabitNotEligible, WeeklyChallenge.Create(Draft(Habit(archivedAt: Now)), Now).Error);
        }

        [Fact]
        public void Create_ShouldRefuseVolume_ForAHabitWithoutADailyTarget()
        {
            Assert.Equal(ChallengeErrors.NotMeasurable, WeeklyChallenge.Create(Draft(type: ChallengeType.Volume, target: 100), Now).Error);
        }

        [Fact]
        public void Create_ShouldRefuseAnUnknownType()
        {
            Assert.Equal(ChallengeErrors.InvalidType, WeeklyChallenge.Create(Draft(type: (ChallengeType)9), Now).Error);
        }

        [Fact]
        public void Create_ShouldRefuseTheCurrentWeek_AfterWednesday()
        {
            Assert.Equal(ChallengeErrors.CreationWindowClosed, WeeklyChallenge.Create(Draft(today: Monday.AddDays(3)), Now).Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        public void Create_ShouldRefuseAFrequencyOutOfRange(int target)
        {
            Assert.Equal(ChallengeErrors.InvalidTarget, WeeklyChallenge.Create(Draft(target: target, weekStart: Monday.AddDays(7)), Now).Error);
        }

        [Fact]
        public void Create_ShouldTakeAWeeklyHabitAboveItsTimesPerWeek()
        {
            var habit = Habit(frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3);

            Assert.True(WeeklyChallenge.Create(Draft(habit, target: 5, weekStart: Monday.AddDays(7)), Now).IsSuccess);
        }

        [Fact]
        public void Create_ShouldLimitTheCurrentWeek_ToTheDaysLeft()
        {
            // Wednesday: Monday is gone (not done), Tuesday to Sunday are open.
            var wednesday = Monday.AddDays(2);

            Assert.Equal(ChallengeErrors.InvalidTarget, WeeklyChallenge.Create(Draft(target: 7, today: wednesday), Now).Error);
            Assert.True(WeeklyChallenge.Create(Draft(target: 6, today: wednesday), Now).IsSuccess);
            Assert.True(WeeklyChallenge.Create(Draft(target: 7, today: wednesday, progress: 1, doneDates: [Monday]), Now).IsSuccess);
        }

        [Fact]
        public void Create_ShouldRefuseATargetAlreadyReached_InTheCurrentWeek()
        {
            Assert.Equal(ChallengeErrors.TargetAlreadyReached, WeeklyChallenge.Create(Draft(target: 2, progress: 2, doneDates: [Monday, Tuesday]), Now).Error);
        }

        [Fact]
        public void Create_ShouldCapVolume_AtThreeTimesAWeekOfTheTarget()
        {
            var habit = Habit(targetValue: 1000);

            Assert.True(WeeklyChallenge.Create(Draft(habit, ChallengeType.Volume, 21_000, Monday.AddDays(7), baseline: null), Now).IsSuccess);
            Assert.Equal(ChallengeErrors.InvalidTarget, WeeklyChallenge.Create(Draft(habit, ChallengeType.Volume, 21_001, Monday.AddDays(7)), Now).Error);
        }

        [Fact]
        public void Complete_ShouldOnlyMoveAnActiveChallenge()
        {
            var challenge = WeeklyChallenge.Create(Draft(), Now).Value!;

            Assert.True(challenge.Complete(Now).IsSuccess);
            Assert.Equal(ChallengeStatus.Completed, challenge.Status);
            Assert.Equal(Now, challenge.CompletedAt);
            Assert.Equal(ChallengeErrors.NotActive, challenge.Complete(Now).Error);
            Assert.Equal(ChallengeErrors.NotActive, challenge.Fail().Error);
            Assert.Equal(ChallengeErrors.NotActive, challenge.EnsureCanDelete().Error);
        }

        [Fact]
        public void FailAndCancel_ShouldEndAnActiveChallenge()
        {
            var failed = WeeklyChallenge.Create(Draft(), Now).Value!;
            var cancelled = WeeklyChallenge.Create(Draft(), Now).Value!;

            Assert.True(failed.Fail().IsSuccess);
            Assert.True(cancelled.Cancel().IsSuccess);
            Assert.Equal(ChallengeStatus.Failed, failed.Status);
            Assert.Equal(ChallengeStatus.Cancelled, cancelled.Status);
            Assert.True(WeeklyChallenge.Create(Draft(), Now).Value!.EnsureCanDelete().IsSuccess);
        }
    }
}
