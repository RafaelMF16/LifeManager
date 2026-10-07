using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Budgets
{
    /// <summary>
    /// One version of a monthly goal: from <see cref="FromMonth"/> on (until <see cref="ToMonth"/>, or open-ended),
    /// the user's <see cref="Type"/> in <see cref="CategoryId"/> (or in the whole month, when null) should be at most
    /// (<see cref="MoneyFlowType.Expense"/>, a spending limit) or at least (<see cref="MoneyFlowType.Investment"/>,
    /// a target) <see cref="Amount"/>. Changing a goal from a month adds a new version and closes the previous one
    /// (see <see cref="BudgetTimeline"/>), so earlier months keep the goal they had.
    /// </summary>
    public class Budget
    {
        public BudgetId? Id { get; private set; }
        public UserId UserId { get; }
        public MoneyFlowType Type { get; }

        /// <summary>Null means the goal is for the whole month's total of <see cref="Type"/>.</summary>
        public CategoryId? CategoryId { get; }

        public BudgetAmount Amount { get; private set; }

        /// <summary>First day of <see cref="FromMonth"/>; a plain date column so months can be range-compared in SQL.</summary>
        public DateOnly EffectiveFrom { get; }

        /// <summary>First day of <see cref="ToMonth"/>, or null while the version is open-ended.</summary>
        public DateOnly? EffectiveTo { get; private set; }

        public YearMonth FromMonth => YearMonth.From(EffectiveFrom);
        public YearMonth? ToMonth => EffectiveTo is null ? null : YearMonth.From(EffectiveTo.Value);

        private Budget(UserId userId, MoneyFlowType type, CategoryId? categoryId, BudgetAmount amount, DateOnly effectiveFrom, DateOnly? effectiveTo)
        {
            UserId = userId;
            Type = type;
            CategoryId = categoryId;
            Amount = amount;
            EffectiveFrom = effectiveFrom;
            EffectiveTo = effectiveTo;
        }

        /// <summary>A new open-ended version starting in <paramref name="from"/>.</summary>
        /// <param name="idCategory">Null for a goal on the month's total.</param>
        public static Result<Budget> Create(int idUser, MoneyFlowType type, int? idCategory, decimal amount, YearMonth from)
        {
            if (type is not (MoneyFlowType.Expense or MoneyFlowType.Investment))
                return BudgetErrors.InvalidType;

            return BudgetAmount.Create(amount)
                .Map(budgetAmount => new Budget(
                    new UserId(idUser),
                    type,
                    idCategory is null ? null : new CategoryId(idCategory.Value),
                    budgetAmount,
                    from.FirstDay,
                    null));
        }

        /// <summary>Whether this version is the goal of <paramref name="month"/>.</summary>
        public bool Covers(YearMonth month)
            => FromMonth.Ordinal <= month.Ordinal && (ToMonth is null || ToMonth.Ordinal >= month.Ordinal);

        /// <summary>Whether the month's <paramref name="actual"/> amount meets the goal: within the limit, or at least the target.</summary>
        public bool IsMetBy(decimal actual)
            => Type == MoneyFlowType.Expense ? actual <= Amount.Value : actual >= Amount.Value;

        /// <summary>How far <paramref name="actual"/> misses the goal: what was spent over the limit, or what is missing to the target. Zero when met.</summary>
        public decimal GapFor(decimal actual)
            => Math.Max(0, Type == MoneyFlowType.Expense ? actual - Amount.Value : Amount.Value - actual);

        /// <summary><paramref name="actual"/> as a part of the goal (1 = exactly the goal), rounded to 4 places.</summary>
        public decimal ProgressOf(decimal actual)
            => Math.Round(actual / Amount.Value, 4);

        internal void ChangeAmount(BudgetAmount amount)
        {
            Amount = amount;
        }

        /// <param name="lastMonth">Null makes the version open-ended again.</param>
        internal void EndAt(YearMonth? lastMonth)
        {
            EffectiveTo = lastMonth?.FirstDay;
        }

        /// <summary>Rehydrates a stored version without re-validating it.</summary>
        internal static Budget FromPersistence(int id, int idUser, MoneyFlowType type, int? idCategory, decimal amount, DateOnly effectiveFrom, DateOnly? effectiveTo)
        {
            var budget = new Budget(
                new UserId(idUser),
                type,
                idCategory is null ? null : new CategoryId(idCategory.Value),
                BudgetAmount.FromPersistence(amount),
                effectiveFrom,
                effectiveTo);
            budget.AssignId(id);

            return budget;
        }

        public void AssignId(int id)
        {
            Id = new BudgetId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Budget other)
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
