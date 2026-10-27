using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.FinanceDashboard.Interfaces
{
    public interface IFinanceDashboardRepository
    {
        /// <summary>
        /// The user's transactions dated between <paramref name="from"/> and <paramref name="to"/> (both included),
        /// summed per category, type and month in the database. Months and categories with nothing are left out.
        /// </summary>
        Task<IReadOnlyList<DashboardCategoryMonthTotal>> GetCategoryMonthTotalsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

        /// <summary>
        /// The user's expenses dated between <paramref name="from"/> and <paramref name="to"/> (both included), summed
        /// and counted per day in the database. Days with nothing are left out.
        /// </summary>
        Task<IReadOnlyList<DashboardDayTotal>> GetExpenseDayTotalsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    }
}
