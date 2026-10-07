using LifeManager.Domain.Categories;
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
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using LifeManager.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.RecurringTransactions
{
    public class RecurringTransactionRepository(LifeManagerDbContext dbContext) : IRecurringTransactionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<RecurringTransaction> AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken)
        {
            _dbContext.Add(recurringTransaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return recurringTransaction;
        }

        public async Task<RecurringTransactionListItem?> GetByIdAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken)
        {
            var row = await WithCategory(_dbContext.RecurringTransactions.AsNoTracking()
                    .Where(recurringTransaction => recurringTransaction.Id == recurringTransactionId && recurringTransaction.UserId == userId))
                .Select(row => new { row.RecurringTransaction, CategoryName = row.Category.Name })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : new RecurringTransactionListItem(row.RecurringTransaction, row.CategoryName.Value);
        }

        public async Task<PagedList<RecurringTransactionListItem>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            RecurringTransactionStatusFilter statusFilter,
            string? normalizedSearch,
            RecurringTransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var recurringTransactions = _dbContext.RecurringTransactions
                .AsNoTracking()
                .Where(recurringTransaction => recurringTransaction.UserId == userId);

            recurringTransactions = typeFilter switch
            {
                TransactionTypeFilter.Income => recurringTransactions.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Income),
                TransactionTypeFilter.Expense => recurringTransactions.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Expense),
                TransactionTypeFilter.Investment => recurringTransactions.Where(recurringTransaction => recurringTransaction.Type == MoneyFlowType.Investment),
                _ => recurringTransactions
            };

            // Mirrors RecurringTransaction.Status: finished once there is no next occurrence, paused or active otherwise.
            recurringTransactions = statusFilter switch
            {
                RecurringTransactionStatusFilter.Active => recurringTransactions.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate != null && recurringTransaction.IsActive),
                RecurringTransactionStatusFilter.Paused => recurringTransactions.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate != null && !recurringTransaction.IsActive),
                RecurringTransactionStatusFilter.Finished => recurringTransactions.Where(recurringTransaction => recurringTransaction.NextOccurrenceDate == null),
                _ => recurringTransactions
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                recurringTransactions = recurringTransactions.Where(recurringTransaction => EF.Functions.Like(recurringTransaction.NormalizedDescription, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            var page = await Order(WithCategory(recurringTransactions), sortBy, sortDirection)
                .Select(row => new { row.RecurringTransaction, CategoryName = row.Category.Name })
                .ToPagedListAsync(pageRequest, cancellationToken);

            return page.Map(row => new RecurringTransactionListItem(row.RecurringTransaction, row.CategoryName.Value));
        }

        public async Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.RecurringTransactions
                .AnyAsync(recurringTransaction => recurringTransaction.CategoryId == categoryId && recurringTransaction.UserId == userId, cancellationToken);
        }

        public async Task<bool> TryUpdateAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, CancellationToken cancellationToken)
        {
            var updatedRows = await _dbContext.RecurringTransactions
                .Where(stored => stored.Id == recurringTransaction.Id
                    && stored.UserId == recurringTransaction.UserId
                    && stored.NextMonth == expectedNextMonth)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(stored => stored.Type, recurringTransaction.Type)
                    .SetProperty(stored => stored.CategoryId, recurringTransaction.CategoryId)
                    .SetProperty(stored => stored.Amount, recurringTransaction.Amount)
                    .SetProperty(stored => stored.Description, recurringTransaction.Description)
                    .SetProperty(stored => stored.NormalizedDescription, recurringTransaction.NormalizedDescription)
                    .SetProperty(stored => stored.DayOfMonth, recurringTransaction.DayOfMonth)
                    .SetProperty(stored => stored.StartMonth, recurringTransaction.StartMonth)
                    .SetProperty(stored => stored.EndMonth, recurringTransaction.EndMonth)
                    .SetProperty(stored => stored.IsActive, recurringTransaction.IsActive)
                    .SetProperty(stored => stored.NextMonth, recurringTransaction.NextMonth)
                    .SetProperty(stored => stored.NextOccurrenceDate, recurringTransaction.NextOccurrenceDate), cancellationToken);

            return updatedRows > 0;
        }

        public async Task<bool> DeleteAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken)
        {
            // The transactions it posted stay: their foreign key is SET NULL.
            var deletedRows = await _dbContext.RecurringTransactions
                .Where(recurringTransaction => recurringTransaction.Id == recurringTransactionId && recurringTransaction.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            return deletedRows > 0;
        }

        public async Task<IReadOnlyList<RecurringTransactionId>> GetDueIdsAsync(DateOnly today, int limit, CancellationToken cancellationToken)
        {
            var ids = await _dbContext.RecurringTransactions
                .AsNoTracking()
                .Where(recurringTransaction => recurringTransaction.IsActive
                    && recurringTransaction.NextOccurrenceDate != null
                    && recurringTransaction.NextOccurrenceDate <= today)
                .OrderBy(recurringTransaction => recurringTransaction.NextOccurrenceDate)
                .ThenBy(recurringTransaction => recurringTransaction.Id)
                .Take(limit)
                .Select(recurringTransaction => recurringTransaction.Id!)
                .ToListAsync(cancellationToken);

            return ids;
        }

        public async Task<RecurringTransaction?> GetForPostingAsync(RecurringTransactionId recurringTransactionId, CancellationToken cancellationToken)
        {
            return await _dbContext.RecurringTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(recurringTransaction => recurringTransaction.Id == recurringTransactionId, cancellationToken);
        }

        public async Task<bool> TryPostOccurrenceAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, Transaction transaction, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Compare-and-swap on the cursor: it also locks the recurrence's row, so a concurrent posting of the same
            // occurrence waits here and then finds the cursor already moved.
            var movedRows = await _dbContext.RecurringTransactions
                .Where(stored => stored.Id == recurringTransaction.Id
                    && stored.IsActive
                    && stored.NextMonth == expectedNextMonth)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(stored => stored.NextMonth, recurringTransaction.NextMonth)
                    .SetProperty(stored => stored.NextOccurrenceDate, recurringTransaction.NextOccurrenceDate), cancellationToken);

            if (movedRows == 0)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);
                return false;
            }

            var monthlySummary = await MonthlySummaryTotals.LockAsync(_dbContext, transaction.MonthlySummaryId, cancellationToken);

            _dbContext.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await MonthlySummaryTotals.RecalculateAsync(_dbContext, monthlySummary, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return true;
        }

        private IQueryable<RecurringTransactionWithCategory> WithCategory(IQueryable<RecurringTransaction> recurringTransactions)
            => recurringTransactions.Join(
                _dbContext.Categories.AsNoTracking(),
                recurringTransaction => recurringTransaction.CategoryId,
                category => category.Id,
                (recurringTransaction, category) => new RecurringTransactionWithCategory { RecurringTransaction = recurringTransaction, Category = category });

        /// <summary>
        /// Sorts by the chosen column, then by description, ending with Id so pages are stable. Finished recurrences
        /// (no next occurrence) come last when sorting by it ascending, and first descending (PostgreSQL's NULL order).
        /// </summary>
        private static IOrderedQueryable<RecurringTransactionWithCategory> Order(IQueryable<RecurringTransactionWithCategory> query, RecurringTransactionSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                RecurringTransactionSortBy.Description => descending
                    ? query.OrderByDescending(row => row.RecurringTransaction.NormalizedDescription)
                    : query.OrderBy(row => row.RecurringTransaction.NormalizedDescription),
                RecurringTransactionSortBy.Amount => descending
                    ? query.OrderByDescending(row => row.RecurringTransaction.Amount)
                    : query.OrderBy(row => row.RecurringTransaction.Amount),
                RecurringTransactionSortBy.Day => descending
                    ? query.OrderByDescending(row => row.RecurringTransaction.DayOfMonth)
                    : query.OrderBy(row => row.RecurringTransaction.DayOfMonth),
                _ => descending
                    ? query.OrderByDescending(row => row.RecurringTransaction.NextOccurrenceDate)
                    : query.OrderBy(row => row.RecurringTransaction.NextOccurrenceDate)
            };

            return descending
                ? ordered.ThenByDescending(row => row.RecurringTransaction.NormalizedDescription).ThenByDescending(row => row.RecurringTransaction.Id)
                : ordered.ThenBy(row => row.RecurringTransaction.NormalizedDescription).ThenBy(row => row.RecurringTransaction.Id);
        }

        private sealed class RecurringTransactionWithCategory
        {
            public required RecurringTransaction RecurringTransaction { get; init; }
            public required Category Category { get; init; }
        }
    }
}
