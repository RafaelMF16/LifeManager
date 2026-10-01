using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.MonthlySummaries
{
    public class MonthlySummaryRepository(LifeManagerDbContext dbContext) : IMonthlySummaryRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<MonthlySummary> AddAsync(MonthlySummary monthlySummary, CancellationToken cancellationToken)
        {
            _dbContext.Add(monthlySummary);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return monthlySummary;
        }

        public async Task<MonthlySummary?> GetByIdAsync(MonthlySummaryId monthlySummaryId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.MonthlySummaries
                .AsNoTracking()
                .SingleOrDefaultAsync(monthlySummary => monthlySummary.Id == monthlySummaryId && monthlySummary.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<MonthlySummary>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            int? year,
            BalanceFilter balanceFilter,
            MonthlySummarySortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.MonthlySummaries
                .AsNoTracking()
                .Where(monthlySummary => monthlySummary.UserId == userId);

            if (year is not null)
            {
                var summaryYear = MonthlySummaryYear.FromPersistence(year.Value);
                query = query.Where(monthlySummary => monthlySummary.Year == summaryYear);
            }

            query = balanceFilter switch
            {
                BalanceFilter.Positive => query.Where(monthlySummary => monthlySummary.BalanceAmount >= 0),
                BalanceFilter.Negative => query.Where(monthlySummary => monthlySummary.BalanceAmount < 0),
                _ => query
            };

            return await Order(query, sortBy, sortDirection).ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<IReadOnlyList<int>> GetYearsByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            var years = await _dbContext.MonthlySummaries
                .AsNoTracking()
                .Where(monthlySummary => monthlySummary.UserId == userId)
                .Select(monthlySummary => monthlySummary.Year)
                .Distinct()
                .OrderByDescending(summaryYear => summaryYear)
                .ToListAsync(cancellationToken);

            return [.. years.Select(summaryYear => summaryYear.Value)];
        }

        public async Task<bool> ExistsAsync(UserId userId, MonthlySummaryMonth month, MonthlySummaryYear year, CancellationToken cancellationToken)
        {
            return await _dbContext.MonthlySummaries
                .AnyAsync(monthlySummary => monthlySummary.UserId == userId && monthlySummary.Year == year && monthlySummary.Month == month, cancellationToken);
        }

        /// <summary>Sorts by the chosen column, then chronologically, ending with Id so pages are stable.</summary>
        private static IOrderedQueryable<MonthlySummary> Order(IQueryable<MonthlySummary> query, MonthlySummarySortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                MonthlySummarySortBy.TotalIncome => descending
                    ? query.OrderByDescending(monthlySummary => monthlySummary.TotalIncome)
                    : query.OrderBy(monthlySummary => monthlySummary.TotalIncome),
                MonthlySummarySortBy.TotalExpense => descending
                    ? query.OrderByDescending(monthlySummary => monthlySummary.TotalExpense)
                    : query.OrderBy(monthlySummary => monthlySummary.TotalExpense),
                MonthlySummarySortBy.Balance => descending
                    ? query.OrderByDescending(monthlySummary => monthlySummary.BalanceAmount)
                    : query.OrderBy(monthlySummary => monthlySummary.BalanceAmount),
                _ => descending
                    ? query.OrderByDescending(monthlySummary => monthlySummary.Year)
                    : query.OrderBy(monthlySummary => monthlySummary.Year)
            };

            return descending
                ? ordered.ThenByDescending(monthlySummary => monthlySummary.Year).ThenByDescending(monthlySummary => monthlySummary.Month).ThenByDescending(monthlySummary => monthlySummary.Id)
                : ordered.ThenBy(monthlySummary => monthlySummary.Year).ThenBy(monthlySummary => monthlySummary.Month).ThenBy(monthlySummary => monthlySummary.Id);
        }
    }
}
