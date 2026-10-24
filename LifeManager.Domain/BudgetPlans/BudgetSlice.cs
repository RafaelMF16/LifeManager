using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.BudgetPlans.ValueObjects;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.BudgetPlans
{
    /// <summary>
    /// A part of a month's <see cref="BudgetPlan"/>: a spending limit for one category (<see cref="MoneyFlowType.Expense"/>)
    /// or the month's investment target (<see cref="MoneyFlowType.Investment"/>, with no category).
    /// </summary>
    public class BudgetSlice
    {
        public MoneyFlowType Type { get; }

        /// <summary>The expense's category; null for the investment slice.</summary>
        public CategoryId? CategoryId { get; }

        public SlicePercent Percent { get; }

        private BudgetSlice(MoneyFlowType type, CategoryId? categoryId, SlicePercent percent)
        {
            Type = type;
            CategoryId = categoryId;
            Percent = percent;
        }

        /// <param name="idCategory">Required for an expense; null for the investment slice.</param>
        public static Result<BudgetSlice> Create(MoneyFlowType type, int? idCategory, decimal percent)
        {
            if (type is not (MoneyFlowType.Expense or MoneyFlowType.Investment))
                return BudgetPlanErrors.InvalidType;

            if (type == MoneyFlowType.Expense && idCategory is null)
                return BudgetPlanErrors.ExpenseSliceWithoutCategory;

            if (type == MoneyFlowType.Investment && idCategory is not null)
                return BudgetPlanErrors.InvalidInvestmentSlice;

            return SlicePercent.Create(percent)
                .Map(slicePercent => new BudgetSlice(
                    type,
                    idCategory is null ? null : new CategoryId(idCategory.Value),
                    slicePercent));
        }

        /// <summary>The slice's share of <paramref name="total"/>, rounded to cents.</summary>
        public decimal AmountOf(BudgetTotal total)
            => Math.Round(ExactAmountOf(total), 2, MidpointRounding.AwayFromZero);

        /// <summary>Within the limit (expense) or at least the target (investment).</summary>
        public bool IsMetBy(decimal actual, BudgetTotal total)
            => Type == MoneyFlowType.Expense ? actual <= AmountOf(total) : actual >= AmountOf(total);

        /// <summary>
        /// <paramref name="actual"/> as a part of the slice (1 = exactly the slice), rounded to 4 places. Divides by the
        /// unrounded share, which is never zero even when the slice rounds to no cents.
        /// </summary>
        public decimal ProgressOf(decimal actual, BudgetTotal total)
            => Math.Round(actual / ExactAmountOf(total), 4);

        private decimal ExactAmountOf(BudgetTotal total)
            => total.Value * Percent.Value / 100;
    }
}
