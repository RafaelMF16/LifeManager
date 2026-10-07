using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RecurringTransactions
{
    /// <summary>
    /// A transaction that is posted automatically once a month, on <see cref="DayOfMonth"/>, from
    /// <see cref="StartMonth"/> until <see cref="EndMonth"/> (or forever). Posting turns the next occurrence
    /// into an ordinary <c>Transaction</c> and moves <see cref="NextMonth"/> forward; already posted
    /// transactions never change when the recurrence does.
    /// </summary>
    public class RecurringTransaction
    {
        public RecurringTransactionId? Id { get; private set; }
        public UserId UserId { get; }
        public MoneyFlowType Type { get; private set; }
        public CategoryId CategoryId { get; private set; }
        public TransactionAmount Amount { get; private set; }
        public TransactionDescription Description { get; private set; }
        public RecurrenceDay DayOfMonth { get; private set; }
        public YearMonth StartMonth { get; private set; }

        /// <summary>Last month with an occurrence; null means it never ends.</summary>
        public YearMonth? EndMonth { get; private set; }

        public bool IsActive { get; private set; }

        /// <summary>The posting cursor: the first month whose occurrence hasn't been posted (or skipped) yet.</summary>
        public YearMonth NextMonth { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="TransactionDescription.NormalizedValue"/>, kept as its own column so it can be
        /// indexed for trigram search. Always derived from <see cref="Description"/>.
        /// </summary>
        public string NormalizedDescription { get; private set; }

        /// <summary>
        /// Persisted date of the occurrence in <see cref="NextMonth"/>, null once past <see cref="EndMonth"/>. Kept as
        /// its own column so the due occurrences can be found (and sorted) in the database. Always derived from
        /// <see cref="NextMonth"/>, <see cref="DayOfMonth"/> and <see cref="EndMonth"/>.
        /// </summary>
        public DateOnly? NextOccurrenceDate { get; private set; }

        public RecurringTransactionStatus Status
            => NextOccurrenceDate is null
                ? RecurringTransactionStatus.Finished
                : IsActive ? RecurringTransactionStatus.Active : RecurringTransactionStatus.Paused;

        private RecurringTransaction(
            UserId userId,
            MoneyFlowType type,
            CategoryId categoryId,
            TransactionAmount amount,
            TransactionDescription description,
            RecurrenceDay dayOfMonth,
            YearMonth startMonth,
            YearMonth? endMonth,
            bool isActive,
            YearMonth nextMonth)
        {
            UserId = userId;
            Type = type;
            CategoryId = categoryId;
            Amount = amount;
            Description = description;
            DayOfMonth = dayOfMonth;
            StartMonth = startMonth;
            EndMonth = endMonth;
            IsActive = isActive;
            NextMonth = nextMonth;
            NormalizedDescription = description.NormalizedValue;
            NextOccurrenceDate = ToNextOccurrenceDate(nextMonth, dayOfMonth, endMonth);
        }

        /// <param name="startMonth">First month with an occurrence, "yyyy-MM"; it can't be before <paramref name="today"/>'s month.</param>
        /// <param name="endMonth">Last month with an occurrence, "yyyy-MM"; null or empty means it never ends.</param>
        /// <param name="today">The user's current date, so past months are never filled in.</param>
        public static Result<RecurringTransaction> Create(
            int idUser,
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            int dayOfMonth,
            string? startMonth,
            string? endMonth,
            DateOnly today)
        {
            var valuesResult = Validate(type, amount, description, dayOfMonth, startMonth, endMonth);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value;

            if (values.StartMonth.Ordinal < YearMonth.From(today).Ordinal)
                return RecurringTransactionErrors.StartInPast;

            return new RecurringTransaction(
                new UserId(idUser),
                type,
                new CategoryId(idCategory),
                values.Amount,
                values.Description,
                values.DayOfMonth,
                values.StartMonth,
                values.EndMonth,
                isActive: true,
                nextMonth: values.StartMonth);
        }

        /// <summary>
        /// Changes what the next occurrences will look like; transactions already posted stay as they are. The start
        /// month can only change (and only to the current month or later) while the recurrence hasn't started.
        /// </summary>
        public Result<RecurringTransaction> Update(
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            int dayOfMonth,
            string? startMonth,
            string? endMonth,
            DateOnly today)
        {
            var valuesResult = Validate(type, amount, description, dayOfMonth, startMonth, endMonth);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value;
            var nextMonth = NextMonth;

            if (!values.StartMonth.Equals(StartMonth))
            {
                if (!NextMonth.Equals(StartMonth))
                    return RecurringTransactionErrors.StartLocked;

                if (values.StartMonth.Ordinal < YearMonth.From(today).Ordinal)
                    return RecurringTransactionErrors.StartInPast;

                nextMonth = values.StartMonth;
            }

            Type = type;
            CategoryId = new CategoryId(idCategory);
            Amount = values.Amount;
            Description = values.Description;
            NormalizedDescription = values.Description.NormalizedValue;
            DayOfMonth = values.DayOfMonth;
            StartMonth = values.StartMonth;
            EndMonth = values.EndMonth;
            NextMonth = nextMonth;
            NextOccurrenceDate = ToNextOccurrenceDate(NextMonth, DayOfMonth, EndMonth);

            return this;
        }

        public Result<RecurringTransaction> Pause()
        {
            if (!IsActive)
                return RecurringTransactionErrors.AlreadyPaused;

            IsActive = false;
            return this;
        }

        /// <summary>Starts posting again from <paramref name="today"/>'s month: the months it stayed paused are skipped.</summary>
        public Result<RecurringTransaction> Resume(DateOnly today)
        {
            if (IsActive)
                return RecurringTransactionErrors.NotPaused;

            var currentMonth = YearMonth.From(today);
            if (NextMonth.Ordinal < currentMonth.Ordinal)
                NextMonth = currentMonth;

            IsActive = true;
            NextOccurrenceDate = ToNextOccurrenceDate(NextMonth, DayOfMonth, EndMonth);

            return this;
        }

        /// <summary>Whether the next occurrence should be posted now: active, not finished and its day has come.</summary>
        public bool IsDue(DateOnly today)
            => IsActive && NextOccurrenceDate is not null && NextOccurrenceDate.Value <= today;

        /// <summary>Moves the cursor past the occurrence that was just posted.</summary>
        public void AdvanceAfterPosting()
        {
            NextMonth = NextMonth.AddMonths(1);
            NextOccurrenceDate = ToNextOccurrenceDate(NextMonth, DayOfMonth, EndMonth);
        }

        /// <summary>Rehydrates a stored recurrence without re-validating it (its start month may be in the past by now).</summary>
        internal static RecurringTransaction FromPersistence(
            int id,
            int idUser,
            MoneyFlowType type,
            int idCategory,
            decimal amount,
            string description,
            int dayOfMonth,
            YearMonth startMonth,
            YearMonth? endMonth,
            bool isActive,
            YearMonth nextMonth)
        {
            var recurringTransaction = new RecurringTransaction(
                new UserId(idUser),
                type,
                new CategoryId(idCategory),
                TransactionAmount.FromPersistence(amount),
                TransactionDescription.FromPersistence(description),
                RecurrenceDay.FromPersistence(dayOfMonth),
                startMonth,
                endMonth,
                isActive,
                nextMonth);
            recurringTransaction.AssignId(id);

            return recurringTransaction;
        }

        public void AssignId(int id)
        {
            Id = new RecurringTransactionId(id);
        }

        private static DateOnly? ToNextOccurrenceDate(YearMonth nextMonth, RecurrenceDay dayOfMonth, YearMonth? endMonth)
            => endMonth is not null && nextMonth.Ordinal > endMonth.Ordinal ? null : dayOfMonth.DateIn(nextMonth);

        private static Result<ValidatedValues> Validate(
            MoneyFlowType type,
            decimal amount,
            string description,
            int dayOfMonth,
            string? startMonth,
            string? endMonth)
        {
            if (!Enum.IsDefined(type))
                return TransactionErrors.InvalidType;

            var descriptionResult = TransactionDescription.Create(description);
            if (!descriptionResult.IsSuccess)
                return descriptionResult.Error;

            var amountResult = TransactionAmount.Create(amount);
            if (!amountResult.IsSuccess)
                return amountResult.Error;

            var dayResult = RecurrenceDay.Create(dayOfMonth);
            if (!dayResult.IsSuccess)
                return dayResult.Error;

            if (!YearMonth.TryParse(startMonth, out var start))
                return RecurringTransactionErrors.InvalidStartMonth;

            YearMonth? end = null;
            if (!string.IsNullOrEmpty(endMonth))
            {
                if (!YearMonth.TryParse(endMonth, out var parsedEnd))
                    return RecurringTransactionErrors.InvalidEndMonth;

                if (parsedEnd.Ordinal < start.Ordinal)
                    return RecurringTransactionErrors.EndBeforeStart;

                end = parsedEnd;
            }

            return new ValidatedValues(amountResult.Value, descriptionResult.Value, dayResult.Value, start, end);
        }

        private record ValidatedValues(
            TransactionAmount Amount,
            TransactionDescription Description,
            RecurrenceDay DayOfMonth,
            YearMonth StartMonth,
            YearMonth? EndMonth);

        public override bool Equals(object? obj)
        {
            if (obj is not RecurringTransaction other)
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
