using LifeManager.Domain.BudgetPlans.Errors;
using LifeManager.Domain.BudgetPlans.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.BudgetPlans
{
    /// <summary>
    /// A month's budget: a <see cref="Total"/> the user plans to spend and invest, divided into <see cref="Slices"/>
    /// that add up to at most 100%. What the slices leave is free, and spending outside them counts against it. Each
    /// month has at most one plan and changing it never touches another month.
    /// </summary>
    public class BudgetPlan
    {
        private List<BudgetSlice> _slices;

        public BudgetPlanId? Id { get; private set; }
        public UserId UserId { get; }

        /// <summary>First day of <see cref="Month"/>; a plain date column so months can be range-compared in SQL.</summary>
        public DateOnly MonthStart { get; }

        public BudgetTotal Total { get; private set; }

        public IReadOnlyList<BudgetSlice> Slices => _slices;

        public YearMonth Month => YearMonth.From(MonthStart);

        /// <summary>What the slices leave, in percent.</summary>
        public decimal FreePercent => 100 - _slices.Sum(slice => slice.Percent.Value);

        /// <summary>What the slices leave, in money: the total minus the slices' rounded amounts, so it closes with the total.</summary>
        public decimal FreeAmount => Total.Value - _slices.Sum(slice => slice.AmountOf(Total));

        public BudgetSlice? InvestmentSlice => _slices.SingleOrDefault(slice => slice.Type == MoneyFlowType.Investment);

        /// <summary>The most the month may spend: everything but the investment slice, the free part included.</summary>
        public decimal SpendingLimit => Total.Value - (InvestmentSlice?.AmountOf(Total) ?? 0);

        // For EF Core, which sets the owned slices after construction.
        private BudgetPlan(UserId userId, DateOnly monthStart)
        {
            UserId = userId;
            MonthStart = monthStart;
            Total = null!;
            _slices = [];
        }

        private BudgetPlan(UserId userId, DateOnly monthStart, BudgetTotal total, List<BudgetSlice> slices)
        {
            UserId = userId;
            MonthStart = monthStart;
            Total = total;
            _slices = slices;
        }

        public static Result<BudgetPlan> Create(int idUser, YearMonth month, decimal total, IReadOnlyList<BudgetSlice> slices)
        {
            var totalResult = BudgetTotal.Create(total);
            if (!totalResult.IsSuccess)
                return totalResult.Error;

            var slicesError = ValidateSlices(slices);
            if (slicesError is not null)
                return slicesError;

            return new BudgetPlan(new UserId(idUser), month.FirstDay, totalResult.Value, [.. slices]);
        }

        /// <summary>
        /// Replaces the whole plan with <paramref name="plan"/>'s total and slices: the same user's month saved again,
        /// already validated by <see cref="Create"/>.
        /// </summary>
        public void ReplaceWith(BudgetPlan plan)
        {
            if (plan.UserId != UserId || plan.MonthStart != MonthStart)
                throw new InvalidOperationException("A plan can only be replaced by a plan of the same user and month.");

            Total = plan.Total;
            _slices = [.. plan._slices];
        }

        public void AssignId(int id)
        {
            Id = new BudgetPlanId(id);
        }

        private static Error? ValidateSlices(IReadOnlyList<BudgetSlice> slices)
        {
            if (slices.Count(slice => slice.Type == MoneyFlowType.Investment) > 1)
                return BudgetPlanErrors.InvalidInvestmentSlice;

            var categoryIds = slices.Where(slice => slice.CategoryId is not null).Select(slice => slice.CategoryId!.Value).ToList();
            if (categoryIds.Distinct().Count() != categoryIds.Count)
                return BudgetPlanErrors.DuplicateCategory;

            if (slices.Sum(slice => slice.Percent.Value) > 100)
                return BudgetPlanErrors.OverAllocated;

            return null;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not BudgetPlan other)
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
