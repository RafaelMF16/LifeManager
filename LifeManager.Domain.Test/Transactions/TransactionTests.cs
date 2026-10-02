using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Errors;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionTests
    {
        private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

        private static MonthlySummary March()
        {
            var monthlySummary = MonthlySummary.Create(1, 3, CurrentYear).Value!;
            monthlySummary.AssignId(7);

            return monthlySummary;
        }

        private static DateOnly MarchDay(int day) => new(CurrentYear, 3, day);

        [Fact]
        public void Create_ShouldReturnTransaction_WhenTheValuesAreValid()
        {
            var result = Transaction.Create(MoneyFlowType.Expense, 2, 50.25m, "  Farmácia  ", MarchDay(10), March());

            Assert.True(result.IsSuccess);
            var transaction = result.Value;
            Assert.Null(transaction.Id);
            Assert.Equal(MoneyFlowType.Expense, transaction.Type);
            Assert.Equal(2, transaction.CategoryId.Value);
            Assert.Equal(50.25m, transaction.Amount.Value);
            Assert.Equal("Farmácia", transaction.Description.Value);
            Assert.Equal("farmacia", transaction.NormalizedDescription);
            Assert.Equal(MarchDay(10), transaction.TransactionDate);
            Assert.Equal(7, transaction.MonthlySummaryId.Value);
        }

        [Theory]
        [InlineData(MoneyFlowType.Expense, -50)]
        [InlineData(MoneyFlowType.Income, 50)]
        public void Create_ShouldSignAmountByType(MoneyFlowType type, decimal expectedSignedAmount)
        {
            var transaction = Transaction.Create(type, 1, 50m, "description", MarchDay(1), March()).Value!;

            Assert.Equal(expectedSignedAmount, transaction.SignedAmount);
            Assert.Equal(50m, transaction.Amount.Value);
        }

        [Fact]
        public void Create_ShouldReturnInvalidType_WhenTypeIsNotDefined()
        {
            var result = Transaction.Create((MoneyFlowType)99, 1, 50m, "description", MarchDay(1), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.InvalidType, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnDescriptionError_WhenDescriptionIsMissing()
        {
            var result = Transaction.Create(MoneyFlowType.Expense, 1, 50m, " ", MarchDay(1), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DescriptionIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnAmountError_WhenAmountIsNotPositive()
        {
            var result = Transaction.Create(MoneyFlowType.Expense, 1, 0m, "description", MarchDay(1), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.AmountNotPositive, result.Error);
        }

        [Theory]
        [InlineData(2, 28)]
        [InlineData(4, 1)]
        public void Create_ShouldReturnDateOutsideMonth_WhenDateIsInAnotherMonth(int month, int day)
        {
            var result = Transaction.Create(MoneyFlowType.Expense, 1, 50m, "description", new DateOnly(CurrentYear, month, day), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DateOutsideMonth, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnDateOutsideMonth_WhenDateIsInAnotherYear()
        {
            var result = Transaction.Create(MoneyFlowType.Expense, 1, 50m, "description", new DateOnly(CurrentYear - 1, 3, 10), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DateOutsideMonth, result.Error);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(31)]
        public void Create_ShouldAcceptFirstAndLastDayOfTheMonth(int day)
        {
            var result = Transaction.Create(MoneyFlowType.Income, 1, 50m, "description", MarchDay(day), March());

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Update_ShouldChangeEveryFieldAndKeepTheMonth_WhenValuesAreValid()
        {
            var transaction = Transaction.Create(MoneyFlowType.Income, 1, 50m, "Salário", MarchDay(5), March()).Value!;

            var result = transaction.Update(MoneyFlowType.Expense, 3, 20m, "Pão de açúcar", MarchDay(6), March());

            Assert.True(result.IsSuccess);
            Assert.Same(transaction, result.Value);
            Assert.Equal(MoneyFlowType.Expense, transaction.Type);
            Assert.Equal(3, transaction.CategoryId.Value);
            Assert.Equal(20m, transaction.Amount.Value);
            Assert.Equal(-20m, transaction.SignedAmount);
            Assert.Equal("Pão de açúcar", transaction.Description.Value);
            Assert.Equal("pao de acucar", transaction.NormalizedDescription);
            Assert.Equal(MarchDay(6), transaction.TransactionDate);
            Assert.Equal(7, transaction.MonthlySummaryId.Value);
        }

        [Fact]
        public void Update_ShouldKeepCurrentValues_WhenValuesAreInvalid()
        {
            var transaction = Transaction.Create(MoneyFlowType.Income, 1, 50m, "Salário", MarchDay(5), March()).Value!;

            var result = transaction.Update(MoneyFlowType.Expense, 3, 20m, "Pão", new DateOnly(CurrentYear, 4, 1), March());

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DateOutsideMonth, result.Error);
            Assert.Equal(MoneyFlowType.Income, transaction.Type);
            Assert.Equal(50m, transaction.SignedAmount);
            Assert.Equal("Salário", transaction.Description.Value);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenTransactionHasNoIdYet()
        {
            var transaction = Transaction.Create(MoneyFlowType.Expense, 1, 50m, "description", MarchDay(1), March()).Value!;

            transaction.AssignId(10);

            Assert.NotNull(transaction.Id);
            Assert.Equal(10, transaction.Id!.Value);
        }
    }
}
