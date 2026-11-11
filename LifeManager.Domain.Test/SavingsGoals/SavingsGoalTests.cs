using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.SavingsGoals
{
    public class SavingsGoalTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        private static readonly YearMonth CurrentMonth = Month("2026-11");

        private static YearMonth Month(string value)
        {
            Assert.True(YearMonth.TryParse(value, out var month));
            return month;
        }

        private static SavingsGoal CreateGoal(string name = "Viagem", decimal target = 5000m, string? targetMonth = "2027-12", decimal initial = 0m, string? icon = "plane")
        {
            var result = SavingsGoal.Create(1, name, target, targetMonth is null ? null : Month(targetMonth), initial, icon, CurrentMonth, Now);
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SavingsGoalName_ShouldReturnFailure_WhenValueIsNullOrWhiteSpace(string? value)
        {
            var result = SavingsGoalName.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void SavingsGoalName_ShouldReturnFailure_WhenValueIsLongerThanMaxLength()
        {
            var result = SavingsGoalName.Create(new string('a', SavingsGoalName.MaxLength + 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void SavingsGoalName_ShouldTrimAndNormalize()
        {
            var result = SavingsGoalName.Create("  Reserva de Emergência ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Reserva de Emergência", result.Value!.Value);
            Assert.Equal("reserva de emergencia", result.Value.NormalizedValue);
        }

        [Fact]
        public void Create_ShouldReturnActiveGoal_WithTrimmedValues()
        {
            var result = SavingsGoal.Create(1, " Viagem ", 5000m, Month("2027-12"), 300.5m, "  plane ", CurrentMonth, Now);

            Assert.True(result.IsSuccess);
            var goal = result.Value!;
            Assert.Equal("Viagem", goal.Name.Value);
            Assert.Equal("viagem", goal.NormalizedName);
            Assert.Equal(5000m, goal.TargetAmount);
            Assert.Equal(Month("2027-12"), goal.TargetMonth);
            Assert.Equal(new DateOnly(2027, 12, 1), goal.TargetMonthStart);
            Assert.Equal(300.5m, goal.InitialAmount);
            Assert.Equal("plane", goal.Icon);
            Assert.Equal(Now, goal.CreatedAt);
            Assert.False(goal.IsArchived);
            Assert.Null(goal.CompletedAt);
        }

        [Fact]
        public void Create_ShouldAcceptNoDeadlineAndBlankIcon()
        {
            var result = SavingsGoal.Create(1, "Reserva", 20000m, null, 0m, "  ", CurrentMonth, Now);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value!.TargetMonth);
            Assert.Null(result.Value.TargetMonthStart);
            Assert.Null(result.Value.Icon);
        }

        [Fact]
        public void Create_ShouldAcceptTheCurrentMonthAsDeadline()
        {
            var result = SavingsGoal.Create(1, "Presente", 200m, CurrentMonth, 0m, null, CurrentMonth, Now);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenTargetMonthIsInThePast()
        {
            var result = SavingsGoal.Create(1, "Viagem", 5000m, Month("2026-10"), 0m, null, CurrentMonth, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.TargetMonthInPast, result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(10.001)]
        [InlineData(1_000_000_000_000)]
        public void Create_ShouldReturnFailure_WhenTargetAmountIsInvalid(decimal target)
        {
            var result = SavingsGoal.Create(1, "Viagem", target, null, 0m, null, CurrentMonth, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.InvalidTargetAmount, result.Error);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.005)]
        [InlineData(1_000_000_000_000)]
        public void Create_ShouldReturnFailure_WhenInitialAmountIsInvalid(decimal initial)
        {
            var result = SavingsGoal.Create(1, "Viagem", 5000m, null, initial, null, CurrentMonth, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.InvalidInitialAmount, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenIconIsTooLong()
        {
            var result = SavingsGoal.Create(1, "Viagem", 5000m, null, 0m, new string('a', SavingsGoal.IconMaxLength + 1), CurrentMonth, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.IconTooLong, result.Error);
        }

        [Fact]
        public void Update_ShouldChangeEveryField()
        {
            var goal = CreateGoal();

            var result = goal.Update("Viagem ao Japão", 8000m, null, 1000m, "globe", CurrentMonth);

            Assert.True(result.IsSuccess);
            Assert.Equal("Viagem ao Japão", goal.Name.Value);
            Assert.Equal("viagem ao japao", goal.NormalizedName);
            Assert.Equal(8000m, goal.TargetAmount);
            Assert.Null(goal.TargetMonth);
            Assert.Equal(1000m, goal.InitialAmount);
            Assert.Equal("globe", goal.Icon);
        }

        [Fact]
        public void Update_ShouldReturnFailure_WhenTargetMonthIsInThePast()
        {
            var goal = CreateGoal();

            var result = goal.Update("Viagem", 5000m, Month("2026-10"), 0m, null, CurrentMonth);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.TargetMonthInPast, result.Error);
            Assert.Equal(Month("2027-12"), goal.TargetMonth);
        }

        [Fact]
        public void Update_ShouldReturnFailure_WhenArchived()
        {
            var goal = CreateGoal();
            goal.Archive(Now);

            var result = goal.Update("Viagem", 5000m, null, 0m, null, CurrentMonth);

            Assert.False(result.IsSuccess);
            Assert.Equal(SavingsGoalErrors.Archived, result.Error);
        }

        [Fact]
        public void ArchiveAndRestore_ShouldToggleArchivedAt()
        {
            var goal = CreateGoal();

            Assert.True(goal.Archive(Now).IsSuccess);
            Assert.Equal(Now, goal.ArchivedAt);
            Assert.Equal(SavingsGoalErrors.AlreadyArchived, goal.Archive(Now).Error);

            Assert.True(goal.Restore().IsSuccess);
            Assert.Null(goal.ArchivedAt);
            Assert.Equal(SavingsGoalErrors.NotArchived, goal.Restore().Error);
        }

        [Fact]
        public void EnsureCanLink_ShouldReturnFailure_WhenArchived()
        {
            var goal = CreateGoal();
            Assert.True(goal.EnsureCanLink().IsSuccess);

            goal.Archive(Now);

            Assert.Equal(SavingsGoalErrors.Archived, goal.EnsureCanLink().Error);
        }

        [Fact]
        public void MarkCompleted_ShouldKeepTheFirstDate()
        {
            var goal = CreateGoal();

            Assert.True(goal.MarkCompleted(new DateOnly(2026, 11, 11)));
            Assert.False(goal.MarkCompleted(new DateOnly(2026, 12, 1)));

            Assert.Equal(new DateOnly(2026, 11, 11), goal.CompletedAt);
        }

        [Fact]
        public void FromPersistence_ShouldRehydrateAPastDeadlineWithoutValidating()
        {
            var goal = SavingsGoal.FromPersistence(
                7, 1, "Antigo", 100m, new DateOnly(2020, 1, 1), 0m, null, Now, archivedAt: null, completedAt: new DateOnly(2020, 1, 15));

            Assert.Equal(new SavingsGoalId(7), goal.Id);
            Assert.Equal(Month("2020-01"), goal.TargetMonth);
            Assert.Equal(new DateOnly(2020, 1, 15), goal.CompletedAt);
        }
    }
}
