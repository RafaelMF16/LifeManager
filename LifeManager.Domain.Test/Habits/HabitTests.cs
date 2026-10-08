using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Test.Habits
{
    public class HabitTests
    {
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static Result<Habit> Create(
            HabitKind kind = HabitKind.Positive,
            HabitDifficulty difficulty = HabitDifficulty.Medium,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            string? name = "Read",
            string? description = null,
            string? trigger = null)
            => Habit.Create(1, name, description, trigger, kind, difficulty, frequencyType, weekDays, timesPerWeek, Today, Now);

        private static Habit Archived()
        {
            var habit = Create().Value!;
            habit.Archive(Now);
            return habit;
        }

        [Fact]
        public void Create_ShouldStartTodayWithEmptyStreakAndCursorOnYesterday()
        {
            var result = Create(description: "  10 pages  ", trigger: " After dinner ");

            Assert.True(result.IsSuccess);
            var habit = result.Value;
            Assert.Equal("Read", habit.Name.Value);
            Assert.Equal("read", habit.NormalizedName);
            Assert.Equal("10 pages", habit.Description!.Value);
            Assert.Equal("After dinner", habit.Trigger!.Value);
            Assert.Equal(Today, habit.StartDate);
            Assert.Equal(Today.AddDays(-1), habit.EvaluatedUntil);
            Assert.Equal(Now, habit.CreatedAt);
            Assert.Equal(0, habit.CurrentStreak);
            Assert.Equal(0, habit.LongestStreak);
            Assert.False(habit.IsArchived);
        }

        [Fact]
        public void Create_ShouldKeepFrequencyColumns_WhenFrequencyIsWeekDays()
        {
            var habit = Create(frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday | HabitWeekDays.Friday).Value!;

            Assert.Equal(HabitFrequencyType.WeekDays, habit.FrequencyType);
            Assert.Equal(HabitWeekDays.Monday | HabitWeekDays.Friday, habit.WeekDays);
            Assert.Null(habit.TimesPerWeek);
            Assert.Equal(HabitFrequencyType.WeekDays, habit.Frequency.Type);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenKindIsUndefined()
        {
            Assert.Equal(HabitErrors.InvalidKind, Create(kind: (HabitKind)9).Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenDifficultyIsUndefined()
        {
            Assert.Equal(HabitErrors.InvalidDifficulty, Create(difficulty: (HabitDifficulty)9).Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenNameIsBlank()
        {
            Assert.Equal(HabitErrors.NameIsNullOrWhiteSpace, Create(name: " ").Error);
        }

        [Fact]
        public void Create_ShouldAcceptNegativeHabit_WhenFrequencyIsDaily()
        {
            var result = Create(kind: HabitKind.Negative);

            Assert.True(result.IsSuccess);
            Assert.Equal(HabitKind.Negative, result.Value.Kind);
        }

        [Theory]
        [InlineData(HabitFrequencyType.WeekDays, HabitWeekDays.Monday, null)]
        [InlineData(HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, 3)]
        public void Create_ShouldAcceptNegativeHabit_WithAnyFrequency(HabitFrequencyType frequencyType, HabitWeekDays weekDays, int? timesPerWeek)
        {
            var result = Create(kind: HabitKind.Negative, frequencyType: frequencyType, weekDays: weekDays, timesPerWeek: timesPerWeek);

            Assert.True(result.IsSuccess);
            Assert.Equal(frequencyType, result.Value.FrequencyType);
            Assert.Equal(weekDays, result.Value.WeekDays);
            Assert.Equal(timesPerWeek, result.Value.TimesPerWeek);
        }

        [Fact]
        public void Update_ShouldChangeEverythingButKind()
        {
            var habit = Create().Value!;

            var result = habit.Update("Read más", "desc", "trigger", HabitDifficulty.Hard, HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, 4);

            Assert.True(result.IsSuccess);
            Assert.Equal("Read más", habit.Name.Value);
            Assert.Equal("read mas", habit.NormalizedName);
            Assert.Equal("desc", habit.Description!.Value);
            Assert.Equal("trigger", habit.Trigger!.Value);
            Assert.Equal(HabitDifficulty.Hard, habit.Difficulty);
            Assert.Equal(HabitFrequencyType.TimesPerWeek, habit.FrequencyType);
            Assert.Equal(4, habit.TimesPerWeek);
            Assert.Equal(HabitKind.Positive, habit.Kind);
        }

        [Fact]
        public void Update_ShouldClearOptionalTexts_WhenTheyAreBlank()
        {
            var habit = Create(description: "desc", trigger: "trigger").Value!;

            habit.Update("Read", "", null, HabitDifficulty.Easy, HabitFrequencyType.Daily, HabitWeekDays.None, null);

            Assert.Null(habit.Description);
            Assert.Null(habit.Trigger);
        }

        [Fact]
        public void Update_ShouldReturnFailureAndKeepValues_WhenValuesAreInvalid()
        {
            var habit = Create().Value!;

            var result = habit.Update("Other", null, null, HabitDifficulty.Hard, HabitFrequencyType.WeekDays, HabitWeekDays.None, null);

            Assert.Equal(HabitErrors.WeekDaysRequired, result.Error);
            Assert.Equal("Read", habit.Name.Value);
            Assert.Equal(HabitDifficulty.Medium, habit.Difficulty);
        }

        [Fact]
        public void Update_ShouldChangeTheFrequency_WhenHabitIsNegative()
        {
            var habit = Create(kind: HabitKind.Negative).Value!;

            var result = habit.Update("Video games", null, null, HabitDifficulty.Hard, HabitFrequencyType.TimesPerWeek, HabitWeekDays.None, 2);

            Assert.True(result.IsSuccess);
            Assert.Equal(HabitFrequencyType.TimesPerWeek, habit.FrequencyType);
            Assert.Equal(2, habit.TimesPerWeek);
            Assert.Equal(HabitKind.Negative, habit.Kind);
        }

        [Fact]
        public void Update_ShouldReturnConflict_WhenHabitIsArchived()
        {
            var habit = Archived();

            var result = habit.Update("Other", null, null, HabitDifficulty.Hard, HabitFrequencyType.Daily, HabitWeekDays.None, null);

            Assert.Equal(HabitErrors.Archived, result.Error);
            Assert.Equal("Read", habit.Name.Value);
        }

        [Fact]
        public void Archive_ShouldSetArchivedAt()
        {
            var habit = Create().Value!;

            var result = habit.Archive(Now);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now, habit.ArchivedAt);
            Assert.True(habit.IsArchived);
        }

        [Fact]
        public void Archive_ShouldReturnConflict_WhenAlreadyArchived()
        {
            Assert.Equal(HabitErrors.AlreadyArchived, Archived().Archive(Now).Error);
        }

        [Fact]
        public void Restore_ShouldReturnConflict_WhenNotArchived()
        {
            Assert.Equal(HabitErrors.NotArchived, Create().Value!.Restore(Today).Error);
        }

        [Fact]
        public void Restore_ShouldResetStreakKeepRecordAndSkipArchivedDays()
        {
            var habit = Habit.FromPersistence(
                1, 1, "Read", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null,
                startDate: new DateOnly(2026, 9, 1),
                createdAt: Now,
                archivedAt: Now,
                currentStreak: 12,
                longestStreak: 20,
                evaluatedUntil: new DateOnly(2026, 9, 20));
            var restoreDay = new DateOnly(2026, 10, 10);

            var result = habit.Restore(restoreDay);

            Assert.True(result.IsSuccess);
            Assert.False(habit.IsArchived);
            Assert.Equal(0, habit.CurrentStreak);
            Assert.Equal(20, habit.LongestStreak);
            Assert.Equal(restoreDay.AddDays(-1), habit.EvaluatedUntil);
        }

        [Fact]
        public void Restore_ShouldNotMoveCursorBack_WhenItIsAlreadyPastYesterday()
        {
            var habit = Archived();
            var cursor = habit.EvaluatedUntil;

            habit.Restore(Today.AddDays(-5));

            Assert.Equal(cursor, habit.EvaluatedUntil);
        }
    }
}
