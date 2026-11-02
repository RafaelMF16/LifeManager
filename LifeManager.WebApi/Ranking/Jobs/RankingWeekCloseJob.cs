namespace LifeManager.WebApi.Ranking.Jobs
{
    /// <summary>
    /// Runs <see cref="RankingWeekCloseRunner"/> once at startup and then every <see cref="Interval"/>. Only registered while
    /// <c>backgroundJobs:enabled</c> isn't <c>false</c>: on Cloud Run the instance scales to zero, so the same runner is
    /// triggered by Cloud Scheduler through a Cloud Run Job (<c>--run-jobs</c>) instead. It runs
    /// beside the day close's job here, so a week the day close hasn't judged yet just closes on a later tick.
    /// </summary>
    public sealed class RankingWeekCloseJob(
        RankingWeekCloseRunner runner,
        TimeProvider timeProvider) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly RankingWeekCloseRunner _runner = runner;
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
