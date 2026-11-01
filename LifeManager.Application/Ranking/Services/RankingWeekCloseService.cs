using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Application.Ranking.Services
{
    /// <summary>What one group's close did, for the runner's log.</summary>
    /// <param name="Forced">Weeks closed past the grace period with a member's day close still behind.</param>
    /// <param name="Waiting">The week left open because a member's day close hasn't judged its Sunday yet.</param>
    public record RankingWeekCloseOutcome(IReadOnlyList<DateOnly> Closed, IReadOnlyList<DateOnly> Forced, DateOnly? Waiting);

    /// <summary>
    /// Stores the final result of a group's settled weeks (<see cref="RankingWeekResult"/>), oldest first. The cursor is
    /// the group's latest stored week, so a missed run loses nothing and two runs at once store a week once.
    /// </summary>
    public class RankingWeekCloseService(
        IRankingWeekResultRepository rankingWeekResultRepository,
        IRankingGroupRepository rankingGroupRepository,
        WeeklyScoreService weeklyScoreService,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IRankingWeekResultRepository _rankingWeekResultRepository = rankingWeekResultRepository;
        private readonly IRankingGroupRepository _rankingGroupRepository = rankingGroupRepository;
        private readonly WeeklyScoreService _weeklyScoreService = weeklyScoreService;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public Task<IReadOnlyList<RankingGroupId>> GetGroupsToCloseAsync(CancellationToken cancellationToken)
            => _rankingWeekResultRepository.GetGroupsToCloseAsync(GameWeek.LastSettledStart(_appClock.Today()), cancellationToken);

        public async Task<RankingWeekCloseOutcome> CloseDueWeeksAsync(RankingGroupId groupId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var lastSettled = GameWeek.LastSettledStart(today);
            List<DateOnly> closed = [];
            List<DateOnly> forced = [];

            var group = await _rankingGroupRepository.GetViewAsync(groupId, cancellationToken);
            if (group is null)
                return new RankingWeekCloseOutcome(closed, forced, null);

            var latest = await _rankingWeekResultRepository.GetLatestClosedWeekAsync(groupId, cancellationToken);
            var weekStart = latest?.AddDays(GameWeek.Days) ?? GameWeek.Start(group.Group.CreatedOn);

            for (; weekStart <= lastSettled; weekStart = weekStart.AddDays(GameWeek.Days))
            {
                var sunday = GameWeek.End(weekStart);

                // The members at the close, but only those already in by the week's end.
                var members = group.Members.Where(member => member.JoinedOn <= sunday).ToList();
                if (members.Count == 0)
                    continue;

                var userIds = members.Select(member => member.UserId).ToList();
                if (!await _rankingWeekResultRepository.AreDaysClosedAsync(userIds, sunday, cancellationToken))
                {
                    if (!RankingWeekResult.IsPastGrace(weekStart, today))
                        return new RankingWeekCloseOutcome(closed, forced, weekStart);

                    forced.Add(weekStart);
                }

                var scores = await _weeklyScoreService.GetForUsersAsync(userIds, weekStart, sunday, cancellationToken);
                var standings = GroupStandings.Rank(scores.Select(score => (score.Key, score.Value)));
                var results = RankingWeekResult.ForWeek(
                    groupId, weekStart, standings, members.ToDictionary(member => member.UserId, member => member.JoinedOn), _timeProvider.GetUtcNow());

                // Another run stored it first: it goes on from there.
                if (!await _rankingWeekResultRepository.AddWeekAsync(results, cancellationToken))
                {
                    forced.Remove(weekStart);
                    break;
                }

                closed.Add(weekStart);
            }

            return new RankingWeekCloseOutcome(closed, forced, null);
        }
    }
}
