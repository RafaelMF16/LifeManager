using LifeManager.Application.RecurringTransactions.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.Errors;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.RecurringTransactions.Services
{
    /// <remarks>
    /// Saving a recurrence (create, update, resume) also posts right away whatever is already due, so an occurrence
    /// whose day has passed this month shows up without waiting for the background job.
    /// </remarks>
    public class RecurringTransactionService(
        IRecurringTransactionRepository recurringTransactionRepository,
        ICategoryRepository categoryRepository,
        RecurringTransactionPostingService postingService,
        AppClock appClock)
    {
        private readonly IRecurringTransactionRepository _recurringTransactionRepository = recurringTransactionRepository;
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly RecurringTransactionPostingService _postingService = postingService;
        private readonly AppClock _appClock = appClock;

        public async Task<Result<RecurringTransactionResponseDto>> CreateAsync(RecurringTransactionDto recurringTransactionDto, UserId userId, CancellationToken cancellationToken)
        {
            var recurringTransactionResult = RecurringTransaction.Create(
                userId.Value,
                recurringTransactionDto.Type,
                recurringTransactionDto.CategoryId,
                recurringTransactionDto.Amount,
                recurringTransactionDto.Description,
                recurringTransactionDto.DayOfMonth,
                recurringTransactionDto.StartMonth,
                recurringTransactionDto.EndMonth,
                _appClock.Today());
            if (!recurringTransactionResult.IsSuccess)
                return recurringTransactionResult.Error;

            var recurringTransaction = recurringTransactionResult.Value;

            var category = await _categoryRepository.GetByIdAsync(recurringTransaction.CategoryId, userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            await _recurringTransactionRepository.AddAsync(recurringTransaction, cancellationToken);

            return await PostDueAndReloadAsync(recurringTransaction.Id!, userId, cancellationToken);
        }

        public async Task<Result<RecurringTransactionResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var item = await _recurringTransactionRepository.GetByIdAsync(new RecurringTransactionId(id), userId, cancellationToken);
            if (item is null)
                return RecurringTransactionErrors.NotFound;

            return ToResponseDto(item);
        }

        public async Task<Result<PagedResponseDto<RecurringTransactionResponseDto>>> GetPagedAsync(RecurringTransactionListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;
            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > TransactionDescription.MaxLength)
                return new PagedResponseDto<RecurringTransactionResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var recurringTransactions = await _recurringTransactionRepository.GetPagedByUserIdAsync(
                userId, pageRequest, query.Type, query.Status, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<RecurringTransactionResponseDto>.From(recurringTransactions, ToResponseDto);
        }

        public async Task<Result<RecurringTransactionResponseDto>> UpdateAsync(int id, RecurringTransactionDto recurringTransactionDto, UserId userId, CancellationToken cancellationToken)
        {
            var item = await _recurringTransactionRepository.GetByIdAsync(new RecurringTransactionId(id), userId, cancellationToken);
            if (item is null)
                return RecurringTransactionErrors.NotFound;

            var recurringTransaction = item.RecurringTransaction;
            var expectedNextMonth = recurringTransaction.NextMonth;

            var updateResult = recurringTransaction.Update(
                recurringTransactionDto.Type,
                recurringTransactionDto.CategoryId,
                recurringTransactionDto.Amount,
                recurringTransactionDto.Description,
                recurringTransactionDto.DayOfMonth,
                recurringTransactionDto.StartMonth,
                recurringTransactionDto.EndMonth,
                _appClock.Today());
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            var category = await _categoryRepository.GetByIdAsync(recurringTransaction.CategoryId, userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            if (!await _recurringTransactionRepository.TryUpdateAsync(recurringTransaction, expectedNextMonth, cancellationToken))
                return RecurringTransactionErrors.ChangedConcurrently;

            return await PostDueAndReloadAsync(recurringTransaction.Id!, userId, cancellationToken);
        }

        public async Task<Result<RecurringTransactionResponseDto>> PauseAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var item = await _recurringTransactionRepository.GetByIdAsync(new RecurringTransactionId(id), userId, cancellationToken);
            if (item is null)
                return RecurringTransactionErrors.NotFound;

            var recurringTransaction = item.RecurringTransaction;
            var expectedNextMonth = recurringTransaction.NextMonth;

            var pauseResult = recurringTransaction.Pause();
            if (!pauseResult.IsSuccess)
                return pauseResult.Error;

            if (!await _recurringTransactionRepository.TryUpdateAsync(recurringTransaction, expectedNextMonth, cancellationToken))
                return RecurringTransactionErrors.ChangedConcurrently;

            return ToResponseDto(item);
        }

        public async Task<Result<RecurringTransactionResponseDto>> ResumeAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var item = await _recurringTransactionRepository.GetByIdAsync(new RecurringTransactionId(id), userId, cancellationToken);
            if (item is null)
                return RecurringTransactionErrors.NotFound;

            var recurringTransaction = item.RecurringTransaction;
            var expectedNextMonth = recurringTransaction.NextMonth;

            var resumeResult = recurringTransaction.Resume(_appClock.Today());
            if (!resumeResult.IsSuccess)
                return resumeResult.Error;

            if (!await _recurringTransactionRepository.TryUpdateAsync(recurringTransaction, expectedNextMonth, cancellationToken))
                return RecurringTransactionErrors.ChangedConcurrently;

            return await PostDueAndReloadAsync(recurringTransaction.Id!, userId, cancellationToken);
        }

        /// <summary>Transactions it already posted are kept, as ordinary transactions.</summary>
        public async Task<Result> DeleteAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var deleted = await _recurringTransactionRepository.DeleteAsync(new RecurringTransactionId(id), userId, cancellationToken);
            if (!deleted)
                return RecurringTransactionErrors.NotFound;

            return Result.Success();
        }

        private async Task<Result<RecurringTransactionResponseDto>> PostDueAndReloadAsync(RecurringTransactionId id, UserId userId, CancellationToken cancellationToken)
        {
            // The recurrence is already saved: if posting fails here, the background job tries again on its next run.
            await _postingService.PostDueOccurrencesAsync(id, cancellationToken);

            var item = await _recurringTransactionRepository.GetByIdAsync(id, userId, cancellationToken);
            if (item is null)
                return RecurringTransactionErrors.NotFound;

            return ToResponseDto(item);
        }

        private static RecurringTransactionResponseDto ToResponseDto(RecurringTransactionListItem item)
        {
            var recurringTransaction = item.RecurringTransaction;

            return new RecurringTransactionResponseDto(
                recurringTransaction.Id!.Value,
                recurringTransaction.Type,
                recurringTransaction.CategoryId.Value,
                item.CategoryName,
                recurringTransaction.Amount.Value,
                recurringTransaction.Description.Value,
                recurringTransaction.DayOfMonth.Value,
                recurringTransaction.StartMonth.ToString(),
                recurringTransaction.EndMonth?.ToString(),
                recurringTransaction.Status,
                recurringTransaction.NextOccurrenceDate);
        }
    }
}
