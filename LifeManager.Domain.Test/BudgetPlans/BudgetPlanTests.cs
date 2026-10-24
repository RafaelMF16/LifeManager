using System.Globalization;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.BudgetPlans
{
    public class BudgetPlanTests
    {
        private static YearMonth Month(string value)
        {
            YearMonth.TryParse(value, out var month);
            return month;
        }

        private static BudgetSlice Expense(int idCategory, decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Expense, idCategory, percent).Value!;

        private static BudgetSlice Investment(decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Investment, null, percent).Value!;

        [Fact]
        public void Create_ShouldReturnThePlan_WhenTheValuesAreValid()
        {
            var result = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m), Expense(4, 25m), Investment(20m)]);

            Assert.True(result.IsSuccess);
            var plan = result.Value;
            Assert.Null(plan.Id);
            Assert.Equal(1, plan.UserId.Value);
            Assert.Equal(new DateOnly(2026, 10, 1), plan.MonthStart);
            Assert.Equal(Month("2026-10"), plan.Month);
            Assert.Equal(6000m, plan.Total.Value);
            Assert.Equal(3, plan.Slices.Count);
        }

        [Fact]
        public void Create_ShouldAcceptAPlanWithoutSlices_LeavingEverythingFree()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, []).Value!;

            Assert.Equal(100m, plan.FreePercent);
            Assert.Equal(6000m, plan.FreeAmount);
        }

        [Fact]
        public void Create_ShouldAcceptSlicesAddingUpToExactlyAHundred()
        {
            Assert.True(BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 60m), Investment(40m)]).IsSuccess);
        }

        [Fact]
        public void Create_ShouldReturnOverAllocated_WhenSlicesAddUpToMoreThanAHundred()
        {
            var result = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 60m), Investment(40.01m)]);

            Assert.Equal(BudgetPlanErrors.OverAllocated, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnDuplicateCategory_WhenACategoryHasTwoSlices()
        {
            var result = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 10m), Expense(3, 15m)]);

            Assert.Equal(BudgetPlanErrors.DuplicateCategory, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnInvalidInvestmentSlice_WhenThereAreTwoInvestmentSlices()
        {
            var result = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Investment(10m), Investment(15m)]);

            Assert.Equal(BudgetPlanErrors.InvalidInvestmentSlice, result.Error);
        }

        [Theory]
        [InlineData(0, "BudgetPlan.TotalNotPositive")]
        [InlineData(-5, "BudgetPlan.TotalNotPositive")]
        [InlineData(10.123, "BudgetPlan.TotalTooManyDecimals")]
        [InlineData(1_000_000_000_000, "BudgetPlan.TotalTooLarge")]
        public void Create_ShouldReturnTotalError_WhenTheTotalIsInvalid(decimal total, string expectedCode)
        {
            Assert.Equal(expectedCode, BudgetPlan.Create(1, Month("2026-10"), total, []).Error!.Code);
        }

        [Fact]
        public void ReplaceWith_ShouldTakeTheTotalAndSlices_KeepingTheIdAndMonth()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m)]).Value!;
            plan.AssignId(7);

            plan.ReplaceWith(BudgetPlan.Create(1, Month("2026-10"), 8000m, [Expense(4, 10m), Investment(20m)]).Value!);

            Assert.Equal(8000m, plan.Total.Value);
            Assert.Equal([4, null], plan.Slices.Select(slice => slice.CategoryId?.Value));
            Assert.Equal(7, plan.Id!.Value);
            Assert.Equal(Month("2026-10"), plan.Month);
        }

        [Theory]
        [InlineData(1, "2026-11")]
        [InlineData(2, "2026-10")]
        public void ReplaceWith_ShouldThrow_WhenThePlanIsOfAnotherUserOrMonth(int idUser, string month)
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m)]).Value!;
            var other = BudgetPlan.Create(idUser, Month(month), 8000m, []).Value!;

            Assert.Throws<InvalidOperationException>(() => plan.ReplaceWith(other));
            Assert.Equal(6000m, plan.Total.Value);
        }

        [Fact]
        public void FreePercentAndAmount_ShouldBeWhatTheSlicesLeave()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m), Expense(4, 25m), Investment(20m)]).Value!;

            Assert.Equal(25m, plan.FreePercent);
            Assert.Equal(1500m, plan.FreeAmount);
        }

        [Theory]
        [InlineData(100, "33.33;33.33;33.33")]
        [InlineData(0.07, "50;50")]
        [InlineData(1234.57, "12.5;7.77;61.11")]
        [InlineData(999.99, "0.01;0.01")]
        public void FreeAmount_ShouldCloseWithTheTotal_WhateverTheRounding(decimal total, string percents)
        {
            var slices = percents.Split(';')
                .Select((percent, index) => Expense(index + 1, decimal.Parse(percent, CultureInfo.InvariantCulture)))
                .ToList();
            var plan = BudgetPlan.Create(1, Month("2026-10"), total, slices).Value!;

            Assert.Equal(total, plan.FreeAmount + plan.Slices.Sum(slice => slice.AmountOf(plan.Total)));
        }

        [Fact]
        public void InvestmentSlice_ShouldBeTheSliceWithoutCategory()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m), Investment(20m)]).Value!;

            Assert.Equal(20m, plan.InvestmentSlice!.Percent.Value);
        }

        [Fact]
        public void SpendingLimit_ShouldBeTheTotalMinusTheInvestmentSlice_IncludingTheFreePart()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m), Investment(20m)]).Value!;

            Assert.Equal(4800m, plan.SpendingLimit);
        }

        [Fact]
        public void SpendingLimit_ShouldBeTheWholeTotal_WithoutAnInvestmentSlice()
        {
            var plan = BudgetPlan.Create(1, Month("2026-10"), 6000m, [Expense(3, 30m)]).Value!;

            Assert.Null(plan.InvestmentSlice);
            Assert.Equal(6000m, plan.SpendingLimit);
        }
    }
}
