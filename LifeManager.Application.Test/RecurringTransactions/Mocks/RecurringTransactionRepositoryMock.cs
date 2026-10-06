using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Transactions.Mocks;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.RecurringTransactions.Mocks
{
    public class RecurringTransactionRepositoryMock : IRecurringTransactionRepository
    {
        private readonly RecurringTransactionSingleton _instance;

        public int PostCallCount { get; private set; }

        /// <summary>Runs right before the cursor check of the next posting, to simulate a concurrent change.</summary>
        public Action? BeforeNextPost { get; set; }

        /// <summary>Runs right before the cursor check of the next update, to simulate a concurrent change.</summary>
        public Action? BeforeNextUpdate { get; set; }

        public RecurringTransactionRepositoryMock()
        {
            _instance = RecurringTransactionSingleton.Instance;
        }

        public Task<RecurringTransaction> AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(stored => stored.Id!.Value) + 1;
            recurringTransaction.AssignId(newId);

            _instance.Add(ToDetachedCopy(recurringTransaction));

            return Task.FromResult(recurringTransaction);
        }

        public Task<RecurringTransactionListItem?> GetByIdAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(recurringTransaction => recurringTransaction.Id == recurringTransactionId && recurringTransaction.UserId == userId);

            return Task.FromResult(stored is null ? null : ToListItem(stored));
        }

        public Task<PagedList<RecurringTransactionListItem>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            RecurringTransactionStatusFilter statusFilter,
            string? normalizedSearch,
            RecurringTransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = _instance.Where(recurringTransaction => recurringTransaction.UserId == userId);

            query = typeFilter switch
            {
                TransactionTypeFilter.Income => query.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Income),
                TransactionTypeFilter.Expense => query.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Expense),
                TransactionTypeFilter.Investment => query.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Investment),
                _ => query
            };

            query = statusFilter switch
            {
                RecurringTransactionStatusFilter.Active => query.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate != null && recurringTransaction.IsActive),
                RecurringTransactionStatusFilter.Paused => query.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate != null && !recurringTransaction.IsActive),
                RecurringTransactionStatusFilter.Finished => query.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate == null),
                _ => query
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(recurringTransaction => recurringTransaction.NormalizedDescription.Contains(normalizedSearch, StringComparison.Ordinal));

            var matching = Order(query, sortBy, sortDirection).ToList();
            IReadOnlyList<RecurringTransactionListItem> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToListItem)];

            return Task.FromResult(new PagedList<RecurringTransactionListItem>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
            => Task.FromResult(_instance.Any(recurringTransaction => recurringTransaction.CategoryId == categoryId && recurringTransaction.UserId == userId));

        public Task<bool> TryUpdateAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, CancellationToken cancellationToken)
        {
            BeforeNextUpdate?.Invoke();
            BeforeNextUpdate = null;

            var index = _instance.FindIndex(stored =>
                stored.Id == recurringTransaction.Id
                && stored.UserId == recurringTransaction.UserId
                && stored.NextMonth.Equals(expectedNextMonth));
            if (index < 0)
                return Task.FromResult(false);

            _instance[index] = ToDetachedCopy(recurringTransaction);

            return Task.FromResult(true);
        }

        // Mirrors the SET NULL foreign key: the transactions it posted stay, without the link.
        public Task<bool> DeleteAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken)
        {
            var deletedRows = _instance.RemoveAll(recurringTransaction => recurringTransaction.Id == recurringTransactionId && recurringTransaction.UserId == userId);

            if (deletedRows > 0)
            {
                var transactions = TransactionSingleton.Instance;
                for (var index = 0; index < transactions.Count; index++)
                {
                    var transaction = transactions[index];
                    if (transaction.RecurringTransactionId == recurringTransactionId)
                        transactions[index] = Transaction.FromPersistence(
                            transaction.Id!.Value,
                            transaction.MonthlySummaryId.Value,
                            transaction.Type,
                            transaction.CategoryId.Value,
                            transaction.Amount.Value,
                            transaction.Description.Value,
                            transaction.TransactionDate);
                }
            }

            return Task.FromResult(deletedRows > 0);
        }

        public Task<IReadOnlyList<RecurringTransactionId>> GetDueIdsAsync(DateOnly today, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<RecurringTransactionId> ids = [.. _instance
                .Where(recurringTransaction => recurringTransaction.IsActive
                    && recurringTransaction.NextOccurrenceDate != null
                    && recurringTransaction.NextOccurrenceDate <= today)
                .OrderBy(recurringTransaction => recurringTransaction.NextOccurrenceDate)
                .ThenBy(recurringTransaction => recurringTransaction.Id!.Value)
                .Take(limit)
                .Select(recurringTransaction => recurringTransaction.Id!)];

            return Task.FromResult(ids);
        }

        public Task<RecurringTransaction?> GetForPostingAsync(RecurringTransactionId recurringTransactionId, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(recurringTransaction => recurringTransaction.Id == recurringTransactionId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        // Mirrors RecurringTransactionRepository.TryPostOccurrenceAsync: the conditional cursor move, then the insert
        // with its totals recalculation (through the transaction mock, which also enforces the unique index).
        public async Task<bool> TryPostOccurrenceAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, Transaction transaction, CancellationToken cancellationToken)
        {
            BeforeNextPost?.Invoke();
            BeforeNextPost = null;

            var index = _instance.FindIndex(stored =>
                stored.Id == recurringTransaction.Id
                && stored.IsActive
                && stored.NextMonth.Equals(expectedNextMonth));
            if (index < 0)
                return false;

            await new TransactionRepositoryMock().AddAsync(transaction, cancellationToken);

            var stored = _instance[index];
            _instance[index] = RecurringTransaction.FromPersistence(
                stored.Id!.Value,
                stored.UserId.Value,
                stored.Type,
                stored.CategoryId.Value,
                stored.Amount.Value,
                stored.Description.Value,
                stored.DayOfMonth.Value,
                stored.StartMonth,
                stored.EndMonth,
                stored.IsActive,
                recurringTransaction.NextMonth);

            PostCallCount++;

            return true;
        }

        // Mirrors RecurringTransactionRepository.Order, including PostgreSQL's NULL order for finished recurrences
        // (last ascending, first descending), then description and Id in the same direction.
        private static IOrderedEnumerable<RecurringTransaction> Order(IEnumerable<RecurringTransaction> query, RecurringTransactionSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                RecurringTransactionSortBy.Description => descending
                    ? query.OrderByDescending(recurringTransaction => recurringTransaction.NormalizedDescription, StringComparer.Ordinal)
                    : query.OrderBy(recurringTransaction => recurringTransaction.NormalizedDescription, StringComparer.Ordinal),
                RecurringTransactionSortBy.Amount => descending
                    ? query.OrderByDescending(recurringTransaction => recurringTransaction.Amount.Value)
                    : query.OrderBy(recurringTransaction => recurringTransaction.Amount.Value),
                RecurringTransactionSortBy.Day => descending
                    ? query.OrderByDescending(recurringTransaction => recurringTransaction.DayOfMonth.Value)
                    : query.OrderBy(recurringTransaction => recurringTransaction.DayOfMonth.Value),
                _ => descending
                    ? query.OrderByDescending(recurringTransaction => recurringTransaction.NextOccurrenceDate is null).ThenByDescending(recurringTransaction => recurringTransaction.NextOccurrenceDate)
                    : query.OrderBy(recurringTransaction => recurringTransaction.NextOccurrenceDate is null).ThenBy(recurringTransaction => recurringTransaction.NextOccurrenceDate)
            };

            return descending
                ? ordered.ThenByDescending(recurringTransaction => recurringTransaction.NormalizedDescription, StringComparer.Ordinal).ThenByDescending(recurringTransaction => recurringTransaction.Id!.Value)
                : ordered.ThenBy(recurringTransaction => recurringTransaction.NormalizedDescription, StringComparer.Ordinal).ThenBy(recurringTransaction => recurringTransaction.Id!.Value);
        }

        private static RecurringTransactionListItem ToListItem(RecurringTransaction recurringTransaction)
            => new(
                ToDetachedCopy(recurringTransaction),
                CategorySingleton.Instance.Single(category => category.Id == recurringTransaction.CategoryId).Name.Value);

        private static RecurringTransaction ToDetachedCopy(RecurringTransaction recurringTransaction)
            => RecurringTransaction.FromPersistence(
                recurringTransaction.Id!.Value,
                recurringTransaction.UserId.Value,
                recurringTransaction.Type,
                recurringTransaction.CategoryId.Value,
                recurringTransaction.Amount.Value,
                recurringTransaction.Description.Value,
                recurringTransaction.DayOfMonth.Value,
                recurringTransaction.StartMonth,
                recurringTransaction.EndMonth,
                recurringTransaction.IsActive,
                recurringTransaction.NextMonth);
    }
}
