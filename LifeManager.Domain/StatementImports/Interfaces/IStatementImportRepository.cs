using LifeManager.Domain.Categorization;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.StatementImports.Interfaces
{
    /// <remarks>
    /// Rows are only reached through their import's id: callers load the import with its user first
    /// (<see cref="GetByIdAsync"/>), so another user's rows are never reachable.
    /// </remarks>
    public interface IStatementImportRepository
    {
        /// <summary>Stores the draft and its rows together.</summary>
        Task<StatementImport> AddAsync(StatementImport statementImport, IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken);

        Task<StatementImport?> GetByIdAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken);

        /// <summary>Drafts first, then newest first.</summary>
        Task<PagedList<StatementImport>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken);

        /// <summary>The draft's rows grouped by month, chronologically, each with the user's month when it exists.</summary>
        Task<IReadOnlyList<StatementImportMonth>> GetMonthsAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken);

        /// <param name="month">Null means every month.</param>
        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); matches the description or the bank's text.</param>
        Task<PagedList<StatementImportRowListItem>> GetRowsPagedAsync(
            StatementImportId statementImportId,
            PageRequest pageRequest,
            YearMonth? month,
            StatementImportRowFilter filter,
            string? normalizedSearch,
            StatementImportRowSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken);

        Task<StatementImportRowListItem?> GetRowAsync(StatementImportId statementImportId, StatementImportRowId rowId, CancellationToken cancellationToken);

        /// <summary>The rows among <paramref name="rowIds"/> that belong to the import.</summary>
        Task<IReadOnlyList<StatementImportRow>> GetRowsAsync(StatementImportId statementImportId, IReadOnlyCollection<StatementImportRowId> rowIds, CancellationToken cancellationToken);

        /// <summary>The rows of the same merchant the user hasn't edited directly (see <see cref="StatementImportRow.EditedByUser"/>).</summary>
        Task<IReadOnlyList<StatementImportRow>> GetUneditedRowsByMerchantKeyAsync(StatementImportId statementImportId, string merchantKey, CancellationToken cancellationToken);

        Task<IReadOnlyList<StatementImportRow>> GetAllRowsAsync(StatementImportId statementImportId, CancellationToken cancellationToken);

        Task UpdateRowsAsync(IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken);

        /// <summary>Deletes a draft with its rows, or a confirmed import's history entry (its transactions stay).</summary>
        Task<bool> DeleteAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken);

        /// <summary>Deletes every user's drafts created before <paramref name="cutoff"/>, with their rows.</summary>
        /// <returns>How many drafts were deleted.</returns>
        Task<int> DeleteDraftsCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

        /// <summary>
        /// Confirms the draft in one database transaction: locks the import and gives up (null) unless it is still a
        /// draft; then, month by month in id order, locks the month, adds its transactions (skipping any whose bank line
        /// id is already in that month) and recalculates its totals; adds <paramref name="learnings"/> to the user's
        /// rules (used on <paramref name="today"/>); drops the rows and marks the import committed
        /// (<see cref="StatementImport.MarkCommitted"/>, also on <paramref name="statementImport"/>).
        /// </summary>
        /// <param name="transactions">Built for months that already exist (the service opens the missing ones first).</param>
        /// <param name="createdMonthCount">How many of those months the service had to open, stored with the import.</param>
        Task<StatementImportCommitOutcome?> CommitAsync(
            StatementImport statementImport,
            IReadOnlyList<Transaction> transactions,
            IReadOnlyList<RuleLearning> learnings,
            int createdMonthCount,
            DateTimeOffset now,
            DateOnly today,
            CancellationToken cancellationToken);
    }
}
