using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions.Enums;
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

        /// <summary>The recurrence that posted this transaction; null for transactions entered by hand.</summary>
        public RecurringTransactionId? RecurringTransactionId { get; private set; }

        /// <summary>The bank statement import that posted this transaction; null otherwise. Kept when the import leaves the history.</summary>
        public StatementImportId? StatementImportId { get; private set; }

        /// <summary>The bank line it came from (<see cref="ImportExternalId"/>), so importing the same statement again finds it.</summary>
        public string? ExternalId { get; private set; }

        /// <summary>The savings goal this investment counts towards; null when it counts towards none. Only investments have one.</summary>
        public SavingsGoalId? SavingsGoalId { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="TransactionDescription.NormalizedValue"/>, kept as its own column so it can be
        /// indexed for trigram search. Always derived from <see cref="Description"/>.
        /// </summary>
        public string NormalizedDescription { get; private set; }

        /// <summary>
        /// Persisted <see cref="Amount"/> with the sign of its <see cref="Type"/> (income positive; expense and investment
        /// negative, since both leave the account),
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
        /// <param name="idSavingsGoal">
        /// The savings goal an investment counts towards (checked by the caller to be the user's and active); only an
        /// <see cref="MoneyFlowType.Investment"/> takes one.
        /// </param>
        public static Result<Transaction> Create(
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            DateOnly transactionDate,
            MonthlySummary monthlySummary,
            int? idSavingsGoal = null)
        {
            return Validate(type, amount, description, transactionDate, monthlySummary, idSavingsGoal)
                .Map(values => new Transaction(
                    monthlySummary.Id!,
                    type,
                    new CategoryId(idCategory),
                    values.Amount,
                    values.Description,
                    transactionDate)
                {
                    SavingsGoalId = ToSavingsGoalId(idSavingsGoal)
                });
        }

        /// <summary>
        /// Posts the recurrence's next occurrence, dated <see cref="RecurringTransaction.NextOccurrenceDate"/>, into
        /// that date's month. The transaction remembers its recurrence but is otherwise an ordinary transaction.
        /// </summary>
        public static Result<Transaction> CreateFromRecurrence(RecurringTransaction recurringTransaction, MonthlySummary monthlySummary)
        {
            var occurrenceDate = recurringTransaction.NextOccurrenceDate
                ?? throw new InvalidOperationException("A finished recurrence has no occurrence to post");

            return Create(
                    recurringTransaction.Type,
                    recurringTransaction.CategoryId.Value,
                    recurringTransaction.Amount.Value,
                    recurringTransaction.Description.Value,
                    occurrenceDate,
                    monthlySummary)
                .Tap(transaction => transaction.RecurringTransactionId = recurringTransaction.Id);
        }

        /// <summary>
        /// Posts a confirmed statement line into its date's month. The row must have a category (the confirmation
        /// checks every included row has one first).
        /// </summary>
        public static Result<Transaction> CreateFromImport(StatementImportRow row, MonthlySummary monthlySummary)
        {
            if (row.CategoryId is null)
                return StatementImportErrors.UncategorizedRows;

            return Create(row.Type, row.CategoryId.Value, row.Amount.Value, row.Description.Value, row.EffectiveDate, monthlySummary)
                .Tap(transaction =>
                {
                    transaction.StatementImportId = row.StatementImportId;
                    transaction.ExternalId = row.ExternalId;
                });
        }

        /// <summary>
        /// Posts the real expense of a redeemed reward ("Dinner out", in its category, for its amount) on
        /// <paramref name="date"/>, the redemption's day, into that day's month.
        /// </summary>
        public static Result<Transaction> CreateFromRedemption(Reward reward, DateOnly date, MonthlySummary monthlySummary)
        {
            if (!reward.HasExpense)
                return RewardErrors.ExpenseNotConfigured;

            return Create(MoneyFlowType.Expense, reward.ExpenseCategoryId!.Value, reward.ExpenseAmount!.Value, reward.Name.Value, date, monthlySummary);
        }

        /// <summary>
        /// Where the transaction came from: its recurrence first, then its import (the import's id, or the bank line once
        /// the import left the history), then a reward redemption pointing to it (<paramref name="isRewardExpense"/>,
        /// stored on the redemption), otherwise typed by hand.
        /// </summary>
        public TransactionOrigin Origin(bool isRewardExpense)
        {
            if (RecurringTransactionId is not null)
                return TransactionOrigin.Recurring;

            if (StatementImportId is not null || ExternalId is not null)
                return TransactionOrigin.Import;

            return isRewardExpense ? TransactionOrigin.Reward : TransactionOrigin.Manual;
        }

        /// <summary>
        /// Changes every field except the month: a transaction always stays in the month it was created in. A null
        /// <paramref name="idSavingsGoal"/> unlinks it from its goal.
        /// </summary>
        public Result<Transaction> Update(
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            DateOnly transactionDate,
            MonthlySummary monthlySummary,
            int? idSavingsGoal = null)
        {
            return Validate(type, amount, description, transactionDate, monthlySummary, idSavingsGoal)
                .Map(values =>
                {
                    Type = type;
                    CategoryId = new CategoryId(idCategory);
                    Amount = values.Amount;
                    Description = values.Description;
                    NormalizedDescription = values.Description.NormalizedValue;
                    TransactionDate = transactionDate;
                    SignedAmount = ToSignedAmount(type, values.Amount);
                    SavingsGoalId = ToSavingsGoalId(idSavingsGoal);
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
            DateOnly transactionDate,
            int? idRecurringTransaction = null,
            int? idStatementImport = null,
            string? externalId = null,
            int? idSavingsGoal = null)
        {
            var transaction = new Transaction(
                new MonthlySummaryId(idMonthlySummary),
                type,
                new CategoryId(idCategory),
                TransactionAmount.FromPersistence(amount),
                TransactionDescription.FromPersistence(description),
                transactionDate)
            {
                RecurringTransactionId = idRecurringTransaction is null ? null : new RecurringTransactionId(idRecurringTransaction.Value),
                StatementImportId = idStatementImport is null ? null : new StatementImportId(idStatementImport.Value),
                ExternalId = externalId,
                SavingsGoalId = ToSavingsGoalId(idSavingsGoal)
            };
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
            MonthlySummary monthlySummary,
            int? idSavingsGoal)
        {
            if (!Enum.IsDefined(type))
                return TransactionErrors.InvalidType;

            if (idSavingsGoal is not null && type != MoneyFlowType.Investment)
                return TransactionErrors.GoalRequiresInvestment;

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

        private static SavingsGoalId? ToSavingsGoalId(int? idSavingsGoal)
            => idSavingsGoal is null ? null : new SavingsGoalId(idSavingsGoal.Value);

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
