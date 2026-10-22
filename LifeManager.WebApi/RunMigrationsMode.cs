using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.WebApi
{
    /// <summary>
    /// <c>dotnet LifeManager.WebApi.dll --migrate</c>: applies the pending EF Core migrations and exits, without starting the
    /// web server. It's the entry point of the Cloud Run Job that <c>deploy/deploy-api.ps1</c> runs before updating the
    /// service, so a failed migration (or a model change with no migration) throws, exits non-zero and stops the deploy with
    /// the previous revision still serving. That revision keeps serving on the new schema for a few seconds, so migrations
    /// must stay additive.
    /// </summary>
    public static class RunMigrationsMode
    {
        public const string Argument = "--migrate";

        public static async Task RunAsync(WebApplication app)
        {
            await using (app)
            {
                var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RunMigrationsMode).FullName!);

                await using var scope = app.Services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<LifeManagerDbContext>().Database;

                var pendingMigrations = (await database.GetPendingMigrationsAsync()).ToList();

                if (pendingMigrations.Count == 0)
                    logger.LogInformation("No pending migrations");
                else
                    logger.LogInformation("Applying {Count} migration(s): {Migrations}", pendingMigrations.Count, string.Join(", ", pendingMigrations));

                // Called even with nothing pending: it still fails on a model change that has no migration.
                await database.MigrateAsync();

                logger.LogInformation("Migrations finished");
            }
        }
    }
}
