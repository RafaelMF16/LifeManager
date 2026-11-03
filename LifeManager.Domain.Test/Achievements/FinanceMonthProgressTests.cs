using LifeManager.Domain.Achievements;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.Achievements
{
    public class FinanceMonthProgressTests
    {
        private static readonly YearMonth September = YearMonth.From(new DateOnly(2026, 9, 1));
        private static readonly YearMonth October = YearMonth.From(new DateOnly(2026, 10, 1));

        // 5000 total with a 20% investment slice: a 4000 spending limit and a 1000 investment goal.
        private static FinanceMonthPlan Plan(YearMonth month, int createdDay = 1, DateOnly? updatedOn = null, bool withInvestment = true)
        {
            BudgetSlice[] slices = withInvestment ? [BudgetSlice.Create(MoneyFlowType.Investment, null, 20m).Value!] : [];
            var plan = BudgetPlan.Create(1, month, 5000m, slices, DateTimeOffset.UnixEpoch).Value!;
            var createdOn = new DateOnly(month.Year, month.Month, createdDay);

            return new FinanceMonthPlan(plan, createdOn, updatedOn ?? createdOn);
        }

        private static FinanceMonth Month(YearMonth month, decimal income = 0, decimal expense = 0, decimal investment = 0)
            => new(month, income, expense, investment);

        private static FinanceMonthCounts Count(FinanceMonth[] months, FinanceMonthPlan[] plans, YearMonth? lastClosed = null)
            => FinanceMonthProgress.Count(months, plans, lastClosed ?? October);

        [Fact]
        public void Count_ShouldCountAMonthWithinTheSpendingLimit()
        {
            var counts = Count([Month(October, expense: 3000)], [Plan(October)]);

            Assert.Equal(1, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldNotCountTheBudget_WhenSpendingPassedTheLimit()
        {
            var counts = Count([Month(October, expense: 4000.01m)], [Plan(October)]);

            Assert.Equal(0, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldNotCountTheBudget_WhenSpendingIsUnderHalfTheLimit()
        {
            // An inflated total to pass easily: 1999.99 of a 4000 limit.
            var counts = Count([Month(October, expense: 1999.99m)], [Plan(October)]);

            Assert.Equal(0, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldCountTheBudget_AtExactlyHalfTheLimit()
        {
            var counts = Count([Month(October, expense: 2000)], [Plan(October)]);

            Assert.Equal(1, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldNotCountAPlan_CreatedAfterTheFifthDay()
        {
            var counts = Count([Month(October, expense: 3000, investment: 1000)], [Plan(October, createdDay: 6)]);

            Assert.Equal(0, counts.BudgetMonths);
            Assert.Equal(0, counts.InvestmentMonths);
        }

        [Fact]
        public void Count_ShouldCountAPlan_CreatedBeforeTheMonthStarted()
        {
            var plan = Plan(October) with { CreatedOn = new DateOnly(2026, 9, 20) };

            var counts = Count([Month(October, expense: 3000)], [plan]);

            Assert.Equal(1, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldNotCountAPlan_ChangedAfterTheMonthEnded()
        {
            var counts = Count([Month(October, expense: 3000, investment: 1000)], [Plan(October, updatedOn: new DateOnly(2026, 11, 1))]);

            Assert.Equal(0, counts.BudgetMonths);
            Assert.Equal(0, counts.InvestmentMonths);
        }

        [Fact]
        public void Count_ShouldCountAPlan_ChangedOnTheMonthsLastDay()
        {
            var counts = Count([Month(October, expense: 3000)], [Plan(October, updatedOn: new DateOnly(2026, 10, 31))]);

            Assert.Equal(1, counts.BudgetMonths);
        }

        [Fact]
        public void Count_ShouldLeaveOutMonthsAfterTheLastClosedOne()
        {
            var counts = Count(
                [Month(September, income: 1000, expense: 3000, investment: 1000), Month(October, income: 1000, expense: 3000, investment: 1000)],
                [Plan(September), Plan(October)],
                lastClosed: September);

            Assert.Equal(1, counts.BudgetMonths);
            Assert.Equal(1, counts.InvestmentMonths);
        }

        [Fact]
        public void Count_ShouldLeaveOutTheSavingsOfMonthsAfterTheLastClosedOne()
        {
            var counts = Count(
                [Month(September, income: 5000, expense: 1000), Month(October, income: 5000, expense: 1000)],
                [],
                lastClosed: September);

            Assert.Equal(1, counts.SavingsMonths);
        }

        [Fact]
        public void Count_ShouldCountTheInvestment_WhenTheSliceWasReached()
        {
            var counts = Count([Month(October, investment: 1000), Month(September, investment: 999.99m)], [Plan(October), Plan(September)]);

            Assert.Equal(1, counts.InvestmentMonths);
        }

        [Fact]
        public void Count_ShouldNotCountTheInvestment_WithoutAnInvestmentSlice()
        {
            var counts = Count([Month(October, expense: 3000, investment: 1000)], [Plan(October, withInvestment: false)]);

            Assert.Equal(0, counts.InvestmentMonths);
        }

        [Fact]
        public void Count_ShouldTreatAPlannedMonthWithoutTransactionsAsZeros()
        {
            var counts = Count([], [Plan(October)]);

            Assert.Equal(new FinanceMonthCounts(0, 0, 0), counts);
        }

        [Fact]
        public void Count_ShouldCountTheSavings_AtTwentyPercentOfIncome_WithoutAPlan()
        {
            var counts = Count(
            [
                Month(October, income: 5000, expense: 4000),
                Month(September, income: 5000, expense: 4000.01m)
            ], []);

            Assert.Equal(1, counts.SavingsMonths);
        }

        [Fact]
        public void Count_ShouldNotCountTheSavings_WithoutIncome()
        {
            var counts = Count([Month(October, income: 0, expense: 0)], []);

            Assert.Equal(0, counts.SavingsMonths);
        }
    }
}
