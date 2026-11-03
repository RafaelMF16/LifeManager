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

        /// <summary>When the month's plan was first saved; saving it again keeps it.</summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>When the month's plan was last saved.</summary>
        public DateTimeOffset UpdatedAt { get; private set; }

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

        private BudgetPlan(UserId userId, DateOnly monthStart, BudgetTotal total, List<BudgetSlice> slices, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            UserId = userId;
            MonthStart = monthStart;
            Total = total;
            _slices = slices;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        /// <param name="now">When the plan is saved: its <see cref="CreatedAt"/> and <see cref="UpdatedAt"/>.</param>
        public static Result<BudgetPlan> Create(int idUser, YearMonth month, decimal total, IReadOnlyList<BudgetSlice> slices, DateTimeOffset now)
        {
            var totalResult = BudgetTotal.Create(total);
            if (!totalResult.IsSuccess)
                return totalResult.Error;

            var slicesError = ValidateSlices(slices);
            if (slicesError is not null)
                return slicesError;

            return new BudgetPlan(new UserId(idUser), month.FirstDay, totalResult.Value, [.. slices], now, now);
        }

        /// <summary>
        /// Replaces the whole plan with <paramref name="plan"/>'s total and slices: the same user's month saved again,
        /// already validated by <see cref="Create"/>. Keeps <see cref="CreatedAt"/>; the new plan's instant becomes
        /// <see cref="UpdatedAt"/>.
        /// </summary>
        public void ReplaceWith(BudgetPlan plan)
        {
            if (plan.UserId != UserId || plan.MonthStart != MonthStart)
                throw new InvalidOperationException("A plan can only be replaced by a plan of the same user and month.");

            Total = plan.Total;
            _slices = [.. plan._slices];
            UpdatedAt = plan.CreatedAt;
        }

        /// <summary>Rehydrates a stored plan without re-validating it.</summary>
        internal static BudgetPlan FromPersistence(
            int id,
            int idUser,
            DateOnly monthStart,
            decimal total,
            IReadOnlyList<BudgetSlice> slices,
            DateTimeOffset createdAt,
            DateTimeOffset updatedAt)
        {
            var plan = new BudgetPlan(new UserId(idUser), monthStart, BudgetTotal.FromPersistence(total), [.. slices], createdAt, updatedAt);
            plan.AssignId(id);

            return plan;
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
