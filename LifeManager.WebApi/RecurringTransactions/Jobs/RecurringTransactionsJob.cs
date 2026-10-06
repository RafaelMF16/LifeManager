using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.Domain.RecurringTransactions.ValueObjects;

namespace LifeManager.WebApi.RecurringTransactions.Jobs
{
    /// <summary>
    /// Posts due recurring transactions once at startup and then every <see cref="Interval"/>. It only triggers
    /// <see cref="RecurringTransactionPostingService"/>: the schedule is each recurrence's cursor in the database, so
    /// occurrences missed while the API was down are caught up on the next run and a run never posts twice.
    /// </summary>
    public sealed class RecurringTransactionsJob(
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider,
        ILogger<RecurringTransactionsJob> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly ILogger<RecurringTransactionsJob> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval, _timeProvider);

            try
            {
                do
                {
                    await RunOnceAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The host is shutting down.
            }
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<RecurringTransactionId> dueIds;

            try
            {
                // The job is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
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
