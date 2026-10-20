using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Application.StatementImports.DTOs;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.StatementImports.Services
{
    /// <summary>
    /// The review of a draft's rows. Every operation first loads the import with its user, so another user's rows are
    /// never reachable; changes are only allowed while it is a draft.
    /// </summary>
    public class StatementImportRowService(
        IStatementImportRepository statementImportRepository,
        ICategoryRepository categoryRepository,
        AppClock appClock)
    {
        /// <summary>How many selected rows one bulk change takes (a page of the listing at most).</summary>
        public const int MaxBulkRows = PageRequest.MaxPageSize;

        private readonly IStatementImportRepository _statementImportRepository = statementImportRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly AppClock _appClock = appClock;

        public async Task<Result<PagedResponseDto<StatementImportRowResponseDto>>> GetPagedAsync(int statementImportId, StatementImportRowListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;

            YearMonth? month = null;
            if (!string.IsNullOrWhiteSpace(query.Month))
            {
                if (!YearMonth.TryParse(query.Month, out var parsedMonth))
                    return StatementImportErrors.InvalidMonth;

                month = parsedMonth;
            }

            var statementImport = await _statementImportRepository.GetByIdAsync(new StatementImportId(statementImportId), userId, cancellationToken);
            if (statementImport is null)
                return StatementImportErrors.NotFound;

            var normalizedSearch = SearchText.Normalize(query.Search);
            if (normalizedSearch.Length > StatementImportRow.OriginalDescriptionMaxLength)
                return new PagedResponseDto<StatementImportRowResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var rows = await _statementImportRepository.GetRowsPagedAsync(
                statementImport.Id!, pageRequest, month, query.Filter, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<StatementImportRowResponseDto>.From(rows, ToResponseDto);
        }

        /// <summary>
        /// Saves the user's edit of one row. The result also says how many other rows of the same merchant are filed
        /// differently, so the user can be offered to file them the same way (<see cref="ApplyToSimilarAsync"/>).
        /// </summary>
        public async Task<Result<StatementImportRowUpdateResultDto>> UpdateAsync(int statementImportId, int rowId, StatementImportRowDto rowDto, UserId userId, CancellationToken cancellationToken)
        {
            var draftResult = await GetDraftAsync(statementImportId, userId, cancellationToken);
            if (!draftResult.IsSuccess)
                return draftResult.Error;

            var statementImport = draftResult.Value;

            var item = await _statementImportRepository.GetRowAsync(statementImport.Id!, new StatementImportRowId(rowId), cancellationToken);
            if (item is null)
                return StatementImportErrors.RowNotFound;

            var row = item.Row;

            var editResult = row.Edit(rowDto.Type, rowDto.CategoryId, rowDto.Amount, rowDto.Description, rowDto.Date, rowDto.Included, _appClock.Today(), rowDto.ShiftToNextMonth);
            if (!editResult.IsSuccess)
                return editResult.Error;

            string? categoryName = null;
            if (row.CategoryId is not null)
            {
                var category = await _categoryRepository.GetByIdAsync(row.CategoryId, userId, cancellationToken);
                if (category is null)
                    return CategoryErrors.NotFound;

                categoryName = category.Name.Value;
            }

            await _statementImportRepository.UpdateRowsAsync([row], cancellationToken);

            var similar = await _statementImportRepository.GetUneditedRowsByMerchantKeyAsync(statementImport.Id!, row.MerchantKey, cancellationToken);
            var similarCount = similar.Count(other => !other.Equals(row) && FiledDifferently(other, row));

            return new StatementImportRowUpdateResultDto(ToResponseDto(row, categoryName), similarCount);
        }

        public async Task<Result<StatementImportRowsChangedDto>> BulkUpdateAsync(int statementImportId, StatementImportBulkUpdateDto bulkDto, UserId userId, CancellationToken cancellationToken)
        {
            var rowIds = (bulkDto.RowIds ?? []).Distinct().ToList();
            if (rowIds.Count == 0 || (bulkDto.Type is null && bulkDto.CategoryId is null && bulkDto.Included is null && bulkDto.ShiftToNextMonth is null))
                return StatementImportErrors.NothingToChange;

            if (rowIds.Count > MaxBulkRows)
                return StatementImportErrors.TooManyRowsSelected;

            var draftResult = await GetDraftAsync(statementImportId, userId, cancellationToken);
            if (!draftResult.IsSuccess)
                return draftResult.Error;

            if (bulkDto.CategoryId is not null
                && await _categoryRepository.GetByIdAsync(new CategoryId(bulkDto.CategoryId.Value), userId, cancellationToken) is null)
                return CategoryErrors.NotFound;

            var rows = await _statementImportRepository.GetRowsAsync(
                draftResult.Value.Id!, [.. rowIds.Select(id => new StatementImportRowId(id))], cancellationToken);

            foreach (var row in rows)
            {
                var editResult = row.EditInBulk(bulkDto.Type, bulkDto.CategoryId, bulkDto.Included, bulkDto.ShiftToNextMonth);
                if (!editResult.IsSuccess)
                    return editResult.Error;
            }

            await _statementImportRepository.UpdateRowsAsync(rows, cancellationToken);

            return new StatementImportRowsChangedDto(rows.Count);
        }

        /// <summary>
        /// Files the rows of the same merchant like the given one: type, category, whether it is included (a
        /// duplicate stays out) and whether it counts in the next month. Rows the user edited directly keep their own decision.
        /// </summary>
        public async Task<Result<StatementImportRowsChangedDto>> ApplyToSimilarAsync(int statementImportId, int rowId, UserId userId, CancellationToken cancellationToken)
        {
            var draftResult = await GetDraftAsync(statementImportId, userId, cancellationToken);
            if (!draftResult.IsSuccess)
                return draftResult.Error;

            var statementImport = draftResult.Value;

            var item = await _statementImportRepository.GetRowAsync(statementImport.Id!, new StatementImportRowId(rowId), cancellationToken);
            if (item is null)
                return StatementImportErrors.RowNotFound;

            var source = item.Row;
            var similar = await _statementImportRepository.GetUneditedRowsByMerchantKeyAsync(statementImport.Id!, source.MerchantKey, cancellationToken);
            var changed = similar.Where(row => !row.Equals(source) && FiledDifferently(row, source)).ToList();

            foreach (var row in changed)
                row.CopyDecisionFrom(source);

            if (changed.Count > 0)
                await _statementImportRepository.UpdateRowsAsync(changed, cancellationToken);

            return new StatementImportRowsChangedDto(changed.Count);
        }

        private async Task<Result<StatementImport>> GetDraftAsync(int statementImportId, UserId userId, CancellationToken cancellationToken)
        {
            var statementImport = await _statementImportRepository.GetByIdAsync(new StatementImportId(statementImportId), userId, cancellationToken);
            if (statementImport is null)
                return StatementImportErrors.NotFound;

            var draftResult = statementImport.EnsureDraft();
            if (!draftResult.IsSuccess)
                return draftResult.Error;

            return statementImport;
        }

        /// <summary>Whether <see cref="StatementImportRow.CopyDecisionFrom"/> would change <paramref name="row"/>.</summary>
        private static bool FiledDifferently(StatementImportRow row, StatementImportRow source)
            => row.Type != source.Type
                || row.CategoryId != source.CategoryId
                || row.Included != (source.Included && !row.IsDuplicate)
                || row.ShiftToNextMonth != source.ShiftToNextMonth;

        private static StatementImportRowResponseDto ToResponseDto(StatementImportRowListItem item)
            => ToResponseDto(item.Row, item.CategoryName);

        private static StatementImportRowResponseDto ToResponseDto(StatementImportRow row, string? categoryName)
            => new(
                row.Id!.Value,
                row.Date,
                row.Description.Value,
                row.OriginalDescription,
                row.Amount.Value,
                row.Type,
                row.CategoryId?.Value,
                categoryName,
                row.Included,
                row.DuplicateOfTransactionId,
                row.DuplicateOfMonthlySummaryId,
                row.SuggestionSource,
                row.SuggestionConfidence,
                row.SuggestionUses,
                row.EditedByUser,
                row.EffectiveDate,
                row.ShiftToNextMonth,
                row.ShiftLearned);
    }
}
