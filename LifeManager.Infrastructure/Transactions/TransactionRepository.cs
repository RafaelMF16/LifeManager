using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Transactions
{
    public class TransactionRepository(LifeManagerDbContext dbContext) : ITransactionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            await WriteAndRecalculateTotalsAsync(transaction.MonthlySummaryId, async () =>
            {
                _dbContext.Add(transaction);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);

            return transaction;
        }

        public async Task<TransactionListItem?> GetByIdAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var row = await WithCategory(_dbContext.Transactions.AsNoTracking()
                    .Where(transaction => transaction.Id == transactionId && transaction.MonthlySummaryId == monthlySummaryId))
                .Select(row => new { row.Transaction, CategoryName = row.Category.Name })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : new TransactionListItem(row.Transaction, row.CategoryName.Value);
        }

        public async Task<PagedList<TransactionListItem>> GetPagedAsync(
            MonthlySummaryId monthlySummaryId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            CategoryId? categoryId,
            string? normalizedSearch,
            TransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var transactions = _dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.MonthlySummaryId == monthlySummaryId);

            transactions = typeFilter switch
            {
                TransactionTypeFilter.Income => transactions.Where(transaction => transaction.Type == MoneyFlowType.Income),
                TransactionTypeFilter.Expense => transactions.Where(transaction => transaction.Type == MoneyFlowType.Expense),
                TransactionTypeFilter.Investment => transactions.Where(transaction => transaction.Type == MoneyFlowType.Investment),
                _ => transactions
            };

            if (categoryId is not null)
                transactions = transactions.Where(transaction => transaction.CategoryId == categoryId);

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                transactions = transactions.Where(transaction => EF.Functions.Like(transaction.NormalizedDescription, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            var page = await Order(WithCategory(transactions), sortBy, sortDirection)
                .Select(row => new { row.Transaction, CategoryName = row.Category.Name })
                .ToPagedListAsync(pageRequest, cancellationToken);

            return page.Map(row => new TransactionListItem(row.Transaction, row.CategoryName.Value));
        }

        public async Task<TransactionCounts> CountByTypeAsync(MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            var counts = await _dbContext.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.MonthlySummaryId == monthlySummaryId)
                .GroupBy(transaction => transaction.Type)
                .Select(group => new { Type = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            return new TransactionCounts(
                counts.Where(count => count.Type == MoneyFlowType.Income).Sum(count => count.Count),
                counts.Where(count => count.Type == MoneyFlowType.Expense).Sum(count => count.Count),
                counts.Where(count => count.Type == MoneyFlowType.Investment).Sum(count => count.Count));
        }

        public async Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Transactions
                .AnyAsync(transaction => transaction.CategoryId == categoryId
                    && _dbContext.MonthlySummaries.Any(monthlySummary => monthlySummary.Id == transaction.MonthlySummaryId && monthlySummary.UserId == userId),
                    cancellationToken);
        }

        public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            await WriteAndRecalculateTotalsAsync(transaction.MonthlySummaryId, async () =>
            {
                await _dbContext.Transactions
                    .Where(storedTransaction => storedTransaction.Id == transaction.Id && storedTransaction.MonthlySummaryId == transaction.MonthlySummaryId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(storedTransaction => storedTransaction.Type, transaction.Type)
                        .SetProperty(storedTransaction => storedTransaction.CategoryId, transaction.CategoryId)
                        .SetProperty(storedTransaction => storedTransaction.Amount, transaction.Amount)
                        .SetProperty(storedTransaction => storedTransaction.SignedAmount, transaction.SignedAmount)
                        .SetProperty(storedTransaction => storedTransaction.Description, transaction.Description)
                        .SetProperty(storedTransaction => storedTransaction.NormalizedDescription, transaction.NormalizedDescription)
                        .SetProperty(storedTransaction => storedTransaction.TransactionDate, transaction.TransactionDate), cancellationToken);
                return true;
            }, cancellationToken);
        }

        public async Task<bool> DeleteAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken)
        {
            return await WriteAndRecalculateTotalsAsync(monthlySummaryId, async () =>
            {
                var deletedRows = await _dbContext.Transactions
                    .Where(transaction => transaction.Id == transactionId && transaction.MonthlySummaryId == monthlySummaryId)
                    .ExecuteDeleteAsync(cancellationToken);

                return deletedRows > 0;
            }, cancellationToken);
        }

        /// <summary>
        /// Runs the write and recalculates the month's totals from its transactions in one database transaction.
        /// The month's row is locked first (FOR UPDATE), so concurrent writes to the same month run one after
        /// the other and the last recalculation always sees every committed transaction.
        /// </summary>
        private async Task<T> WriteAndRecalculateTotalsAsync<T>(MonthlySummaryId monthlySummaryId, Func<Task<T>> write, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var id = monthlySummaryId.Value;
            var lockedSummaries = await _dbContext.MonthlySummaries
                .FromSql($"""SELECT * FROM "MonthlySummaries" WHERE "Id" = {id} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var monthlySummary = lockedSummaries.Single();

            var result = await write();

            var totals = await _dbContext.Transactions
                .Where(transaction => transaction.MonthlySummaryId == monthlySummaryId)
                .GroupBy(transaction => transaction.Type)
                .Select(group => new { Type = group.Key, Total = group.Sum(transaction => transaction.SignedAmount) })
                .ToListAsync(cancellationToken);

            var totalIncome = totals.Where(total => total.Type == MoneyFlowType.Income).Sum(total => total.Total);
            var totalExpense = -totals.Where(total => total.Type == MoneyFlowType.Expense).Sum(total => total.Total);
            var totalInvestment = -totals.Where(total => total.Type == MoneyFlowType.Investment).Sum(total => total.Total);

            var applyResult = monthlySummary.ApplyTotals(totalIncome, totalExpense, totalInvestment);
            if (!applyResult.IsSuccess)
                throw new InvalidOperationException($"Recalculated totals are invalid: {applyResult.Error.Code}");

            await _dbContext.MonthlySummaries
                .Where(storedSummary => storedSummary.Id == monthlySummaryId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedSummary => storedSummary.TotalIncome, monthlySummary.TotalIncome)
                    .SetProperty(storedSummary => storedSummary.TotalExpense, monthlySummary.TotalExpense)
                    .SetProperty(storedSummary => storedSummary.TotalInvestment, monthlySummary.TotalInvestment)
                    .SetProperty(storedSummary => storedSummary.BalanceAmount, monthlySummary.BalanceAmount), cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return result;
        }

        private IQueryable<TransactionWithCategory> WithCategory(IQueryable<Transaction> transactions)
            => transactions.Join(
                _dbContext.Categories.AsNoTracking(),
                transaction => transaction.CategoryId,
                category => category.Id,
                (transaction, category) => new TransactionWithCategory { Transaction = transaction, Category = category });

        /// <summary>Sorts by the chosen column, then by date, ending with Id so pages are stable.</summary>
        private static IOrderedQueryable<TransactionWithCategory> Order(IQueryable<TransactionWithCategory> query, TransactionSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                TransactionSortBy.Description => descending
                    ? query.OrderByDescending(row => row.Transaction.NormalizedDescription)
                    : query.OrderBy(row => row.Transaction.NormalizedDescription),
                TransactionSortBy.Category => descending
                    ? query.OrderByDescending(row => row.Category.NormalizedName)
                    : query.OrderBy(row => row.Category.NormalizedName),
                TransactionSortBy.Amount => descending
                    ? query.OrderByDescending(row => row.Transaction.SignedAmount)
                    : query.OrderBy(row => row.Transaction.SignedAmount),
                _ => descending
                    ? query.OrderByDescending(row => row.Transaction.TransactionDate)
                    : query.OrderBy(row => row.Transaction.TransactionDate)
            };

            return descending
                ? ordered.ThenByDescending(row => row.Transaction.TransactionDate).ThenByDescending(row => row.Transaction.Id)
                : ordered.ThenBy(row => row.Transaction.TransactionDate).ThenBy(row => row.Transaction.Id);
        }

        private sealed class TransactionWithCategory
        {
            public required Transaction Transaction { get; init; }
            public required Category Category { get; init; }
        }
    }
}
