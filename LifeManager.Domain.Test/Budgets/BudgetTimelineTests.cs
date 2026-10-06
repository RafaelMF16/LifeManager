using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.Test.Budgets
{
    public class BudgetTimelineTests
    {
        private static YearMonth Month(string value)
        {
            YearMonth.TryParse(value, out var month);
            return month;
        }

        private static Budget Stored(int id, decimal amount, string from, string? to = null)
            => Budget.FromPersistence(id, 1, MoneyFlowType.Expense, 3, amount, Month(from).FirstDay, to is null ? null : Month(to).FirstDay);

        private static Budget NewVersion(decimal amount, string from)
            => Budget.Create(1, MoneyFlowType.Expense, 3, amount, Month(from)).Value!;

        [Fact]
        public void SetFrom_ShouldAddTheVersion_WhenTheGoalIsNew()
        {
            var newVersion = NewVersion(1200m, "2026-10");

            var change = BudgetTimeline.SetFrom([], newVersion);

            Assert.Empty(change.DeletedIds);
            Assert.Empty(change.Updated);
            Assert.Same(newVersion, change.Added);
        }

        [Fact]
        public void SetFrom_ShouldCloseTheVersionInForce_AndAddTheNewOne()
        {
            var inForce = Stored(1, 1000m, "2026-01");

            var change = BudgetTimeline.SetFrom([inForce], NewVersion(1200m, "2026-10"));

            var closed = Assert.Single(change.Updated);
            Assert.Equal(Month("2026-09"), closed.ToMonth);
            Assert.Equal(1000m, closed.Amount.Value);
            Assert.Equal(1200m, change.Added!.Amount.Value);
            Assert.Empty(change.DeletedIds);
        }

        [Fact]
        public void SetFrom_ShouldReplaceTheLaterVersions()
        {
            var first = Stored(1, 1000m, "2026-01", "2026-05");
            var second = Stored(2, 1100m, "2026-06", "2026-11");
            var third = Stored(3, 1300m, "2026-12");

            var change = BudgetTimeline.SetFrom([first, second, third], NewVersion(1200m, "2026-08"));

            Assert.Equal([3], change.DeletedIds.Select(id => id.Value).ToArray());
            var closed = Assert.Single(change.Updated);
            Assert.Equal(2, closed.Id!.Value);
            Assert.Equal(Month("2026-07"), closed.ToMonth);
            Assert.NotNull(change.Added);
        }

        [Fact]
        public void SetFrom_ShouldRewriteTheVersion_WhenOneStartsInTheSameMonth()
        {
            var sameMonth = Stored(1, 1000m, "2026-10", "2026-12");
            var later = Stored(2, 900m, "2027-01");

            var change = BudgetTimeline.SetFrom([sameMonth, later], NewVersion(1500m, "2026-10"));

            Assert.Equal([2], change.DeletedIds.Select(id => id.Value).ToArray());
            var rewritten = Assert.Single(change.Updated);
            Assert.Equal(1500m, rewritten.Amount.Value);
            Assert.Null(rewritten.ToMonth);
            Assert.Null(change.Added);
        }

        [Fact]
        public void SetFrom_ShouldExtendTheVersionInForce_WhenTheAmountIsTheSame()
        {
            var inForce = Stored(1, 1000m, "2026-01", "2026-11");
            var later = Stored(2, 800m, "2026-12");

            var change = BudgetTimeline.SetFrom([inForce, later], NewVersion(1000m, "2026-10"));

            Assert.Equal([2], change.DeletedIds.Select(id => id.Value).ToArray());
            Assert.Null(Assert.Single(change.Updated).ToMonth);
            Assert.Null(change.Added);
        }

        [Fact]
        public void SetFrom_ShouldLeaveAnEarlierClosedVersionAlone()
        {
            var earlier = Stored(1, 1000m, "2026-01", "2026-03");

            var change = BudgetTimeline.SetFrom([earlier], NewVersion(1200m, "2026-10"));

            Assert.Empty(change.Updated);
            Assert.Empty(change.DeletedIds);
            Assert.Equal(Month("2026-03"), earlier.ToMonth);
        }

        [Fact]
        public void RemoveFrom_ShouldCloseTheVersionInForce_AndDeleteTheLaterOnes()
        {
            var inForce = Stored(1, 1000m, "2026-01", "2026-11");
            var later = Stored(2, 800m, "2026-12");

            var result = BudgetTimeline.RemoveFrom([inForce, later], Month("2026-10"));

            Assert.True(result.IsSuccess);
            Assert.Equal([2], result.Value.DeletedIds.Select(id => id.Value).ToArray());
            Assert.Equal(Month("2026-09"), Assert.Single(result.Value.Updated).ToMonth);
            Assert.Null(result.Value.Added);
        }

        [Fact]
        public void RemoveFrom_ShouldDeleteTheVersion_WhenItStartsInThatMonth()
        {
            var version = Stored(1, 1000m, "2026-10");

            var result = BudgetTimeline.RemoveFrom([version], Month("2026-10"));

            Assert.Equal([1], result.Value!.DeletedIds.Select(id => id.Value).ToArray());
            Assert.Empty(result.Value.Updated);
        }

        [Fact]
        public void RemoveFrom_ShouldReturnNotFound_WhenThereIsNoGoalFromThatMonth()
        {
            var earlier = Stored(1, 1000m, "2026-01", "2026-03");

            var result = BudgetTimeline.RemoveFrom([earlier], Month("2026-10"));

            Assert.Equal(BudgetErrors.NotFound, result.Error);
        }

        [Fact]
        public void GoalIn_ShouldReturnTheVersionInForceThatMonth()
        {
            var first = Stored(1, 1000m, "2026-01", "2026-05");
            var second = Stored(2, 1100m, "2026-06");

            Assert.Null(BudgetTimeline.GoalIn([first, second], Month("2025-12")));
            Assert.Same(first, BudgetTimeline.GoalIn([first, second], Month("2026-05")));
            Assert.Same(second, BudgetTimeline.GoalIn([first, second], Month("2026-06")));
        }
    }
}
