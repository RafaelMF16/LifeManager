using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Ranking;

namespace LifeManager.Domain.Test.Ranking
{
    public class WeeklyScoreTests
    {
        // Monday 2026-11-02 to Sunday 2026-11-08.
        private static readonly DateOnly Monday = new(2026, 11, 2);
        private static readonly DateOnly Sunday = Monday.AddDays(6);

        private static ScoredHabit Build(int id, DateOnly? start = null, DateOnly? archivedOn = null, HabitFrequency? frequency = null)
            => new(id, HabitKind.Positive, frequency ?? HabitFrequency.Daily, start ?? Monday.AddDays(-30), archivedOn);

        private static ScoredHabit Avoid(int id, HabitFrequency? frequency = null)
            => new(id, HabitKind.Negative, frequency ?? HabitFrequency.Daily, Monday.AddDays(-30), null);

        private static ScoredCheckIn CheckIn(int habitId, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => new(habitId, date, status);

        private static WeeklyScoreDay Day(WeeklyScoreResult result, DateOnly date)
            => result.Days.Single(day => day.Date == date);

        [Fact]
        public void Calculate_ShouldReturnSevenEmptyDays_WhenThereIsNothing()
        {
            var result = WeeklyScore.Calculate([], [], Monday, Sunday);

            Assert.Equal(Monday, result.WeekStart);
            Assert.Equal(7, result.Days.Count);
            Assert.Equal(Enumerable.Range(0, 7).Select(Monday.AddDays), result.Days.Select(day => day.Date));
            Assert.Equal(0, result.Points);
            Assert.Equal(0, result.PerfectDays);
            Assert.All(result.Days, day => Assert.False(day.Perfect));
        }

        [Fact]
        public void Calculate_ShouldGiveTenPointsPerKeptHabitPlusThePerfectBonus_WhenEveryDueHabitIsDone()
        {
            var result = WeeklyScore.Calculate([Build(1), Build(2)], [CheckIn(1, Monday), CheckIn(2, Monday)], Monday, Sunday);

            var monday = Day(result, Monday);
            Assert.Equal(2, monday.Kept);
            Assert.True(monday.Perfect);
            Assert.Equal(30, monday.Points);
        }

        [Fact]
        public void Calculate_ShouldNotBePerfect_WhenAHabitToBuildIsStillPending()
        {
            var result = WeeklyScore.Calculate([Build(1), Build(2)], [CheckIn(1, Monday)], Monday, Sunday);

            var monday = Day(result, Monday);
            Assert.False(monday.Perfect);
            Assert.Equal(10, monday.Points);
        }

        [Fact]
        public void Calculate_ShouldCapTheKeptHabitsAtFivePerDay()
        {
            var habits = Enumerable.Range(1, 8).Select(id => Build(id)).ToList();
            var checkIns = habits.Select(habit => CheckIn(habit.HabitId, Monday)).ToList();

            var monday = Day(WeeklyScore.Calculate(habits, checkIns, Monday, Sunday), Monday);

            Assert.Equal(5, monday.Kept);
            Assert.True(monday.Perfect);
            Assert.Equal(60, monday.Points);
        }

        [Fact]
        public void Calculate_ShouldCountCleanDays_AndBreakThePerfectDayOnARelapse()
        {
            var habits = new[] { Build(1), Avoid(2) };
            var checkIns = new[]
            {
                CheckIn(1, Monday), CheckIn(2, Monday, HabitCheckInStatus.Clean),
                CheckIn(1, Monday.AddDays(1)), CheckIn(2, Monday.AddDays(1), HabitCheckInStatus.Relapse)
            };

            var result = WeeklyScore.Calculate(habits, checkIns, Monday, Sunday);

            Assert.Equal(30, Day(result, Monday).Points);
            var tuesday = Day(result, Monday.AddDays(1));
            Assert.Equal(1, tuesday.Kept);
            Assert.False(tuesday.Perfect);
            Assert.Equal(10, tuesday.Points);
        }

        [Fact]
        public void Calculate_ShouldStillBePerfect_WhenAHabitToAvoidHasNoRecordYet()
        {
            // The clean day is only written by the day close; until then, no relapse is enough.
            var result = WeeklyScore.Calculate([Build(1), Avoid(2)], [CheckIn(1, Monday)], Monday, Sunday);

            Assert.True(Day(result, Monday).Perfect);
            Assert.Equal(20, Day(result, Monday).Points);
        }

        [Fact]
        public void Calculate_ShouldLeaveAFrozenDayOutOfThePerfectDay()
        {
            var result = WeeklyScore.Calculate(
                [Build(1), Build(2)],
                [CheckIn(1, Monday), CheckIn(2, Monday, HabitCheckInStatus.Frozen)],
                Monday, Sunday);

            var monday = Day(result, Monday);
            Assert.Equal(1, monday.Kept);
            Assert.True(monday.Perfect);
            Assert.Equal(20, monday.Points);
        }

        [Fact]
        public void Calculate_ShouldNotBePerfect_WhenOnlyFrozenHabitsWereDue()
        {
            var result = WeeklyScore.Calculate([Build(1)], [CheckIn(1, Monday, HabitCheckInStatus.Frozen)], Monday, Sunday);

            Assert.False(Day(result, Monday).Perfect);
            Assert.Equal(0, Day(result, Monday).Points);
        }

        [Fact]
        public void Calculate_ShouldCountTimesPerWeekHabitsAsKept_ButLeaveThemOutOfThePerfectDay()
        {
            var weekly = Build(1, frequency: HabitFrequency.Create(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, 3).Value!);

            var withDaily = WeeklyScore.Calculate([weekly, Build(2)], [CheckIn(2, Monday)], Monday, Sunday);
            var alone = WeeklyScore.Calculate([weekly], [CheckIn(1, Monday)], Monday, Sunday);

            Assert.True(Day(withDaily, Monday).Perfect);
            Assert.Equal(1, Day(alone, Monday).Kept);
            Assert.False(Day(alone, Monday).Perfect);
            Assert.Equal(10, Day(alone, Monday).Points);
        }

        [Fact]
        public void Calculate_ShouldOnlyExpectASetDaysHabitOnItsDays()
        {
            var setDays = Build(1, frequency: HabitFrequency.Create(HabitFrequencyType.WeekDays, HabitWeekDays.Monday, null).Value!);

            var result = WeeklyScore.Calculate([setDays, Build(2)], [CheckIn(2, Monday), CheckIn(2, Monday.AddDays(1))], Monday, Sunday);

            Assert.False(Day(result, Monday).Perfect);
            Assert.True(Day(result, Monday.AddDays(1)).Perfect);
        }

        [Fact]
        public void Calculate_ShouldNotExpectAHabitBeforeItsStartDate()
        {
            var result = WeeklyScore.Calculate([Build(1), Build(2, start: Monday.AddDays(3))], [CheckIn(1, Monday)], Monday, Sunday);

            Assert.True(Day(result, Monday).Perfect);
        }

        [Fact]
        public void Calculate_ShouldStillExpectAHabitOnTheDayItWasArchived_ButNotAfter()
        {
            var archived = Build(2, archivedOn: Monday.AddDays(1));
            var checkIns = new[] { CheckIn(1, Monday.AddDays(1)), CheckIn(1, Monday.AddDays(2)) };

            var result = WeeklyScore.Calculate([Build(1), archived], checkIns, Monday, Sunday);

            Assert.False(Day(result, Monday.AddDays(1)).Perfect);
            Assert.True(Day(result, Monday.AddDays(2)).Perfect);
        }

        [Fact]
        public void Calculate_ShouldGiveNothingAfterUpTo()
        {
            var checkIns = Enumerable.Range(0, 7).Select(offset => CheckIn(1, Monday.AddDays(offset))).ToList();

            var result = WeeklyScore.Calculate([Build(1)], checkIns, Monday, Monday.AddDays(2));

            Assert.Equal(60, result.Points);
            Assert.Equal(3, result.PerfectDays);
            Assert.Equal(3, result.KeptDays);
            Assert.All(result.Days.Skip(3), day => Assert.Equal(0, day.Points));
        }

        [Fact]
        public void Calculate_ShouldIgnoreCheckInsOutsideTheWeek()
        {
            var result = WeeklyScore.Calculate([Build(1)], [CheckIn(1, Monday.AddDays(-1)), CheckIn(1, Sunday.AddDays(1))], Monday, Sunday);

            Assert.Equal(0, result.Points);
        }

        [Fact]
        public void Calculate_ShouldSumTheWeek_WithTheMaximumOf420()
        {
            var habits = Enumerable.Range(1, 5).Select(id => Build(id)).ToList();
            var checkIns = habits.SelectMany(habit => Enumerable.Range(0, 7).Select(offset => CheckIn(habit.HabitId, Monday.AddDays(offset)))).ToList();

            var result = WeeklyScore.Calculate(habits, checkIns, Monday, Sunday);

            Assert.Equal(420, result.Points);
            Assert.Equal(7, result.PerfectDays);
            Assert.Equal(35, result.KeptDays);
        }

        private static ScoredChallenge Challenge(
            int habitId, int target, ChallengeEffort effort = ChallengeEffort.Stretch, ChallengeStatus status = ChallengeStatus.Completed,
            ChallengeType type = ChallengeType.Frequency)
            => new(habitId, type, target, effort, status);

        [Fact]
        public void Calculate_ShouldAddFifteenPointsPerReachedSteadyOrStretchChallenge()
        {
            var checkIns = new[] { CheckIn(1, Monday), CheckIn(1, Monday.AddDays(1)), CheckIn(2, Monday) };

            var result = WeeklyScore.Calculate(
                [Build(1), Build(2)], checkIns, Monday, Sunday,
                [Challenge(1, 2), Challenge(2, 1, ChallengeEffort.Steady, ChallengeStatus.Active), Challenge(2, 1, ChallengeEffort.Light)]);

            Assert.Equal(30, result.ChallengePoints);
            Assert.Equal(WeeklyScore.Calculate([Build(1), Build(2)], checkIns, Monday, Sunday).Points + 30, result.Points);
        }

        [Fact]
        public void Calculate_ShouldNotScoreAChallengeBelowItsTargetFailedOrCancelled()
        {
            var checkIns = new[] { CheckIn(1, Monday), CheckIn(1, Monday.AddDays(1), HabitCheckInStatus.Frozen) };

            var result = WeeklyScore.Calculate(
                [Build(1)], checkIns, Monday, Sunday,
                [Challenge(1, 2), Challenge(1, 1, status: ChallengeStatus.Failed), Challenge(1, 1, status: ChallengeStatus.Cancelled)]);

            Assert.Equal(0, result.ChallengePoints);
        }

        [Fact]
        public void Calculate_ShouldTakeThePointsBack_WhenTheCheckInThatCompletedTheChallengeIsUndone()
        {
            // Completed and paid, but the stored check-ins no longer reach the target.
            var result = WeeklyScore.Calculate([Build(1)], [CheckIn(1, Monday)], Monday, Sunday, [Challenge(1, 2)]);

            Assert.Equal(0, result.ChallengePoints);
        }

        [Fact]
        public void Calculate_ShouldScoreAtMostThreeChallenges()
        {
            var result = WeeklyScore.Calculate(
                [Build(1)], [CheckIn(1, Monday)], Monday, Sunday,
                [Challenge(1, 1), Challenge(1, 1), Challenge(1, 1), Challenge(1, 1)]);

            Assert.Equal(45, result.ChallengePoints);
        }

        [Fact]
        public void Calculate_ShouldCountAVolumeChallengeFromTheAmountsUpToTheLastScoredDay()
        {
            var progress = new[] { new ScoredProgress(1, Monday, 600), new ScoredProgress(1, Monday.AddDays(2), 500), new ScoredProgress(2, Monday, 900) };
            var volume = Challenge(1, 1000, type: ChallengeType.Volume);

            Assert.Equal(15, WeeklyScore.Calculate([Build(1)], [], Monday, Sunday, [volume], progress).ChallengePoints);
            Assert.Equal(0, WeeklyScore.Calculate([Build(1)], [], Monday, Monday.AddDays(1), [volume], progress).ChallengePoints);
        }
    }
}
