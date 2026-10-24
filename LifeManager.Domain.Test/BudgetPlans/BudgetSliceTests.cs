using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.BudgetPlans.ValueObjects;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Test.BudgetPlans
{
    public class BudgetSliceTests
    {
        private static BudgetTotal Total(decimal value) => BudgetTotal.Create(value).Value!;

        [Fact]
        public void Create_ShouldReturnAnExpenseSlice_WithItsCategory()
        {
            var slice = BudgetSlice.Create(MoneyFlowType.Expense, 3, 25m).Value!;

            Assert.Equal(MoneyFlowType.Expense, slice.Type);
            Assert.Equal(3, slice.CategoryId!.Value);
            Assert.Equal(25m, slice.Percent.Value);
        }

        [Fact]
        public void Create_ShouldReturnAnInvestmentSlice_WithoutCategory()
        {
            var slice = BudgetSlice.Create(MoneyFlowType.Investment, null, 20m).Value!;

            Assert.Null(slice.CategoryId);
        }

        [Fact]
        public void Create_ShouldReturnExpenseSliceWithoutCategory_WhenAnExpenseHasNoCategory()
        {
            Assert.Equal(BudgetPlanErrors.ExpenseSliceWithoutCategory, BudgetSlice.Create(MoneyFlowType.Expense, null, 25m).Error);
        }

        [Fact]
        public void Create_ShouldReturnInvalidInvestmentSlice_WhenAnInvestmentHasACategory()
        {
            Assert.Equal(BudgetPlanErrors.InvalidInvestmentSlice, BudgetSlice.Create(MoneyFlowType.Investment, 3, 25m).Error);
        }

        [Theory]
        [InlineData(MoneyFlowType.Income)]
        [InlineData((MoneyFlowType)99)]
        public void Create_ShouldReturnInvalidType_WhenNotExpenseOrInvestment(MoneyFlowType type)
        {
            Assert.Equal(BudgetPlanErrors.InvalidType, BudgetSlice.Create(type, 3, 25m).Error);
        }

        [Fact]
        public void Create_ShouldReturnThePercentError_WhenThePercentIsInvalid()
        {
            Assert.Equal(BudgetPlanErrors.PercentOutOfRange, BudgetSlice.Create(MoneyFlowType.Expense, 3, 0m).Error);
        }

        [Theory]
        [InlineData(6000, 25, 1500)]
        [InlineData(100, 33.33, 33.33)]
        [InlineData(10.05, 50, 5.03)]
        [InlineData(1, 0.01, 0)]
        public void AmountOf_ShouldBeTheShareOfTheTotal_RoundedToCents(decimal total, decimal percent, decimal expected)
        {
            var slice = BudgetSlice.Create(MoneyFlowType.Expense, 3, percent).Value!;

            Assert.Equal(expected, slice.AmountOf(Total(total)));
        }

        [Theory]
        [InlineData(MoneyFlowType.Expense, 1200, true)]
        [InlineData(MoneyFlowType.Expense, 1500, true)]
        [InlineData(MoneyFlowType.Expense, 1500.01, false)]
        [InlineData(MoneyFlowType.Investment, 1499.99, false)]
        [InlineData(MoneyFlowType.Investment, 1500, true)]
        [InlineData(MoneyFlowType.Investment, 2000, true)]
        public void IsMetBy_ShouldCompareWithTheSliceAmount_AsALimitOrATarget(MoneyFlowType type, decimal actual, bool expected)
        {
            var slice = BudgetSlice.Create(type, type == MoneyFlowType.Expense ? 3 : null, 25m).Value!;

            Assert.Equal(expected, slice.IsMetBy(actual, Total(6000m)));
        }

        [Fact]
        public void ProgressOf_ShouldBeTheActualOverTheSliceAmount_RoundedToFourPlaces()
        {
            var slice = BudgetSlice.Create(MoneyFlowType.Expense, 3, 25m).Value!;

            Assert.Equal(0.8333m, slice.ProgressOf(1250m, Total(6000m)));
        }

        [Fact]
        public void ProgressOf_ShouldNotDivideByZero_WhenTheSliceRoundsToNoCents()
        {
            var slice = BudgetSlice.Create(MoneyFlowType.Expense, 3, 0.01m).Value!;

            Assert.Equal(1m, slice.ProgressOf(0.0001m, Total(1m)));
        }
    }
}
