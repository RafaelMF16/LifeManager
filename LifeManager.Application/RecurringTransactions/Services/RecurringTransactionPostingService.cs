using LifeManager.Application.Shared.Time;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;

namespace LifeManager.Application.RecurringTransactions.Services
{
    /// <summary>
    /// Turns due occurrences into transactions. The schedule lives in the data (each recurrence's cursor), so whatever
    /// triggers this (the background job, or a request right after a recurrence is saved) only has to call it: occurrences
    /// missed while nothing ran are caught up, and running it twice never posts twice.
    /// </summary>
    public class RecurringTransactionPostingService(
        IRecurringTransactionRepository recurringTransactionRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        AppClock appClock)
    {
        /// <summary>How many due recurrences one run picks up; the rest wait for the next run.</summary>
        public const int DueBatchSize = 500;

        private readonly IRecurringTransactionRepository _recurringTransactionRepository = recurringTransactionRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly AppClock _appClock = appClock;

        public async Task<IReadOnlyList<RecurringTransactionId>> GetDueIdsAsync(CancellationToken cancellationToken)
            => await _recurringTransactionRepository.GetDueIdsAsync(_appClock.Today(), DueBatchSize, cancellationToken);

        /// <summary>
        /// Posts every occurrence of the recurrence that is due by today, one month at a time, opening the months the
        /// user doesn't have yet.
        /// </summary>
        /// <returns>How many transactions were posted. Stops quietly when someone else moved the recurrence meanwhile.</returns>
        public async Task<Result<int>> PostDueOccurrencesAsync(RecurringTransactionId recurringTransactionId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var posted = 0;

            var recurringTransaction = await _recurringTransactionRepository.GetForPostingAsync(recurringTransactionId, cancellationToken);
            if (recurringTransaction is null)
                return posted;

            while (recurringTransaction.IsDue(today))
            {
                var month = YearMonth.From(recurringTransaction.NextOccurrenceDate!.Value);

                var monthlySummary = await _monthlySummaryRepository.GetByPeriodAsync(recurringTransaction.UserId, month, cancellationToken)
                    ?? await _monthlySummaryRepository.AddIfMissingAsync(MonthlySummary.OpenForRecurringPosting(recurringTransaction.UserId, month), cancellationToken);

                var transactionResult = Transaction.CreateFromRecurrence(recurringTransaction, monthlySummary);
                if (!transactionResult.IsSuccess)
                    return transactionResult.Error;

                var expectedNextMonth = recurringTransaction.NextMonth;
                recurringTransaction.AdvanceAfterPosting();

                if (!await _recurringTransactionRepository.TryPostOccurrenceAsync(recurringTransaction, expectedNextMonth, transactionResult.Value, cancellationToken))
                    break;

                posted++;
            }

            return posted;
        }
    }
}
