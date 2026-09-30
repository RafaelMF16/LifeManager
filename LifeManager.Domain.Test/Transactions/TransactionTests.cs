using LifeManager.Domain.Categories;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionTests
    {
        [Fact]
        public void Create_ShouldReturnTransaction_WhenTheValuesAreValid()
        {
            var category = Category.Create(1, "name").Value!;

            var type = MoneyFlowType.Expense;
            var amount = 50;
            var description = "description";
            var date = DateTimeOffset.UtcNow;
            var idMonthlySummary = 1;

            var transaction = Transaction.Create(type, category, amount, description, date, idMonthlySummary);

            Assert.NotNull(transaction);
            Assert.IsType<Transaction>(transaction);
            Assert.Null(transaction.Id);
            Assert.Equal(type, transaction.Type);
            Assert.Same(category, transaction.Category);
            Assert.Equal(amount, transaction.Amount.Value);
            Assert.Equal(description, transaction.Description.Value);
            Assert.Equal(date, transaction.TransactionDate);
            Assert.Equal(idMonthlySummary, transaction.MonthlySummaryId.Value);
        }

        [Theory]
        [InlineData(MoneyFlowType.Expense)]
        [InlineData(MoneyFlowType.Income)]
        public void Create_ShouldAcceptAnyMoneyFlowType_WhenUsingTheSameCategory(MoneyFlowType type)
        {
            var category = Category.Create(1, "name").Value!;

            var transaction = Transaction.Create(type, category, 50, "description", DateTimeOffset.UtcNow, 1);

            Assert.Equal(type, transaction.Type);
            Assert.Same(category, transaction.Category);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenTransactionHasNoIdYet()
        {
            var category = Category.Create(1, "name").Value!;
            var transaction = Transaction.Create(MoneyFlowType.Expense, category, 50, "description", DateTimeOffset.UtcNow, 1);

            transaction.AssignId(10);

            Assert.NotNull(transaction.Id);
            Assert.Equal(10, transaction.Id!.Value);
        }
    }
}
