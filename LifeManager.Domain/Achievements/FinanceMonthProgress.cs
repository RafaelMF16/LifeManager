using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Achievements
{
    /// <summary>A month's money: what came in, was spent and was invested.</summary>
    public record FinanceMonth(YearMonth Month, decimal Income, decimal Expense, decimal Investment);

    /// <summary>A month's plan with the dates (in the business time zone) it was first and last saved.</summary>
    public record FinanceMonthPlan(BudgetPlan Plan, DateOnly CreatedOn, DateOnly UpdatedOn);

    /// <summary>How many closed months met each Finance achievement's rule.</summary>
    public record FinanceMonthCounts(int BudgetMonths, int InvestmentMonths, int SavingsMonths);

    /// <summary>
    /// Counts the closed months behind the Finance achievements (numbers in <see cref="GameRules"/>). Pure: the same
    /// history always gives the same counts. A plan only counts when it was made in time and left alone afterwards, so
    /// a goal can't be set (or loosened) after the fact.
    /// </summary>
    public static class FinanceMonthProgress
    {
        /// <param name="lastClosed">The month before the current one: the running month never counts.</param>
        public static FinanceMonthCounts Count(IReadOnlyCollection<FinanceMonth> months, IReadOnlyCollection<FinanceMonthPlan> plans, YearMonth lastClosed)
        {
            var monthsByOrdinal = months.ToDictionary(month => month.Month.Ordinal);
            var eligiblePlans = plans
                .Where(item => item.Plan.Month.Ordinal <= lastClosed.Ordinal && IsEligible(item))
                .Select(item => (item.Plan, Money: monthsByOrdinal.GetValueOrDefault(item.Plan.Month.Ordinal) ?? new FinanceMonth(item.Plan.Month, 0, 0, 0)))
                .ToList();

            return new FinanceMonthCounts(
                eligiblePlans.Count(item => IsWithinBudget(item.Plan, item.Money)),
                eligiblePlans.Count(item => item.Plan.InvestmentSlice is { } slice && slice.IsMetBy(item.Money.Investment, item.Plan.Total)),
                months.Count(month => month.Month.Ordinal <= lastClosed.Ordinal && IsSavingsMonth(month)));
        }

        /// <summary>Created by the month's <see cref="GameRules.BudgetPlanLatestCreationDay"/> and not changed after it ended.</summary>
        private static bool IsEligible(FinanceMonthPlan item)
        {
            var month = item.Plan.Month;

            return item.CreatedOn <= month.FirstDay.AddDays(GameRules.BudgetPlanLatestCreationDay - 1)
                && item.UpdatedOn <= month.LastDay;
        }

        /// <summary>
        /// Within the spending limit, having spent at least <see cref="GameRules.BudgetMinimumUsage"/> of it: an inflated
        /// total doesn't pass.
        /// </summary>
        private static bool IsWithinBudget(BudgetPlan plan, FinanceMonth money)
            => plan.SpendingLimit > 0
                && money.Expense <= plan.SpendingLimit
                && money.Expense >= plan.SpendingLimit * GameRules.BudgetMinimumUsage;

        /// <summary>Saved (income − expense, investing counts as saving) at least <see cref="GameRules.FinanceSavingsRateGoal"/> of the income.</summary>
        private static bool IsSavingsMonth(FinanceMonth month)
            => month.Income > 0 && month.Income - month.Expense >= month.Income * GameRules.FinanceSavingsRateGoal;
    }
}
