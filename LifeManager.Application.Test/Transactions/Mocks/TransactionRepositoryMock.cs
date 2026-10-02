using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Transactions.Mocks
{
    public class TransactionRepositoryMock : ITransactionRepository
    {
        private readonly TransactionSingleton _instance;

        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public TransactionRepositoryMock()
        {
            _instance = TransactionSingleton.Instance;
        }

        public Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            AddCallCount++;

            var newId = _instance.Count == 0 ? 1 : _instance.Max(stored => stored.Id!.Value) + 1;
            transaction.AssignId(newId);

            _instance.Add(ToDetachedCopy(transaction));
            RecalculateTotals(transaction.MonthlySummaryId);

            return Task.FromResult(transaction);
        }

        public Task<TransactionListItem?> GetByIdAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(transaction => transaction.Id == transactionId && transaction.MonthlySummaryId == monthlySummaryId);

            return Task.FromResult(stored is null ? null : ToListItem(stored));
        }

        public Task<PagedList<TransactionListItem>> GetPagedAsync(
            MonthlySummaryId monthlySummaryId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            CategoryId? categoryId,
            string? normalizedSearch,
            TransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _instance.Where(transaction => transaction.MonthlySummaryId == monthlySummaryId);

            query = typeFilter switch
            {
                TransactionTypeFilter.Income => query.Where(transaction => transaction.Type == MoneyFlowType.Income),
                TransactionTypeFilter.Expense => query.Where(transaction => transaction.Type == MoneyFlowType.Expense),
                _ => query
            };

            if (categoryId is not null)
                query = query.Where(transaction => transaction.CategoryId == categoryId);

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(transaction => transaction.NormalizedDescription.Contains(normalizedSearch, StringComparison.Ordinal));

            var matching = Order(query.Select(ToListItem), sortBy, sortDirection).ToList();
            IReadOnlyList<TransactionListItem> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize)];

            return Task.FromResult(new PagedList<TransactionListItem>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<TransactionCounts> CountByTypeAsync(MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var transactions = _instance.Where(transaction => transaction.MonthlySummaryId == monthlySummaryId).ToList();

            return Task.FromResult(new TransactionCounts(
                transactions.Count(transaction => transaction.Type == MoneyFlowType.Income),
                transactions.Count(transaction => transaction.Type == MoneyFlowType.Expense)));
        }

        public Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            var exists = _instance.Any(transaction =>
                transaction.CategoryId == categoryId
                && MonthlySummarySingleton.Instance.Any(monthlySummary => monthlySummary.Id == transaction.MonthlySummaryId && monthlySummary.UserId == userId));

            return Task.FromResult(exists);
        }

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            UpdateCallCount++;

            var index = _instance.FindIndex(stored => stored.Id == transaction.Id && stored.MonthlySummaryId == transaction.MonthlySummaryId);
            if (index >= 0)
                _instance[index] = ToDetachedCopy(transaction);

            RecalculateTotals(transaction.MonthlySummaryId);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var deletedRows = _instance.RemoveAll(transaction => transaction.Id == transactionId && transaction.MonthlySummaryId == monthlySummaryId);

            RecalculateTotals(monthlySummaryId);

            return Task.FromResult(deletedRows > 0);
        }

        // Mirrors TransactionRepository.WriteAndRecalculateTotalsAsync: totals are always summed from the stored transactions.
        private void RecalculateTotals(MonthlySummaryId monthlySummaryId)
        {
            var monthlySummary = MonthlySummarySingleton.Instance.SingleOrDefault(stored => stored.Id == monthlySummaryId);
            if (monthlySummary is null)
                return;

            var transactions = _instance.Where(transaction => transaction.MonthlySummaryId == monthlySummaryId).ToList();
            var totalIncome = transactions.Where(transaction => transaction.Type == MoneyFlowType.Income).Sum(transaction => transaction.SignedAmount);
            var totalExpense = -transactions.Where(transaction => transaction.Type == MoneyFlowType.Expense).Sum(transaction => transaction.SignedAmount);

            // Fails loudly, like the real repository, so an invalid recalculation can't pass a test silently.
            var applyResult = monthlySummary.ApplyTotals(totalIncome, totalExpense);
            if (!applyResult.IsSuccess)
                throw new InvalidOperationException($"Recalculated totals are invalid: {applyResult.Error.Code}");
        }

        // Mirrors TransactionRepository.Order: chosen column, then date and Id in the same direction.
        private static IOrderedEnumerable<TransactionListItem> Order(IEnumerable<TransactionListItem> query, TransactionSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                TransactionSortBy.Description => descending
                    ? query.OrderByDescending(item => item.Transaction.NormalizedDescription, StringComparer.Ordinal)
                    : query.OrderBy(item => item.Transaction.NormalizedDescription, StringComparer.Ordinal),
                TransactionSortBy.Category => descending
                    ? query.OrderByDescending(item => CategoryNormalizedName(item.Transaction.CategoryId), StringComparer.Ordinal)
                    : query.OrderBy(item => CategoryNormalizedName(item.Transaction.CategoryId), StringComparer.Ordinal),
                TransactionSortBy.Amount => descending
                    ? query.OrderByDescending(item => item.Transaction.SignedAmount)
                    : query.OrderBy(item => item.Transaction.SignedAmount),
                _ => descending
                    ? query.OrderByDescending(item => item.Transaction.TransactionDate)
                    : query.OrderBy(item => item.Transaction.TransactionDate)
            };

            return descending
                ? ordered.ThenByDescending(item => item.Transaction.TransactionDate).ThenByDescending(item => item.Transaction.Id!.Value)
                : ordered.ThenBy(item => item.Transaction.TransactionDate).ThenBy(item => item.Transaction.Id!.Value);
        }

        private static string CategoryNormalizedName(CategoryId categoryId)
            => CategorySingleton.Instance.Single(category => category.Id == categoryId).NormalizedName;

        private static TransactionListItem ToListItem(Transaction transaction)
            => new(
                ToDetachedCopy(transaction),
                CategorySingleton.Instance.Single(category => category.Id == transaction.CategoryId).Name.Value);

        private static Transaction ToDetachedCopy(Transaction transaction)
            => Transaction.FromPersistence(
                transaction.Id!.Value,
                transaction.MonthlySummaryId.Value,
                transaction.Type,
                transaction.CategoryId.Value,
                transaction.Amount.Value,
                transaction.Description.Value,
                transaction.TransactionDate);
    }
}
