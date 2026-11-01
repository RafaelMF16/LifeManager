using LifeManager.Application.Ranking.Services;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.WebApi.Ranking.Jobs
{
    /// <summary>
    /// One run of the ranking's week close: stores the result of every group's settled weeks. It only triggers
    /// <see cref="RankingWeekCloseService"/>: the schedule is each group's latest stored week, so a missed run loses
    /// nothing. Goes right after the habits' day close, whose cursors it waits for. Triggered every hour by
    /// <see cref="RankingWeekCloseJob"/> (local) or by the <c>--run-jobs</c> mode (Cloud Run Job).
    /// </summary>
    public sealed class RankingWeekCloseRunner(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RankingWeekCloseRunner> logger)
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<RankingWeekCloseRunner> _logger = logger;

        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<RankingGroupId> groupIds;

            try
            {
                // The runner is a singleton and the DbContext is scoped, so every unit of work gets its own scope.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var closeService = scope.ServiceProvider.GetRequiredService<RankingWeekCloseService>();

                groupIds = await closeService.GetGroupsToCloseAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not read the ranking groups with weeks to close; retrying on the next run");
                return;
            }

            foreach (var groupId in groupIds)
                await CloseAsync(groupId, stoppingToken);
        }

        /// <summary>One scope per group: a failure is logged and the others still close; the next run retries it.</summary>
        private async Task CloseAsync(RankingGroupId groupId, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var closeService = scope.ServiceProvider.GetRequiredService<RankingWeekCloseService>();

                var outcome = await closeService.CloseDueWeeksAsync(groupId, stoppingToken);

                if (outcome.Closed.Count > 0)
                    _logger.LogInformation(
                        "Closed {Weeks} ranking week(s) of group {GroupId}: {WeekStarts}", outcome.Closed.Count, groupId.Value, outcome.Closed);

                if (outcome.Forced.Count > 0)
                    _logger.LogWarning(
                        "Closed ranking week(s) {WeekStarts} of group {GroupId} past the grace period, with a member's day close still behind",
                        outcome.Forced, groupId.Value);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Failed to close the ranking weeks of group {GroupId}", groupId.Value);
            }
        }
    }
}
