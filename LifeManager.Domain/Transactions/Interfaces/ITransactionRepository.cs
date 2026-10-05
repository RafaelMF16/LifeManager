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
        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<TransactionListItem>> GetPagedAsync(
            MonthlySummaryId monthlySummaryId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            CategoryId? categoryId,
            string? normalizedSearch,
            TransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        Task<TransactionCounts> CountByTypeAsync(MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken);

        /// <summary>Whether any of the user's transactions uses the category.</summary>
        Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);

        Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(TransactionId transactionId, MonthlySummaryId monthlySummaryId, CancellationToken cancellationToken);
    }
}
