using LifeManager.Domain.Categories;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using LifeManager.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.StatementImports
{
    public class StatementImportRepository(LifeManagerDbContext dbContext) : IStatementImportRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<StatementImport> AddAsync(StatementImport statementImport, IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.Add(statementImport);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var row in rows)
                row.AssignImport(statementImport.Id!);

            _dbContext.AddRange(rows);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return statementImport;
        }

        public async Task<StatementImport?> GetByIdAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.StatementImports
                .AsNoTracking()
                .SingleOrDefaultAsync(statementImport => statementImport.Id == statementImportId && statementImport.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<StatementImport>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            return await _dbContext.StatementImports
                .AsNoTracking()
                .Where(statementImport => statementImport.UserId == userId)
                .OrderBy(statementImport => statementImport.Status)
                .ThenByDescending(statementImport => statementImport.CreatedAt)
                .ThenByDescending(statementImport => statementImport.Id)
                .ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<IReadOnlyList<StatementImportMonth>> GetMonthsAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            var groups = await _dbContext.StatementImportRows
                .AsNoTracking()
                .Where(row => row.StatementImportId == statementImportId)
                .GroupBy(row => new { row.EffectiveDate.Year, row.EffectiveDate.Month })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    RowCount = group.Count(),
                    IncludedCount = group.Count(row => row.Included),
                    UncategorizedCount = group.Count(row => row.Included && row.CategoryId == null),
                    SuggestedCount = group.Count(row => row.SuggestionSource != SuggestionSource.None && !row.EditedByUser),
                    DuplicateCount = group.Count(row => row.DuplicateOfTransactionId != null),
                    TotalIncome = group.Sum(row => row.Included && row.Type == MoneyFlowType.Income ? row.SignedAmount : 0),
                    TotalExpense = group.Sum(row => row.Included && row.Type == MoneyFlowType.Expense ? -row.SignedAmount : 0),
                    TotalInvestment = group.Sum(row => row.Included && row.Type == MoneyFlowType.Investment ? -row.SignedAmount : 0)
                })
                .ToListAsync(cancellationToken);

            // Only the keys of the user's months are read (Year and Month are value objects, matched in memory).
            var userMonths = await _dbContext.MonthlySummaries
                .AsNoTracking()
                .Where(monthlySummary => monthlySummary.UserId == userId)
                .Select(monthlySummary => new { monthlySummary.Id, monthlySummary.Year, monthlySummary.Month })
                .ToListAsync(cancellationToken);

            var monthIds = userMonths.ToDictionary(month => (month.Year.Value, month.Month.Value), month => month.Id!.Value);

            return [.. groups
                .OrderBy(group => group.Year)
                .ThenBy(group => group.Month)
                .Select(group => new StatementImportMonth(
                    YearMonth.From(new DateOnly(group.Year, group.Month, 1)),
                    monthIds.TryGetValue((group.Year, group.Month), out var monthlySummaryId) ? monthlySummaryId : null,
                    group.RowCount,
                    group.IncludedCount,
                    group.UncategorizedCount,
                    group.SuggestedCount,
                    group.DuplicateCount,
                    group.TotalIncome,
                    group.TotalExpense,
                    group.TotalInvestment))];
        }

        public async Task<PagedList<StatementImportRowListItem>> GetRowsPagedAsync(
            StatementImportId statementImportId,
            PageRequest pageRequest,
            YearMonth? month,
            StatementImportRowFilter filter,
            string? normalizedSearch,
            StatementImportRowSortBy sortBy,
            SortDirection sortDirection,
            CancellationToken cancellationToken)
        {
            var rows = _dbContext.StatementImportRows
                .AsNoTracking()
                .Where(row => row.StatementImportId == statementImportId);

            if (month is not null)
            {
                var firstDay = month.FirstDay;
                var lastDay = month.LastDay;
                rows = rows.Where(row => row.EffectiveDate >= firstDay && row.EffectiveDate <= lastDay);
            }

            rows = filter switch
            {
                StatementImportRowFilter.Uncategorized => rows.Where(row => row.Included && row.CategoryId == null),
                StatementImportRowFilter.Suggested => rows.Where(row => row.SuggestionSource != SuggestionSource.None && !row.EditedByUser),
                StatementImportRowFilter.Duplicates => rows.Where(row => row.DuplicateOfTransactionId != null),
                StatementImportRowFilter.Excluded => rows.Where(row => !row.Included),
                _ => rows
            };

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                rows = rows.Where(row =>
                    EF.Functions.Like(row.NormalizedDescription, pattern, QueryablePagingExtensions.LikeEscapeCharacter)
                    || EF.Functions.Like(row.MerchantKey, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            var page = await Order(WithCategory(rows), sortBy, sortDirection).ToPagedListAsync(pageRequest, cancellationToken);

            return page.Map(ToListItem);
        }

        public async Task<StatementImportRowListItem?> GetRowAsync(StatementImportId statementImportId, StatementImportRowId rowId, CancellationToken cancellationToken)
        {
            var row = await WithCategory(_dbContext.StatementImportRows
                    .AsNoTracking()
                    .Where(row => row.Id == rowId && row.StatementImportId == statementImportId))
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : ToListItem(row);
        }

        public async Task<IReadOnlyList<StatementImportRow>> GetRowsAsync(StatementImportId statementImportId, IReadOnlyCollection<StatementImportRowId> rowIds, CancellationToken cancellationToken)
        {
            var ids = rowIds.ToList();

            return await _dbContext.StatementImportRows
                .AsNoTracking()
                .Where(row => row.StatementImportId == statementImportId && ids.Contains(row.Id!))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<StatementImportRow>> GetUneditedRowsByMerchantKeyAsync(StatementImportId statementImportId, string merchantKey, CancellationToken cancellationToken)
        {
            return await _dbContext.StatementImportRows
                .AsNoTracking()
                .Where(row => row.StatementImportId == statementImportId && row.MerchantKey == merchantKey && !row.EditedByUser)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<StatementImportRow>> GetAllRowsAsync(StatementImportId statementImportId, CancellationToken cancellationToken)
        {
            return await _dbContext.StatementImportRows
                .AsNoTracking()
                .Where(row => row.StatementImportId == statementImportId)
                .OrderBy(row => row.Date)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateRowsAsync(IReadOnlyList<StatementImportRow> rows, CancellationToken cancellationToken)
        {
            _dbContext.UpdateRange(rows);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
        }

        public async Task<bool> DeleteAsync(StatementImportId statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            var deletedRows = await _dbContext.StatementImports
                .Where(statementImport => statementImport.Id == statementImportId && statementImport.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            return deletedRows > 0;
        }

        public async Task<int> DeleteDraftsCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            // The rows go with their import (foreign key CASCADE).
            return await _dbContext.StatementImports
                .Where(statementImport => statementImport.Status == StatementImportStatus.Draft && statementImport.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<StatementImportCommitOutcome?> CommitAsync(
            StatementImport statementImport,
            IReadOnlyList<Transaction> transactions,
            IReadOnlyList<RuleLearning> learnings,
            int createdMonthCount,
            DateTimeOffset now,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Locks the draft: a second confirmation waits here, then finds it committed and gives up.
            var importId = statementImport.Id!.Value;
            var draft = (int)StatementImportStatus.Draft;
            var lockedImports = await _dbContext.StatementImports
                .FromSql($"""SELECT * FROM "StatementImports" WHERE "Id" = {importId} AND "Status" = {draft} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (lockedImports.Count == 0)
            {
                await databaseTransaction.RollbackAsync(cancellationToken);
                return null;
            }

            var imported = 0;
            var skipped = 0;

            // Months are locked in id order, so two imports over the same months can't deadlock.
            foreach (var monthTransactions in transactions.GroupBy(transaction => transaction.MonthlySummaryId.Value).OrderBy(group => group.Key))
            {
                var monthlySummary = await MonthlySummaryTotals.LockAsync(_dbContext, new MonthlySummaryId(monthTransactions.Key), cancellationToken);
                var monthlySummaryId = monthlySummary.Id!;

                var externalIds = monthTransactions.Where(transaction => transaction.ExternalId is not null).Select(transaction => transaction.ExternalId!).ToList();
                var alreadyPosted = externalIds.Count == 0
                    ? []
                    : (await _dbContext.Transactions
                        .Where(transaction => transaction.MonthlySummaryId == monthlySummaryId && transaction.ExternalId != null && externalIds.Contains(transaction.ExternalId))
                        .Select(transaction => transaction.ExternalId!)
                        .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

                var toAdd = monthTransactions.Where(transaction => transaction.ExternalId is null || !alreadyPosted.Contains(transaction.ExternalId)).ToList();
                skipped += monthTransactions.Count() - toAdd.Count;

                _dbContext.AddRange(toAdd);
                await _dbContext.SaveChangesAsync(cancellationToken);
                imported += toAdd.Count;

                await MonthlySummaryTotals.RecalculateAsync(_dbContext, monthlySummary, cancellationToken);
            }

            await LearnAsync(statementImport.UserId, learnings, today, cancellationToken);

            await _dbContext.StatementImportRows
                .Where(row => row.StatementImportId == statementImport.Id)
                .ExecuteDeleteAsync(cancellationToken);

            statementImport.MarkCommitted(now, imported, createdMonthCount);
            await _dbContext.StatementImports
                .Where(stored => stored.Id == statementImport.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(stored => stored.Status, statementImport.Status)
                    .SetProperty(stored => stored.CommittedAt, statementImport.CommittedAt)
                    .SetProperty(stored => stored.ImportedCount, statementImport.ImportedCount)
                    .SetProperty(stored => stored.CreatedMonthCount, statementImport.CreatedMonthCount), cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            return new StatementImportCommitOutcome(imported, skipped);
        }

        /// <summary>
        /// Adds each learning's hits to its rule, creating the rule when missing, in one statement (the unique index on
        /// the rule's key makes it an upsert).
        /// </summary>
        private async Task LearnAsync(UserId userId, IReadOnlyList<RuleLearning> learnings, DateOnly today, CancellationToken cancellationToken)
        {
            if (learnings.Count == 0)
                return;

            var idUser = userId.Value;
            var keys = learnings.Select(learning => learning.MerchantKey).ToArray();
            var types = learnings.Select(learning => (int)learning.Type).ToArray();
            var actions = learnings.Select(learning => (int)learning.Action).ToArray();
            var categoryIds = learnings.Select(learning => learning.CategoryId?.Value).ToArray();
            var hits = learnings.Select(learning => learning.Hits).ToArray();

            await _dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "CategorizationRules" ("UserId", "MerchantKey", "Type", "Action", "CategoryId", "Hits", "LastUsedOn")
                SELECT {idUser}, learned.merchant_key, learned.type, learned.action, learned.category_id, learned.hits, {today}
                FROM unnest({keys}::text[], {types}::integer[], {actions}::integer[], {categoryIds}::integer[], {hits}::integer[])
                    AS learned(merchant_key, type, action, category_id, hits)
                ON CONFLICT ("UserId", "MerchantKey", "Type", "Action", "CategoryId")
                DO UPDATE SET
                    "Hits" = "CategorizationRules"."Hits" + EXCLUDED."Hits",
                    "LastUsedOn" = GREATEST("CategorizationRules"."LastUsedOn", EXCLUDED."LastUsedOn")
                """, cancellationToken);
        }

        private IQueryable<RowWithCategory> WithCategory(IQueryable<StatementImportRow> rows)
            => from row in rows
               join category in _dbContext.Categories.AsNoTracking() on row.CategoryId equals category.Id into categories
               from category in categories.DefaultIfEmpty()
               select new RowWithCategory { Row = row, Category = category };

        /// <summary>Sorts by the chosen column, then by date, ending with Id so pages are stable.</summary>
        private static IOrderedQueryable<RowWithCategory> Order(IQueryable<RowWithCategory> query, StatementImportRowSortBy sortBy, SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            var ordered = sortBy switch
            {
                StatementImportRowSortBy.Description => descending
                    ? query.OrderByDescending(item => item.Row.NormalizedDescription)
                    : query.OrderBy(item => item.Row.NormalizedDescription),
                StatementImportRowSortBy.Category => descending
                    ? query.OrderByDescending(item => item.Category == null ? null : item.Category.NormalizedName)
                    : query.OrderBy(item => item.Category == null ? null : item.Category.NormalizedName),
                StatementImportRowSortBy.Amount => descending
                    ? query.OrderByDescending(item => item.Row.SignedAmount)
                    : query.OrderBy(item => item.Row.SignedAmount),
                _ => descending
                    ? query.OrderByDescending(item => item.Row.Date)
                    : query.OrderBy(item => item.Row.Date)
            };

            return descending
                ? ordered.ThenByDescending(item => item.Row.Date).ThenByDescending(item => item.Row.Id)
                : ordered.ThenBy(item => item.Row.Date).ThenBy(item => item.Row.Id);
        }

        private static StatementImportRowListItem ToListItem(RowWithCategory item)
            => new(item.Row, item.Category?.Name.Value);

        private sealed class RowWithCategory
        {
            public required StatementImportRow Row { get; init; }
            public Category? Category { get; init; }
        }
    }
}
