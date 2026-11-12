using LifeManager.Application.Habits.Reminders;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.WebApi.Habits.Jobs
{
    /// <summary>
    /// One run of the habit reminders (<see cref="HabitReminderService"/>): deletes the old reminder logs, reads the users
    /// with a reminder at the current hour in one scope, then sends each user's in its own scope, a failure logged and
    /// skipped (that user's reminder is lost for the hour: best effort). Triggered every hour by
    /// <see cref="HabitRemindersJob"/> (local) or, first of all, by the <c>--run-jobs</c> mode (Cloud Run Job).
    /// </summary>
    public sealed class HabitRemindersRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<HabitRemindersRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<HabitRemindersRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            HabitReminderRun run;

            try
            {
                // The runner is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<HabitReminderService>();

                if (!service.IsEnabled)
                {
                    _logger.LogInformation("Habit reminders skipped: push notifications are not configured");
                    return;
                }

                await service.DeleteOldLogsAsync(stoppingToken);
                run = await service.GetDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not read the habit reminders due; the next run reads them again");
                return;
            }

            foreach (var userId in run.UserIds)
                await SendAsync(userId, run, stoppingToken);
        }

        private async Task SendAsync(UserId userId, HabitReminderRun run, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var outcome = await scope.ServiceProvider.GetRequiredService<HabitReminderService>()
                    .SendForUserAsync(userId, run.Date, run.Hour, stoppingToken);

                if (outcome.Habits > 0)
                    _logger.LogInformation(
                        "Reminded user {UserId} of {Habits} habit(s) at {Hour}h: {Sent} device(s) notified, {Removed} dead device(s) removed",
                        userId.Value, outcome.Habits, run.Hour, outcome.Sent, outcome.Removed);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Failed to send the habit reminders of user {UserId}", userId.Value);
            }
        }
    }
}
