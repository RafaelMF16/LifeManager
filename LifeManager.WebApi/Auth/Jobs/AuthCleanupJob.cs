namespace LifeManager.WebApi.Auth.Jobs
{
    /// <summary>
    /// Runs <see cref="AuthCleanupRunner"/> once at startup and then every <see cref="Interval"/>. Only
    /// registered while <c>backgroundJobs:enabled</c> isn't <c>false</c>, like the other jobs: on Cloud Run the
    /// <c>--run-jobs</c> mode runs it instead.
    /// </summary>
    public sealed class AuthCleanupJob(
        AuthCleanupRunner runner,
        TimeProvider timeProvider) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly AuthCleanupRunner _runner = runner;
        private readonly TimeProvider _timeProvider = timeProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval, _timeProvider);

            try
            {
                do
                {
                    await _runner.RunOnceAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The host is shutting down.
            }
        }
    }
}
