using LifeManager.Domain.SavingsGoals.Errors;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.SavingsGoals
{
    /// <summary>
    /// A medium-term savings target ("Trip: R$ 5,000 by Dec/2027"). It grows with the <c>Investment</c> transactions
    /// linked to it; that sum is read from the transactions and never stored. Deleting it from the UI archives it.
    /// </summary>
    public class SavingsGoal
    {
        public const int IconMaxLength = 30;

        public SavingsGoalId? Id { get; private set; }
        public UserId UserId { get; }
        public SavingsGoalName Name { get; private set; }

        /// <summary>Persisted copy of <see cref="SavingsGoalName.NormalizedValue"/> (trigram search, uniqueness among active goals).</summary>
        public string NormalizedName { get; private set; }

        /// <summary><see cref="TransactionAmount"/> rules: positive, 2 places.</summary>
        public decimal TargetAmount { get; private set; }

        /// <summary>The deadline's first day (a plain <c>date</c> column, so it sorts in SQL); null without a deadline.</summary>
        public DateOnly? TargetMonthStart { get; private set; }

        /// <summary>What was already saved before the goal existed: zero or more, 2 places.</summary>
        public decimal InitialAmount { get; private set; }

        /// <summary>The id of an icon the frontend knows.</summary>
        public string? Icon { get; private set; }

        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? ArchivedAt { get; private set; }

        /// <summary>The first day the target was reached. Informative, never cleared.</summary>
        public DateOnly? CompletedAt { get; private set; }

        public YearMonth? TargetMonth => TargetMonthStart is null ? null : YearMonth.From(TargetMonthStart.Value);
        public bool IsArchived => ArchivedAt is not null;

        private SavingsGoal(
            UserId userId,
            SavingsGoalName name,
            decimal targetAmount,
            DateOnly? targetMonthStart,
            decimal initialAmount,
            string? icon,
            DateTimeOffset createdAt,
            DateTimeOffset? archivedAt,
            DateOnly? completedAt)
        {
            UserId = userId;
            Name = name;
            NormalizedName = name.NormalizedValue;
            TargetAmount = targetAmount;
            TargetMonthStart = targetMonthStart;
            InitialAmount = initialAmount;
            Icon = icon;
            CreatedAt = createdAt;
            ArchivedAt = archivedAt;
            CompletedAt = completedAt;
        }

        /// <param name="currentMonth">Today's month in the business time zone; the deadline can't be before it.</param>
        public static Result<SavingsGoal> Create(
            int idUser,
            string? name,
            decimal targetAmount,
            YearMonth? targetMonth,
            decimal initialAmount,
            string? icon,
            YearMonth currentMonth,
            DateTimeOffset now)
        {
            var valuesResult = Validate(name, targetAmount, targetMonth, initialAmount, icon, currentMonth);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value!;

            return new SavingsGoal(
                new UserId(idUser), values.Name, targetAmount, targetMonth?.FirstDay, initialAmount, values.Icon, now, archivedAt: null, completedAt: null);
        }

        /// <summary>Rehydrates a stored goal without the creation rules (a deadline already in the past).</summary>
        internal static SavingsGoal FromPersistence(
            int id,
            int idUser,
            string name,
            decimal targetAmount,
            DateOnly? targetMonthStart,
            decimal initialAmount,
            string? icon,
            DateTimeOffset createdAt,
            DateTimeOffset? archivedAt,
            DateOnly? completedAt)
        {
            var goal = new SavingsGoal(
                new UserId(idUser), SavingsGoalName.FromPersistence(name), targetAmount, targetMonthStart, initialAmount, icon, createdAt, archivedAt, completedAt);
            goal.AssignId(id);

            return goal;
        }

        /// <summary>Changes every field; the deadline can't be in the past here either.</summary>
        public Result<SavingsGoal> Update(string? name, decimal targetAmount, YearMonth? targetMonth, decimal initialAmount, string? icon, YearMonth currentMonth)
        {
            if (IsArchived)
                return SavingsGoalErrors.Archived;

            var valuesResult = Validate(name, targetAmount, targetMonth, initialAmount, icon, currentMonth);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value!;

            Name = values.Name;
            NormalizedName = values.Name.NormalizedValue;
            TargetAmount = targetAmount;
            TargetMonthStart = targetMonth?.FirstDay;
            InitialAmount = initialAmount;
            Icon = values.Icon;

            return this;
        }

        public Result<SavingsGoal> Archive(DateTimeOffset now)
        {
            if (IsArchived)
                return SavingsGoalErrors.AlreadyArchived;

            ArchivedAt = now;
            return this;
        }

        public Result<SavingsGoal> Restore()
        {
            if (!IsArchived)
                return SavingsGoalErrors.NotArchived;

            ArchivedAt = null;
            return this;
        }

        /// <summary>Only an active goal takes new transactions.</summary>
        public Result EnsureCanLink()
            => IsArchived ? SavingsGoalErrors.Archived : Result.Success();

        /// <summary>Records the day the target was first reached; false (nothing changes) when it already was.</summary>
        public bool MarkCompleted(DateOnly date)
        {
            if (CompletedAt is not null)
                return false;

            CompletedAt = date;
            return true;
        }

        public void AssignId(int id)
        {
            Id = new SavingsGoalId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not SavingsGoal other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();

        private record ValidatedValues(SavingsGoalName Name, string? Icon);

        private static Result<ValidatedValues> Validate(
            string? name, decimal targetAmount, YearMonth? targetMonth, decimal initialAmount, string? icon, YearMonth currentMonth)
        {
            var nameResult = SavingsGoalName.Create(name);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            if (!TransactionAmount.Create(targetAmount).IsSuccess)
                return SavingsGoalErrors.InvalidTargetAmount;

            if (initialAmount != 0 && !TransactionAmount.Create(initialAmount).IsSuccess)
                return SavingsGoalErrors.InvalidInitialAmount;

            if (targetMonth is not null && targetMonth.Ordinal < currentMonth.Ordinal)
                return SavingsGoalErrors.TargetMonthInPast;

            var normalizedIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
            if (normalizedIcon is not null && normalizedIcon.Length > IconMaxLength)
                return SavingsGoalErrors.IconTooLong;

            return new ValidatedValues(nameResult.Value!, normalizedIcon);
        }
    }
}
