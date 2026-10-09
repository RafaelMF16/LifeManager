namespace LifeManager.WebApi.Habits.Jobs
{
    /// <summary>
    /// Runs <see cref="HabitsDayCloseRunner"/> once at startup and then every <see cref="Interval"/>. Only registered while
    /// <c>backgroundJobs:enabled</c> isn't <c>false</c>: on Cloud Run the instance scales to zero, so the same runner is
    /// triggered by Cloud Scheduler through a Cloud Run Job (<c>--run-jobs</c>) instead.
    /// </summary>
    public sealed class HabitsDayCloseJob(
        HabitsDayCloseRunner runner,
        TimeProvider timeProvider) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly HabitsDayCloseRunner _runner = runner;
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
