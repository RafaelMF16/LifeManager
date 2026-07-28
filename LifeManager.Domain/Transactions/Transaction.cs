using LifeManager.Domain.Categories;
using LifeManager.Domain.Exceptions;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Transactions
{
    public class Transaction
    {
        public TransactionId? Id { get; }
        public MoneyFlowType Type { get; private set; }
        public Category Category { get; private set; }
        public TransactionAmount Amount { get; private set; }
        public TransactionDescription Description { get; private set; }
        public DateTimeOffset TransactionDate { get; private set; }
        public MonthlySummaryId MonthlySummaryId { get; }

        private Transaction(
            MoneyFlowType type,
            Category category,
            TransactionAmount amount,
            TransactionDescription description,
            DateTimeOffset transactionDate,
            MonthlySummaryId monthlySummaryId)
        {
            Type = type;
            Category = category;
            Amount = amount;
            Description = description;
            TransactionDate = transactionDate;
            MonthlySummaryId = monthlySummaryId;
        }

        public static Transaction Create(
            MoneyFlowType type,
            Category category,
            decimal amount,
            string description,
            DateTimeOffset transactionDate,
            int idMonthlySummary)
        {
            if (type != category.Type)
                throw new DomainException("The money flow type must be the same in the transaction and in the category");

            var transactionAmount = TransactionAmount.Create(amount);
            var transactionDescription = TransactionDescription.Create(description);
            var monthlySummaryId = new MonthlySummaryId(idMonthlySummary);

            return new Transaction(type, category, transactionAmount, transactionDescription, transactionDate, monthlySummaryId);
        }
    }
}