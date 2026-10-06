using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
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

        public async Task<MonthlySummaryNeighbors> GetNeighborsAsync(UserId userId, MonthlySummaryYear year, MonthlySummaryMonth month, CancellationToken cancellationToken)
        {
            // Only the keys of the user's months (one row per month ever opened) are read: Year and Month are
            // value objects, so "before/after this period" can't be expressed as a SQL comparison.
            var periods = await _dbContext.MonthlySummaries
                .AsNoTracking()
                .Where(monthlySummary => monthlySummary.UserId == userId)
                .OrderBy(monthlySummary => monthlySummary.Year)
                .ThenBy(monthlySummary => monthlySummary.Month)
                .Select(monthlySummary => new { monthlySummary.Id, monthlySummary.Year, monthlySummary.Month })
                .ToListAsync(cancellationToken);

            var current = year.Value * 100 + month.Value;
            var previous = periods.LastOrDefault(period => period.Year.Value * 100 + period.Month.Value < current);
            var next = periods.FirstOrDefault(period => period.Year.Value * 100 + period.Month.Value > current);

            return new MonthlySummaryNeighbors(previous?.Id!.Value, next?.Id!.Value);
        }

        public async Task<bool> ExistsAsync(UserId userId, MonthlySummaryMonth month, MonthlySummaryYear year, CancellationToken cancellationToken)
        {
            return await _dbContext.MonthlySummaries
                .AnyAsync(monthlySummary => monthlySummary.UserId == userId && monthlySummary.Year == year && monthlySummary.Month == month, cancellationToken);
        }

        public async Task<MonthlySummary?> GetByPeriodAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var summaryYear = MonthlySummaryYear.FromPersistence(month.Year);
            var summaryMonth = MonthlySummaryMonth.FromPersistence(month.Month);

            return await _dbContext.MonthlySummaries
                .AsNoTracking()
                .SingleOrDefaultAsync(monthlySummary => monthlySummary.UserId == userId && monthlySummary.Year == summaryYear && monthlySummary.Month == summaryMonth, cancellationToken);
        }

        public async Task<MonthlySummary> AddIfMissingAsync(MonthlySummary monthlySummary, CancellationToken cancellationToken)
        {
            // ON CONFLICT on the unique (UserId, Year, Month) index makes a concurrent insert of the same month a no-op
            // instead of an error, without aborting a surrounding transaction the way a caught unique violation would.
            var userId = monthlySummary.UserId.Value;
            var month = monthlySummary.Month.Value;
            var year = monthlySummary.Year.Value;
            var totalIncome = monthlySummary.TotalIncome.Value;
            var totalExpense = monthlySummary.TotalExpense.Value;
            var totalInvestment = monthlySummary.TotalInvestment.Value;
            var balanceAmount = monthlySummary.BalanceAmount;

            await _dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "MonthlySummaries" ("UserId", "Month", "Year", "TotalIncome", "TotalExpense", "TotalInvestment", "BalanceAmount")
                VALUES ({userId}, {month}, {year}, {totalIncome}, {totalExpense}, {totalInvestment}, {balanceAmount})
                ON CONFLICT ("UserId", "Year", "Month") DO NOTHING
                """, cancellationToken);

            var stored = await GetByPeriodAsync(monthlySummary.UserId, YearMonth.From(new DateOnly(year, month, 1)), cancellationToken);

            return stored ?? throw new InvalidOperationException("The monthly summary was neither added nor found");
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
                MonthlySummarySortBy.TotalInvestment => descending
                    ? query.OrderByDescending(monthlySummary => monthlySummary.TotalInvestment)
                    : query.OrderBy(monthlySummary => monthlySummary.TotalInvestment),
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
