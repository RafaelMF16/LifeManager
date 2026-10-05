using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.MonthlySummaries.Mocks
{
    public class MonthlySummaryRepositoryMock : IMonthlySummaryRepository
    {
        private readonly MonthlySummarySingleton _instance;

        public int ExistsCallCount { get; private set; }
        public int AddCallCount { get; private set; }

        public MonthlySummaryRepositoryMock()
        {
            _instance = MonthlySummarySingleton.Instance;
        }

        public Task<MonthlySummary> AddAsync(MonthlySummary monthlySummary, CancellationToken cancellationToken)
        {
            AddCallCount++;

            var newId = _instance.Count == 0 ? 1 : _instance.Max(stored => stored.Id!.Value) + 1;
            monthlySummary.AssignId(newId);

            _instance.Add(monthlySummary);

            return Task.FromResult(monthlySummary);
        }

        public Task<MonthlySummary?> GetByIdAsync(MonthlySummaryId monthlySummaryId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(monthlySummary => monthlySummary.Id == monthlySummaryId && monthlySummary.UserId == userId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        public Task<PagedList<MonthlySummary>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            int? year,
            BalanceFilter balanceFilter,
            MonthlySummarySortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _instance.Where(monthlySummary => monthlySummary.UserId == userId);

            if (year is not null)
                query = query.Where(monthlySummary => monthlySummary.Year.Value == year.Value);

            query = balanceFilter switch
            {
                BalanceFilter.Positive => query.Where(monthlySummary => monthlySummary.BalanceAmount >= 0),
                BalanceFilter.Negative => query.Where(monthlySummary => monthlySummary.BalanceAmount < 0),
                _ => query
            };

            var matching = Order(query, sortBy, sortDirection).ToList();
            IReadOnlyList<MonthlySummary> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<MonthlySummary>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<IReadOnlyList<int>> GetYearsByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<int> years = [.. _instance
                .Where(monthlySummary => monthlySummary.UserId == userId)
                .Select(monthlySummary => monthlySummary.Year.Value)
                .Distinct()
                .OrderDescending()];

            return Task.FromResult(years);
        }

        public Task<MonthlySummaryNeighbors> GetNeighborsAsync(UserId userId, MonthlySummaryYear year, MonthlySummaryMonth month, CancellationToken cancellationToken)
        {
            var current = year.Value * 100 + month.Value;
            var periods = _instance
                .Where(monthlySummary => monthlySummary.UserId == userId)
                .Select(monthlySummary => (Id: monthlySummary.Id!.Value, Period: monthlySummary.Year.Value * 100 + monthlySummary.Month.Value))
                .OrderBy(period => period.Period)
                .ToList();

            var previous = periods.Where(period => period.Period < current).Select(period => (int?)period.Id).LastOrDefault();
            var next = periods.Where(period => period.Period > current).Select(period => (int?)period.Id).FirstOrDefault();

            return Task.FromResult(new MonthlySummaryNeighbors(previous, next));
        }

        public Task<bool> ExistsAsync(UserId userId, MonthlySummaryMonth month, MonthlySummaryYear year, CancellationToken cancellationToken)
        {
            ExistsCallCount++;

            var exists = _instance.Any(monthlySummary =>
                monthlySummary.UserId == userId
                && monthlySummary.Year.Equals(year)
                && monthlySummary.Month.Equals(month));

            return Task.FromResult(exists);
        }

        // Mirrors MonthlySummaryRepository.Order: chosen column, then year, month and Id in the same direction.
        private static IOrderedEnumerable<MonthlySummary> Order(IEnumerable<MonthlySummary> query, MonthlySummarySortBy sortBy, SortDirection sortDirection)
        {
            Func<MonthlySummary, decimal> key = sortBy switch
            {
                MonthlySummarySortBy.TotalIncome => monthlySummary => monthlySummary.TotalIncome.Value,
                MonthlySummarySortBy.TotalExpense => monthlySummary => monthlySummary.TotalExpense.Value,
                MonthlySummarySortBy.TotalInvestment => monthlySummary => monthlySummary.TotalInvestment.Value,
                MonthlySummarySortBy.Balance => monthlySummary => monthlySummary.BalanceAmount,
                _ => monthlySummary => monthlySummary.Year.Value
            };

            return sortDirection == SortDirection.Desc
                ? query.OrderByDescending(key)
                    .ThenByDescending(monthlySummary => monthlySummary.Year.Value)
                    .ThenByDescending(monthlySummary => monthlySummary.Month.Value)
                    .ThenByDescending(monthlySummary => monthlySummary.Id!.Value)
                : query.OrderBy(key)
                    .ThenBy(monthlySummary => monthlySummary.Year.Value)
                    .ThenBy(monthlySummary => monthlySummary.Month.Value)
                    .ThenBy(monthlySummary => monthlySummary.Id!.Value);
        }

        private static MonthlySummary ToDetachedCopy(MonthlySummary monthlySummary)
            => MonthlySummary.FromPersistence(
                monthlySummary.Id!.Value,
                monthlySummary.UserId.Value,
                monthlySummary.Month.Value,
                monthlySummary.Year.Value,
                monthlySummary.TotalIncome.Value,
                monthlySummary.TotalExpense.Value,
                monthlySummary.TotalInvestment.Value);
    }
}
