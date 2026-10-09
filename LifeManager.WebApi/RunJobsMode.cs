using LifeManager.WebApi.Habits.Jobs;
using LifeManager.WebApi.RecurringTransactions.Jobs;

namespace LifeManager.WebApi
{
    /// <summary>
    /// <c>dotnet LifeManager.WebApi.dll --run-jobs</c>: runs every background job once and exits, without starting the web
    /// server. It's the entry point of the Cloud Run Job that Cloud Scheduler triggers every hour, since the API itself
    /// scales to zero there. Recurring transactions go first, so a posting never waits an extra hour behind the day close.
    /// </summary>
    public static class RunJobsMode
    {
        public const string Argument = "--run-jobs";

        public static async Task RunAsync(WebApplication app)
        {
            await using (app)
            {
                var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RunJobsMode).FullName!);

                logger.LogInformation("Running background jobs once");

                // No cancellation: every unit of work is its own transaction guarded by a cursor, so a task stopped by its
                // timeout loses nothing and the next run picks up where it stopped.
                await app.Services.GetRequiredService<RecurringTransactionsRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<HabitsDayCloseRunner>().RunOnceAsync(CancellationToken.None);

                logger.LogInformation("Background jobs finished");
            }
        }
    }
}
