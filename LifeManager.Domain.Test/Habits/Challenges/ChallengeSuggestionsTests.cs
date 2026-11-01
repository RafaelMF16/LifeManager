using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using static LifeManager.Domain.Test.Habits.Challenges.ChallengeTestData;

namespace LifeManager.Domain.Test.Habits.Challenges
{
    public class ChallengeSuggestionsTests
    {
        private static ChallengeCandidate Candidate(
            Habit habit, decimal? frequencyBaseline = null, decimal? volumeBaseline = null, int maxFrequency = 7,
            int possibleDays = 7, int frequencyProgress = 0, int volumeProgress = 0)
            => new(habit, frequencyBaseline, volumeBaseline, possibleDays, maxFrequency, frequencyProgress, volumeProgress);

        private static Habit WithId(int id, HabitDifficulty difficulty = HabitDifficulty.Medium, int? targetValue = null, int? targetStep = null,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily, int? timesPerWeek = null)
        {
            var habit = Habit(difficulty: difficulty, targetValue: targetValue, targetStep: targetStep, frequencyType: frequencyType, timesPerWeek: timesPerWeek);
            habit.AssignId(id);

            return habit;
        }

        [Fact]
        public void Suggest_ShouldAskOneDayMoreThanTheAverage()
        {
            var suggestion = Assert.Single(ChallengeRules.Suggest([Candidate(WithId(1), frequencyBaseline: 3.25m)], 0));

            Assert.Equal(ChallengeType.Frequency, suggestion.Type);
            Assert.Equal(5, suggestion.Target);
            Assert.Equal(3.25m, suggestion.Baseline);
            Assert.Equal(ChallengeEffort.Stretch, suggestion.Effort);
            Assert.Equal(new ChallengeReward(60, 120), suggestion.Reward);
        }

        [Fact]
        public void Suggest_ShouldStopAtTheMostDays_AndSkipAHabitAlreadyThere()
        {
            Assert.Equal(7, ChallengeRules.Suggest([Candidate(WithId(1), frequencyBaseline: 6.5m)], 0).Single().Target);
            Assert.Empty(ChallengeRules.Suggest([Candidate(WithId(1), frequencyBaseline: 7m)], 0));
        }

        [Fact]
        public void Suggest_ShouldPreferVolume_RoundedUpToTheStep()
        {
            var habit = WithId(1, targetValue: 2000, targetStep: 250);

            var suggestion = Assert.Single(ChallengeRules.Suggest([Candidate(habit, frequencyBaseline: 4, volumeBaseline: 9000)], 0));

            Assert.Equal(ChallengeType.Volume, suggestion.Type);
            Assert.Equal(10_500, suggestion.Target); // 9000 × 1.15 = 10 350 → 10 500
        }

        [Fact]
        public void Suggest_ShouldSkipATargetAlreadyReachedThisWeek()
        {
            Assert.Empty(ChallengeRules.Suggest([Candidate(WithId(1), frequencyBaseline: 3, frequencyProgress: 4)], 0));
        }

        [Fact]
        public void Suggest_ShouldSkipAHabitWithoutAClosedWeek()
        {
            Assert.Empty(ChallengeRules.Suggest([Candidate(WithId(1))], 0));
        }

        [Fact]
        public void Suggest_ShouldPutTheMostForgottenHabitFirst_ThenTheHardest_AndFillWhatTheWeekHasLeft()
        {
            var steady = Candidate(WithId(1), frequencyBaseline: 6);
            var forgotten = Candidate(WithId(2), frequencyBaseline: 2);
            var hard = Candidate(WithId(3, HabitDifficulty.Hard), frequencyBaseline: 2);
            var weekly = Candidate(WithId(4, frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3), frequencyBaseline: 3);

            var suggestions = ChallengeRules.Suggest([steady, forgotten, hard, weekly], 1);

            Assert.Equal([3, 2], suggestions.Select(suggestion => suggestion.HabitId.Value));
            Assert.Empty(ChallengeRules.Suggest([steady], 3));
        }
    }
}
