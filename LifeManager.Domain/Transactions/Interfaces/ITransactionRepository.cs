using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Transactions.Interfaces
{
    /// <remarks>
    /// Every write also recalculates the month's totals (income, expense, investment and balance) from its transactions,
    /// in the same database transaction, so the summary never drifts from what is stored.
    /// </remarks>
    public interface ITransactionRepository
    {
        Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken);
        Task<TransactionListItem?> GetByIdAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken);

        /// <param name="categoryId">Null means every category.</param>
        /// <param name="date">Null means every day.</param>
        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<TransactionListItem>> GetPagedAsync(
            MonthlySummaryId monthlySummaryId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            CategoryId? categoryId,
            DateOnly? date,
            string? normalizedSearch,
            TransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        Task<TransactionCounts> CountByTypeAsync(MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken);

        /// <summary>Whether any of the user's transactions uses the category.</summary>
        Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);

        /// <summary>
        /// The user's transactions a statement import could repeat: dated from <paramref name="from"/> to
        /// <paramref name="to"/> (a few days wider than the statement), or carrying one of <paramref name="externalIds"/>.
        /// </summary>
        Task<IReadOnlyList<ExistingTransaction>> GetForDuplicateCheckAsync(
            UserId userId,
            DateOnly from,
            DateOnly to,
            IReadOnlyCollection<string> externalIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// The user's transactions grouped by normalized description, type and category (one row per group, with its
        /// count and latest date): what category suggestions learn from.
        /// </summary>
        Task<IReadOnlyList<CategoryHistoryEntry>> GetCategoryHistoryAsync(UserId userId, CancellationToken cancellationToken);

        Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken);
    }
}
