using LifeManager.Application.Habits.Services;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.WebApi.Habits.Jobs
{
    /// <summary>
    /// One run of the habits' day close: judges every closed day of every user. It only triggers
    /// <see cref="HabitEvaluationService"/>: the schedule is each habit's cursor in the database, so days missed while
    /// nothing ran are judged on the next run and a run never judges a day twice. Triggered every hour by
    /// <see cref="HabitsDayCloseJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run Job).
    /// </summary>
    public sealed class HabitsDayCloseRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<HabitsDayCloseRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<HabitsDayCloseRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<UserId> userIds;

            try
            {
                // The runner is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var evaluationService = scope.ServiceProvider.GetRequiredService<HabitEvaluationService>();

                userIds = await evaluationService.GetUserIdsToEvaluateAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not read the users with habits to close; retrying on the next run");
                return;
            }

            foreach (var userId in userIds)
                await EvaluateAsync(userId, stoppingToken);
        }

        /// <summary>One scope per user: a failure is logged and the others still close; the next run retries it.</summary>
        private async Task EvaluateAsync(UserId userId, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var evaluationService = scope.ServiceProvider.GetRequiredService<HabitEvaluationService>();

                var summary = await evaluationService.EvaluateUserAsync(userId, stoppingToken);

                if (summary.DaysEvaluated > 0)
                    _logger.LogInformation(
                        "Closed {Days} habit day(s) for user {UserId} ({Knockouts} knockout(s))", summary.DaysEvaluated, userId.Value, summary.Knockouts);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Failed to close the habit days of user {UserId}", userId.Value);
            }
        }
    }
}
