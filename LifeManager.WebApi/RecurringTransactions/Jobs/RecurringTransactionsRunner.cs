using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.Domain.RecurringTransactions.ValueObjects;

namespace LifeManager.WebApi.RecurringTransactions.Jobs
{
    /// <summary>
    /// One run of the recurring transactions posting: posts every due recurrence. It only triggers
    /// <see cref="RecurringTransactionPostingService"/>: the schedule is each recurrence's cursor in the database, so
    /// occurrences missed while nothing ran are caught up on the next run and a run never posts twice. Triggered every hour
    /// by <see cref="RecurringTransactionsJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run Job).
    /// </summary>
    public sealed class RecurringTransactionsRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringTransactionsRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<RecurringTransactionsRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<RecurringTransactionId> dueIds;

            try
            {
                // The runner is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var postingService = scope.ServiceProvider.GetRequiredService<RecurringTransactionPostingService>();

                dueIds = await postingService.GetDueIdsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not read the due recurring transactions; retrying on the next run");
                return;
            }

            foreach (var id in dueIds)
                await PostAsync(id, stoppingToken);
        }

        /// <summary>One scope per recurrence: a failure is logged and the others still post; the next run retries it.</summary>
        private async Task PostAsync(RecurringTransactionId id, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var postingService = scope.ServiceProvider.GetRequiredService<RecurringTransactionPostingService>();

                var result = await postingService.PostDueOccurrencesAsync(id, stoppingToken);

                if (!result.IsSuccess)
                    _logger.LogWarning("Recurring transaction {RecurringTransactionId} was not posted: {ErrorCode}", id.Value, result.Error.Code);
                else if (result.Value > 0)
                    _logger.LogInformation("Recurring transaction {RecurringTransactionId} posted {Count} occurrence(s)", id.Value, result.Value);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Failed to post recurring transaction {RecurringTransactionId}", id.Value);
            }
        }
    }
}
