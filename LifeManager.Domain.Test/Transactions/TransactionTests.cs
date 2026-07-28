using LifeManager.Domain.Categories;
using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenTypeIsDifferentOfCategoryType()
        {
            var userId = 1;
            var categoryType = MoneyFlowType.Expense;
            var name = "name";
            var category = Category.Create(userId, categoryType, name);

            var type = MoneyFlowType.Income;
            var amount = 50;
            var description = "description";
            var date = DateTimeOffset.UtcNow;
            var idMonthlySummary = 1;

            const string errorMessageExpected = "The money flow type must be the same in the transaction and in the category";
            var exception = Assert.Throws<DomainException>(() => Transaction.Create(type, category, amount, description, date, idMonthlySummary));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnTransaction_WhenTheValuesAreValid()
        {
            var userId = 1;
            var categoryType = MoneyFlowType.Expense;
            var name = "name";
            var category = Category.Create(userId, categoryType, name);

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
            Assert.Equal(amount, transaction.Amount.Value);
            Assert.Equal(description, transaction.Description.Value);
            Assert.Equal(date, transaction.TransactionDate);
            Assert.Equal(idMonthlySummary, transaction.MonthlySummaryId.Value);
        }
    }
}