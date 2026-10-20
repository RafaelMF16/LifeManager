using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Application.StatementImports.DTOs;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Interfaces;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.StatementImports.Services
{
    /// <summary>
    /// A bank statement import from upload to confirmation: the draft is created already filed (categories suggested
    /// from what the user did before, investment movements and transfers between the user's own accounts recognized,
    /// month-end lines the user counted in the next month before shifted again, lines they already have left out),
    /// reviewed row by row
    /// (<see cref="StatementImportRowService"/>), then confirmed into transactions across as many months and years as
    /// the statement covers.
    /// </summary>
    public class StatementImportService(
        IStatementImportRepository statementImportRepository,
        ITransactionRepository transactionRepository,
        ICategorizationRuleRepository categorizationRuleRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        ICategoryRepository categoryRepository,
        IUserRepository userRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        public const int AccountKeyMaxLength = 100;

        private readonly IStatementImportRepository _statementImportRepository = statementImportRepository;
        private readonly ITransactionRepository _transactionRepository = transactionRepository;
        private readonly ICategorizationRuleRepository _categorizationRuleRepository = categorizationRuleRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<StatementImportResponseDto>> CreateAsync(StatementImportDto statementImportDto, UserId userId, CancellationToken cancellationToken)
        {
            var lines = statementImportDto.Lines ?? [];

            var lineCountResult = StatementImport.ValidateLineCount(lines.Count);
            if (!lineCountResult.IsSuccess)
                return lineCountResult.Error;

            if (statementImportDto.AccountKey?.Trim().Length > AccountKeyMaxLength)
                return StatementImportErrors.AccountKeyTooLong;

            var today = _appClock.Today();
            var rowsResult = ReadRows(lines, statementImportDto.AccountKey, today);
            if (!rowsResult.IsSuccess)
                return rowsResult.Error;

            var rows = rowsResult.Value;

            var importResult = StatementImport.Create(userId.Value, statementImportDto.FileName, statementImportDto.Source, rows, _timeProvider.GetUtcNow());
            if (!importResult.IsSuccess)
                return importResult.Error;

            var statementImport = importResult.Value;

            await SuggestCategoriesAsync(rows, userId, today, cancellationToken);
            await MarkDuplicatesAsync(rows, statementImport, userId, cancellationToken);

            await _statementImportRepository.AddAsync(statementImport, rows, cancellationToken);

            return ToResponseDto(statementImport);
        }

        public async Task<Result<PagedResponseDto<StatementImportResponseDto>>> GetPagedAsync(StatementImportListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var imports = await _statementImportRepository.GetPagedByUserIdAsync(userId, pageRequestResult.Value, cancellationToken);

            return PagedResponseDto<StatementImportResponseDto>.From(imports, ToResponseDto);
        }

        public async Task<Result<StatementImportDetailsResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var statementImport = await _statementImportRepository.GetByIdAsync(new StatementImportId(id), userId, cancellationToken);
            if (statementImport is null)
                return StatementImportErrors.NotFound;

            IReadOnlyList<StatementImportMonth> months = statementImport.IsDraft
                ? await _statementImportRepository.GetMonthsAsync(statementImport.Id!, userId, cancellationToken)
                : [];

            return new StatementImportDetailsResponseDto(
                statementImport.Id!.Value,
                statementImport.FileName,
                statementImport.Source,
                statementImport.Status,
                statementImport.RowCount,
                statementImport.FirstDate,
                statementImport.LastDate,
                statementImport.CreatedAt,
                statementImport.CommittedAt,
                statementImport.ImportedCount,
                statementImport.CreatedMonthCount,
                months.Sum(month => month.IncludedCount),
                months.Sum(month => month.UncategorizedCount),
                months.Sum(month => month.SuggestedCount),
                months.Sum(month => month.DuplicateCount),
                [.. months.Select(ToMonthDto)]);
        }

        public async Task<Result> DeleteAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var deleted = await _statementImportRepository.DeleteAsync(new StatementImportId(id), userId, cancellationToken);
            if (!deleted)
                return StatementImportErrors.NotFound;

            return Result.Success();
        }

        /// <summary>
        /// Deletes the drafts nobody confirmed within <see cref="StatementImport.DraftRetentionDays"/> days (run by the
        /// cleanup job), so abandoned statements don't keep their lines forever.
        /// </summary>
        /// <returns>How many drafts were deleted.</returns>
        public async Task<int> DeleteStaleDraftsAsync(CancellationToken cancellationToken)
        {
            var cutoff = _timeProvider.GetUtcNow().AddDays(-StatementImport.DraftRetentionDays);

            return await _statementImportRepository.DeleteDraftsCreatedBeforeAsync(cutoff, cancellationToken);
        }

        /// <summary>
        /// Posts the included rows as transactions, opening the months the user doesn't have (of any year), and teaches
        /// the suggestions what the user decided. Every included row needs a category first.
        /// </summary>
        public async Task<Result<StatementImportCommitResultDto>> CommitAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var statementImport = await _statementImportRepository.GetByIdAsync(new StatementImportId(id), userId, cancellationToken);
            if (statementImport is null)
                return StatementImportErrors.NotFound;

            var draftResult = statementImport.EnsureDraft();
            if (!draftResult.IsSuccess)
                return draftResult.Error;

            var rows = await _statementImportRepository.GetAllRowsAsync(statementImport.Id!, cancellationToken);
            var included = rows.Where(row => row.Included).ToList();

            if (included.Count == 0)
                return StatementImportErrors.NothingToImport;

            if (included.Any(row => row.CategoryId is null))
                return StatementImportErrors.UncategorizedRows;

            foreach (var categoryId in included.Select(row => row.CategoryId!).Distinct())
            {
                if (await _categoryRepository.GetByIdAsync(categoryId, userId, cancellationToken) is null)
                    return CategoryErrors.NotFound;
            }

            var months = new Dictionary<YearMonth, (MonthlySummary MonthlySummary, bool Created)>();
            foreach (var month in included.Select(row => row.Month).Distinct().OrderBy(month => month.Ordinal))
            {
                var existing = await _monthlySummaryRepository.GetByPeriodAsync(userId, month, cancellationToken);
                months[month] = existing is not null
                    ? (existing, false)
                    : (await _monthlySummaryRepository.AddIfMissingAsync(MonthlySummary.OpenForImport(userId, month), cancellationToken), true);
            }

            var transactions = new List<Transaction>(included.Count);
            foreach (var row in included)
            {
                var transactionResult = Transaction.CreateFromImport(row, months[row.Month].MonthlySummary);
                if (!transactionResult.IsSuccess)
                    return transactionResult.Error;

                transactions.Add(transactionResult.Value);
            }

            var createdMonthCount = months.Values.Count(month => month.Created);

            var outcome = await _statementImportRepository.CommitAsync(
                statementImport,
                transactions,
                RuleLearning.From(rows),
                createdMonthCount,
                _timeProvider.GetUtcNow(),
                _appClock.Today(),
                cancellationToken);
            if (outcome is null)
                return StatementImportErrors.AlreadyCommitted;

            return new StatementImportCommitResultDto(
                outcome.ImportedCount,
                outcome.SkippedCount,
                createdMonthCount,
                [.. months.Select(month => new StatementImportCommittedMonthDto(month.Value.MonthlySummary.Id!.Value, month.Key.ToString(), month.Value.Created))]);
        }

        /// <summary>One row per line; a line repeated in the file (same bank line id) is kept once.</summary>
        private static Result<List<StatementImportRow>> ReadRows(IReadOnlyList<StatementLineDto> lines, string? accountKey, DateOnly today)
        {
            var rows = new List<StatementImportRow>(lines.Count);
            var externalIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in lines)
            {
                var rowResult = StatementImportRow.Create(new StatementLine(line.Date, line.Description, line.Amount, line.Fitid), accountKey, today);
                if (!rowResult.IsSuccess)
                    return rowResult.Error;

                var row = rowResult.Value;
                if (row.ExternalId is not null && !externalIds.Add(row.ExternalId))
                    continue;

                rows.Add(row);
            }

            return rows;
        }

        private async Task SuggestCategoriesAsync(IReadOnlyList<StatementImportRow> rows, UserId userId, DateOnly today, CancellationToken cancellationToken)
        {
            var rules = await _categorizationRuleRepository.GetByUserIdAsync(userId, cancellationToken);
            var history = await _transactionRepository.GetCategoryHistoryAsync(userId, cancellationToken);

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            var suggester = new CategorySuggester(rules.Select(rule => rule.ToEvidence()).Concat(CategoryEvidence.FromHistory(history)), today);
            var ownTransfers = OwnTransferDetector.For(user?.Name.Value);
            var monthShifts = new MonthShiftAdvisor(rules, today);

            foreach (var row in rows)
            {
                ImportRowFiler.File(row, suggester.Suggest(row.MerchantKey, row.Type == MoneyFlowType.Income), ownTransfers);

                // After filing: the final type (an income may have become an investment) is part of what was learned.
                if (monthShifts.ShouldShift(row))
                    row.ApplyLearnedShift();
            }
        }

        private async Task MarkDuplicatesAsync(IReadOnlyList<StatementImportRow> rows, StatementImport statementImport, UserId userId, CancellationToken cancellationToken)
        {
            var externalIds = rows.Where(row => row.ExternalId is not null).Select(row => row.ExternalId!).ToList();

            var existing = await _transactionRepository.GetForDuplicateCheckAsync(
                userId,
                statementImport.FirstDate.AddDays(-DuplicateDetector.NearbyDays),
                statementImport.LastDate.AddDays(DuplicateDetector.NearbyDays),
                externalIds,
                cancellationToken);

            DuplicateDetector.MarkDuplicates(rows, existing);
        }

        private static StatementImportResponseDto ToResponseDto(StatementImport statementImport)
            => new(
                statementImport.Id!.Value,
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

        private static StatementImportMonthDto ToMonthDto(StatementImportMonth month)
            => new(
                month.Month.ToString(),
                month.MonthlySummaryId,
                month.RowCount,
                month.IncludedCount,
                month.UncategorizedCount,
                month.SuggestedCount,
                month.DuplicateCount,
                month.TotalIncome,
                month.TotalExpense,
                month.TotalInvestment);
    }
}
