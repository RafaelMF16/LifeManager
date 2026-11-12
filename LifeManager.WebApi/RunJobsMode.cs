using LifeManager.WebApi.Auth.Jobs;
using LifeManager.WebApi.Habits.Jobs;
using LifeManager.WebApi.Ranking.Jobs;
using LifeManager.WebApi.RecurringTransactions.Jobs;
using LifeManager.WebApi.StatementImports.Jobs;
using LifeManager.WebApi.Users.Jobs;

namespace LifeManager.WebApi
{
    /// <summary>
    /// <c>dotnet LifeManager.WebApi.dll --run-jobs</c>: runs every background job once and exits, without starting the web
    /// server. It's the entry point of the Cloud Run Job that Cloud Scheduler triggers every hour, since the API itself
    /// scales to zero there. The habit reminders go first, to arrive as close to the hour as possible; then the recurring
    /// transactions, so a posting never waits an extra hour behind the day close;
    /// the ranking's week close comes right after the day close, whose cursors it waits for;
    /// the cleanups (abandoned import drafts, then the auth e-mail leftovers) and the account deletion go last.
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
                await app.Services.GetRequiredService<HabitRemindersRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<RecurringTransactionsRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<HabitsDayCloseRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<RankingWeekCloseRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<StatementImportsCleanupRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<AuthCleanupRunner>().RunOnceAsync(CancellationToken.None);
                await app.Services.GetRequiredService<AccountDeletionRunner>().RunOnceAsync(CancellationToken.None);

                logger.LogInformation("Background jobs finished");
            }
        }
    }
}
