using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.RecurringTransactions.Enums;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RecurringTransactions.Interfaces
{
    /// <remarks>
    /// Writes that move the posting cursor are conditional on the cursor the caller read
    /// (<c>expectedNextMonth</c>), so a user edit and a posting running at the same time can't both win and
    /// an occurrence is never posted twice.
    /// </remarks>
    public interface IRecurringTransactionRepository
    {
        Task<RecurringTransaction> AddAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken);
        Task<RecurringTransactionListItem?> GetByIdAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken);

        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<RecurringTransactionListItem>> GetPagedByUserIdAsync(
            UserId userId,
            PageRequest pageRequest,
            TransactionTypeFilter typeFilter,
            RecurringTransactionStatusFilter statusFilter,
            string? normalizedSearch,
            RecurringTransactionSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        /// <summary>Whether any of the user's recurring transactions uses the category.</summary>
        Task<bool> ExistsByCategoryAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);

        /// <returns>False when the recurrence is gone or its cursor is no longer <paramref name="expectedNextMonth"/>.</returns>
        Task<bool> TryUpdateAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, CancellationToken cancellationToken);

        Task<bool> DeleteAsync(RecurringTransactionId recurringTransactionId, UserId userId, CancellationToken cancellationToken);

        /// <summary>Every user's active recurrences whose next occurrence is on or before <paramref name="today"/>, oldest first.</summary>
        Task<IReadOnlyList<RecurringTransactionId>> GetDueIdsAsync(DateOnly today, int limit, CancellationToken cancellationToken);

        /// <summary>Loads a recurrence for posting, whoever owns it; the posting runs on behalf of its owner.</summary>
        Task<RecurringTransaction?> GetForPostingAsync(RecurringTransactionId recurringTransactionId, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: moves the cursor to the already advanced <paramref name="recurringTransaction"/>'s
        /// (only if it is still active and at <paramref name="expectedNextMonth"/>), adds <paramref name="transaction"/>
        /// and recalculates its month's totals.
        /// </summary>
        /// <returns>False, with nothing written, when the cursor moved or the recurrence was paused or deleted meanwhile.</returns>
        Task<bool> TryPostOccurrenceAsync(RecurringTransaction recurringTransaction, YearMonth expectedNextMonth, Transaction transaction, CancellationToken cancellationToken);
    }
}
