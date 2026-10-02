using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Transactions
{
    public class Transaction
    {
        public TransactionId? Id { get; private set; }
        public MonthlySummaryId MonthlySummaryId { get; }
        public MoneyFlowType Type { get; private set; }
        public CategoryId CategoryId { get; private set; }
        public TransactionAmount Amount { get; private set; }
        public TransactionDescription Description { get; private set; }
        public DateOnly TransactionDate { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="TransactionDescription.NormalizedValue"/>, kept as its own column so it can be
        /// indexed for trigram search. Always derived from <see cref="Description"/>.
        /// </summary>
        public string NormalizedDescription { get; private set; }

        /// <summary>
        /// Persisted <see cref="Amount"/> with the sign of its <see cref="Type"/> (income positive, expense negative),
        /// so listings can sort by value and totals can be summed in the database. Always derived from both.
        /// </summary>
        public decimal SignedAmount { get; private set; }

        private Transaction(
            MonthlySummaryId monthlySummaryId,
            MoneyFlowType type,
            CategoryId categoryId,
            TransactionAmount amount,
            TransactionDescription description,
            DateOnly transactionDate)
        {
            MonthlySummaryId = monthlySummaryId;
            Type = type;
            CategoryId = categoryId;
            Amount = amount;
            Description = description;
            TransactionDate = transactionDate;
            NormalizedDescription = description.NormalizedValue;
            SignedAmount = ToSignedAmount(type, amount);
        }

        /// <summary>Records money coming in or going out of the given month; the date must fall inside that month.</summary>
        public static Result<Transaction> Create(
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            DateOnly transactionDate,
            MonthlySummary monthlySummary)
        {
            return Validate(type, amount, description, transactionDate, monthlySummary)
                .Map(values => new Transaction(
                    monthlySummary.Id!,
                    type,
                    new CategoryId(idCategory),
                    values.Amount,
                    values.Description,
                    transactionDate));
        }

        /// <summary>Changes every field except the month: a transaction always stays in the month it was created in.</summary>
        public Result<Transaction> Update(
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            DateOnly transactionDate,
            MonthlySummary monthlySummary)
        {
            return Validate(type, amount, description, transactionDate, monthlySummary)
                .Map(values =>
                {
                    Type = type;
                    CategoryId = new CategoryId(idCategory);
                    Amount = values.Amount;
                    Description = values.Description;
                    NormalizedDescription = values.Description.NormalizedValue;
                    TransactionDate = transactionDate;
                    SignedAmount = ToSignedAmount(type, values.Amount);
                    return this;
                });
        }

        /// <summary>Rehydrates a stored transaction without re-validating it.</summary>
        internal static Transaction FromPersistence(
            int id,
            int idMonthlySummary,
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            DateOnly transactionDate)
        {
            var transaction = new Transaction(
                new MonthlySummaryId(idMonthlySummary),
                type,
                new CategoryId(idCategory),
                TransactionAmount.FromPersistence(amount),
                TransactionDescription.FromPersistence(description),
                transactionDate);
            transaction.AssignId(id);

            return transaction;
        }

        public void AssignId(int id)
        {
            Id = new TransactionId(id);
        }

        private static Result<ValidatedValues> Validate(
            MoneyFlowType type,
            decimal amount,
            string description,
            DateOnly transactionDate,
            MonthlySummary monthlySummary)
        {
            if (!Enum.IsDefined(type))
                return TransactionErrors.InvalidType;

            var descriptionResult = TransactionDescription.Create(description);
            if (!descriptionResult.IsSuccess)
                return descriptionResult.Error;

            var amountResult = TransactionAmount.Create(amount);
            if (!amountResult.IsSuccess)
                return amountResult.Error;

            if (transactionDate.Year != monthlySummary.Year.Value || transactionDate.Month != monthlySummary.Month.Value)
                return TransactionErrors.DateOutsideMonth;

            return new ValidatedValues(amountResult.Value, descriptionResult.Value);
        }

        private static decimal ToSignedAmount(MoneyFlowType type, TransactionAmount amount)
            => type == MoneyFlowType.Income ? amount.Value : -amount.Value;

        private record ValidatedValues(TransactionAmount Amount, TransactionDescription Description);

        public override bool Equals(object? obj)
        {
            if (obj is not Transaction other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
