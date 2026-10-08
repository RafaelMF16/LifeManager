using LifeManager.Application.Habits.Services;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.WebApi.Habits.Jobs
{
    /// <summary>
    /// Closes the habits' days once at startup and then every <see cref="Interval"/>. It only triggers
    /// <see cref="HabitEvaluationService"/>: the schedule is each habit's cursor in the database, so days missed while the
    /// API was down are judged on the next run and a run never judges a day twice.
    /// </summary>
    public sealed class HabitsDayCloseJob(
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider,
        ILogger<HabitsDayCloseJob> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly ILogger<HabitsDayCloseJob> _logger = logger;

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
            IReadOnlyList<UserId> userIds;

            try
            {
                // The job is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
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
