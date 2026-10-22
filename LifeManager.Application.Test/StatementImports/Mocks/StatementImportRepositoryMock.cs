using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Transactions.Mocks;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.StatementImports.Mocks
{
    public class StatementImportRepositoryMock : IStatementImportRepository
    {
        private readonly StatementImportSingleton _imports = StatementImportSingleton.Instance;
        private readonly StatementImportRowSingleton _rows = StatementImportRowSingleton.Instance;

        public int CommitCallCount { get; private set; }

        /// <summary>Runs right before the next commit takes its lock, to simulate a concurrent confirmation.</summary>
        public Action? BeforeNextCommit { get; set; }

        public Task<StatementImport> AddAsync(StatementImport statementImport, IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken)
        {
            statementImport.AssignId(_imports.Count == 0 ? 1 : _imports.Max(stored => stored.Id!.Value) + 1);
            _imports.Add(ToDetachedCopy(statementImport));

            var nextRowId = _rows.Count == 0 ? 1 : _rows.Max(stored => stored.Id!.Value) + 1;
            foreach (var row in rows)
            {
                row.AssignImport(statementImport.Id!);
                row.AssignId(nextRowId++);
                _rows.Add(ToDetachedCopy(row));
            }

            return Task.FromResult(statementImport);
        }

        public Task<StatementImport?> GetByIdAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            var stored = _imports.SingleOrDefault(statementImport => statementImport.Id == statementImportId && statementImport.UserId == userId);

            return Task.FromResult(stored is null ? null : ToDetachedCopy(stored));
        }

        // Mirrors StatementImportRepository.GetPagedByUserIdAsync: drafts first, then newest first, Id as tiebreaker.
        public Task<PagedList<StatementImport>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            var matching = _imports
                .Where(statementImport => statementImport.UserId == userId)
                .OrderBy(statementImport => statementImport.Status)
                .ThenByDescending(statementImport => statementImport.CreatedAt)
                .ThenByDescending(statementImport => statementImport.Id!.Value)
                .ToList();

            IReadOnlyList<StatementImport> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<StatementImport>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<IReadOnlyList<StatementImportMonth>> GetMonthsAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            IReadOnlyList<StatementImportMonth> months = [.. RowsOf(statementImportId)
                .GroupBy(row => row.Month)
                .OrderBy(group => group.Key.Ordinal)
                .Select(group => new StatementImportMonth(
                    group.Key,
                    MonthlySummarySingleton.Instance
                        .Where(monthlySummary => monthlySummary.UserId == userId && monthlySummary.Year.Value == group.Key.Year && monthlySummary.Month.Value == group.Key.Month)
                        .Select(monthlySummary => (int?)monthlySummary.Id!.Value)
                        .SingleOrDefault(),
                    group.Count(),
                    group.Count(row => row.Included),
                    group.Count(row => row.Included && row.CategoryId is null),
                    group.Count(IsSuggested),
                    group.Count(row => row.IsDuplicate),
                    group.Where(row => row.Included && row.Type == MoneyFlowType.Income).Sum(row => row.SignedAmount),
                    -group.Where(row => row.Included && row.Type == MoneyFlowType.Expense).Sum(row => row.SignedAmount),
                    -group.Where(row => row.Included && row.Type == MoneyFlowType.Investment).Sum(row => row.SignedAmount)))];

            return Task.FromResult(months);
        }

        public Task<PagedList<StatementImportRowListItem>> GetRowsPagedAsync(
            StatementImportId statementImportId,
            PageRequest pageRequest,
            YearMonth? month,
            StatementImportRowFilter filter,
            string? normalizedSearch,
            StatementImportRowSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var query = RowsOf(statementImportId);

            if (month is not null)
                query = query.Where(row => row.Month == month);

            query = filter switch
            {
                StatementImportRowFilter.Uncategorized => query.Where(row => row.Included && row.CategoryId is null),
                StatementImportRowFilter.Suggested => query.Where(IsSuggested),
                StatementImportRowFilter.Duplicates => query.Where(row => row.IsDuplicate),
                StatementImportRowFilter.Excluded => query.Where(row => !row.Included),
                _ => query
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(row => row.NormalizedDescription.Contains(normalizedSearch, StringComparison.Ordinal)
                    || row.MerchantKey.Contains(normalizedSearch, StringComparison.Ordinal));

            var matching = Order(query.Select(ToListItem), sortBy, sortDirection).ToList();
            IReadOnlyList<StatementImportRowListItem> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize)];

            return Task.FromResult(new PagedList<StatementImportRowListItem>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<StatementImportRowListItem?> GetRowAsync(StatementImportId statementImportId, StatementImportRowId rowId, CancellationToken cancellationToken)
        {
            var stored = RowsOf(statementImportId).SingleOrDefault(row => row.Id == rowId);

            return Task.FromResult(stored is null ? null : ToListItem(stored));
        }

        public Task<IReadOnlyList<StatementImportRow>> GetRowsAsync(StatementImportId statementImportId, IReadOnlyCollection<StatementImportRowId> rowIds, CancellationToken cancellationToken)
        {
            IReadOnlyList<StatementImportRow> rows = [.. RowsOf(statementImportId).Where(row => rowIds.Contains(row.Id!)).Select(ToDetachedCopy)];

            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<StatementImportRow>> GetUneditedRowsByMerchantKeyAsync(StatementImportId statementImportId, string merchantKey, CancellationToken cancellationToken)
        {
            IReadOnlyList<StatementImportRow> rows = [.. RowsOf(statementImportId)
                .Where(row => row.MerchantKey == merchantKey && !row.EditedByUser)
                .Select(ToDetachedCopy)];

            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<StatementImportRow>> GetAllRowsAsync(StatementImportId statementImportId, CancellationToken cancellationToken)
        {
            IReadOnlyList<StatementImportRow> rows = [.. RowsOf(statementImportId)
                .OrderBy(row => row.Date)
                .ThenBy(row => row.Id!.Value)
                .Select(ToDetachedCopy)];

            return Task.FromResult(rows);
        }

        public Task UpdateRowsAsync(IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken)
        {
            foreach (var row in rows)
            {
                var index = _rows.FindIndex(stored => stored.Id == row.Id);
                if (index >= 0)
                    _rows[index] = ToDetachedCopy(row);
            }

            return Task.CompletedTask;
        }

        // Mirrors the cascade: the import's rows go with it; transactions it posted stay (StatementImportId set to null).
        public Task<bool> DeleteAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            var deleted = _imports.RemoveAll(statementImport => statementImport.Id == statementImportId && statementImport.UserId == userId);
            if (deleted == 0)
                return Task.FromResult(false);

            _rows.RemoveAll(row => row.StatementImportId == statementImportId);

            var transactions = TransactionSingleton.Instance;
            for (var index = 0; index < transactions.Count; index++)
            {
                var transaction = transactions[index];
                if (transaction.StatementImportId == statementImportId)
                    transactions[index] = Transaction.FromPersistence(
                        transaction.Id!.Value,
                        transaction.MonthlySummaryId.Value,
                        transaction.Type,
                        transaction.CategoryId.Value,
                        transaction.Amount.Value,
                        transaction.Description.Value,
                        transaction.TransactionDate,
                        transaction.RecurringTransactionId?.Value,
                        idStatementImport: null,
                        transaction.ExternalId);
            }

            return Task.FromResult(true);
        }

        public Task<int> DeleteDraftsCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            var staleIds = _imports.Where(statementImport => statementImport.IsDraft && statementImport.CreatedAt < cutoff).Select(statementImport => statementImport.Id!).ToList();

            _imports.RemoveAll(statementImport => staleIds.Contains(statementImport.Id!));
            _rows.RemoveAll(row => staleIds.Contains(row.StatementImportId));

            return Task.FromResult(staleIds.Count);
        }

        // Mirrors StatementImportRepository.CommitAsync: gives up unless still a draft, skips bank lines already in their
        // month (the unique index), recalculates each month's totals, upserts the rules, drops the rows.
        public Task<StatementImportCommitOutcome?> CommitAsync(
            StatementImport statementImport,
            IReadOnlyList<Transaction> transactions,
            IReadOnlyList<RuleLearning> learnings,
            int createdMonthCount,
            DateTimeOffset now,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            CommitCallCount++;

            var beforeCommit = BeforeNextCommit;
            BeforeNextCommit = null;
            beforeCommit?.Invoke();

            var index = _imports.FindIndex(stored => stored.Id == statementImport.Id && stored.IsDraft);
            if (index < 0)
                return Task.FromResult<StatementImportCommitOutcome?>(null);

            var stored = TransactionSingleton.Instance;
            var imported = 0;
            var skipped = 0;

            foreach (var transaction in transactions)
            {
                if (transaction.ExternalId is not null
                    && stored.Any(existing => existing.MonthlySummaryId == transaction.MonthlySummaryId && existing.ExternalId == transaction.ExternalId))
                {
                    skipped++;
                    continue;
                }

                transaction.AssignId(stored.Count == 0 ? 1 : stored.Max(existing => existing.Id!.Value) + 1);
                stored.Add(TransactionRepositoryMock.ToDetachedCopy(transaction));
                imported++;
            }

            foreach (var monthlySummaryId in transactions.Select(transaction => transaction.MonthlySummaryId).Distinct())
                TransactionRepositoryMock.RecalculateTotals(monthlySummaryId);

            Learn(statementImport.UserId, learnings, today);

            _rows.RemoveAll(row => row.StatementImportId == statementImport.Id);

            statementImport.MarkCommitted(now, imported, createdMonthCount);
            _imports[index] = ToDetachedCopy(statementImport);

            return Task.FromResult<StatementImportCommitOutcome?>(new StatementImportCommitOutcome(imported, skipped));
        }

        // Mirrors the INSERT ... ON CONFLICT DO UPDATE on the rule's unique key.
        private static void Learn(UserId userId, IReadOnlyList<RuleLearning> learnings, DateOnly today)
        {
            var rules = CategorizationRuleSingleton.Instance;

            foreach (var learning in learnings)
            {
                var existing = rules.SingleOrDefault(rule =>
                    rule.UserId == userId
                    && rule.MerchantKey == learning.MerchantKey
                    && rule.Type == learning.Type
                    && rule.Action == learning.Action
                    && rule.CategoryId == learning.CategoryId);

                if (existing is not null)
                {
                    existing.Reinforce(learning.Hits, today);
                    continue;
                }

                rules.Add(CategorizationRule.FromPersistence(
                    rules.Count == 0 ? 1 : rules.Max(rule => rule.Id!.Value) + 1,
                    userId.Value,
                    learning.MerchantKey,
                    learning.Type,
                    learning.Action,
                    learning.CategoryId?.Value,
                    learning.Hits,
                    today));
            }
        }

        private IEnumerable<StatementImportRow> RowsOf(StatementImportId statementImportId)
            => _rows.Where(row => row.StatementImportId == statementImportId);

        private static bool IsSuggested(StatementImportRow row)
            => row.SuggestionSource != SuggestionSource.None && !row.EditedByUser;

        // Mirrors StatementImportRepository.Order: chosen column, then date and Id in the same direction. PostgreSQL sorts
        // a missing category (NULL) last ascending and first descending.
        private static IOrderedEnumerable<StatementImportRowListItem> Order(IEnumerable<StatementImportRowListItem> query, StatementImportRowSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                StatementImportRowSortBy.Description => descending
                    ? query.OrderByDescending(item => item.Row.NormalizedDescription, StringComparer.Ordinal)
                    : query.OrderBy(item => item.Row.NormalizedDescription, StringComparer.Ordinal),
                StatementImportRowSortBy.Category => descending
                    ? query.OrderByDescending(item => item.Row.CategoryId is null).ThenByDescending(item => CategoryNormalizedName(item), StringComparer.Ordinal)
                    : query.OrderBy(item => item.Row.CategoryId is null).ThenBy(item => CategoryNormalizedName(item), StringComparer.Ordinal),
                StatementImportRowSortBy.Amount => descending
                    ? query.OrderByDescending(item => item.Row.SignedAmount)
                    : query.OrderBy(item => item.Row.SignedAmount),
                _ => descending
                    ? query.OrderByDescending(item => item.Row.Date)
                    : query.OrderBy(item => item.Row.Date)
            };

            return descending
                ? ordered.ThenByDescending(item => item.Row.Date).ThenByDescending(item => item.Row.Id!.Value)
                : ordered.ThenBy(item => item.Row.Date).ThenBy(item => item.Row.Id!.Value);
        }

        private static string CategoryNormalizedName(StatementImportRowListItem item)
            => item.Row.CategoryId is null
                ? string.Empty
                : CategorySingleton.Instance.Single(category => category.Id == item.Row.CategoryId).NormalizedName;

        private static StatementImportRowListItem ToListItem(StatementImportRow row)
            => new(
                ToDetachedCopy(row),
                row.CategoryId is null ? null : CategorySingleton.Instance.Single(category => category.Id == row.CategoryId).Name.Value);

        internal static StatementImport ToDetachedCopy(StatementImport statementImport)
            => StatementImport.FromPersistence(
                statementImport.Id!.Value,
                statementImport.UserId.Value,
                statementImport.FileName,
                statementImport.Source,
                statementImport.Status,
                statementImport.RowCount,
                statementImport.FirstDate,
                statementImport.LastDate,
                statementImport.CreatedAt,
                statementImport.CommittedAt,
                statementImport.ImportedCount,
                statementImport.CreatedMonthCount);

        internal static StatementImportRow ToDetachedCopy(StatementImportRow row)
            => StatementImportRow.FromPersistence(
                row.Id!.Value,
                row.StatementImportId.Value,
                row.Date,
                row.OriginalDescription,
                row.MerchantKey,
                row.Description.Value,
                row.Amount.Value,
                row.Type,
                row.CategoryId?.Value,
                row.Included,
                row.ExternalId,
                row.DuplicateOfTransactionId,
                row.DuplicateOfMonthlySummaryId,
                row.SuggestionSource,
                row.SuggestionConfidence,
                row.SuggestionUses,
                row.EditedByUser,
                row.ShiftToNextMonth,
                row.ShiftLearned);
    }
}
