using LifeManager.Application.Ranking.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Ranking.Services
{
    /// <summary>
    /// The ranking points of players in a week (<see cref="WeeklyScore"/>), read from their stored check-ins, challenges
    /// and amounts.
    /// </summary>
    public class WeeklyScoreService(IWeeklyScoreRepository weeklyScoreRepository, AppClock appClock)
    {
        private readonly IWeeklyScoreRepository _weeklyScoreRepository = weeklyScoreRepository;
        private readonly AppClock _appClock = appClock;

        /// <summary>The player's own week (the current one by default), up to today.</summary>
        public async Task<Result<WeeklyScoreDto>> GetMineAsync(UserId userId, DateOnly? weekStart, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var start = weekStart ?? GameWeek.Start(today);
            if (!GameWeek.IsWeekStart(start))
                return RankingErrors.InvalidWeekStart;

            var scores = await GetForUsersAsync([userId], start, today, cancellationToken);

            return WeeklyScoreDto.From(scores[userId]);
        }

        /// <summary>One result per user (zeros for a user without habits), scoring the days up to <paramref name="upTo"/>.</summary>
        public async Task<IReadOnlyDictionary<UserId, WeeklyScoreResult>> GetForUsersAsync(
            IReadOnlyCollection<UserId> userIds,
            DateOnly weekStart,
            DateOnly upTo,
            CancellationToken cancellationToken)
        {
            if (upTo < weekStart)
                return userIds.Distinct().ToDictionary(userId => userId, _ => WeeklyScoreResult.Empty(weekStart));

            var inputs = await _weeklyScoreRepository.GetInputsAsync(userIds, weekStart, GameWeek.End(weekStart), cancellationToken);
            var habitsByUser = inputs.Habits.ToLookup(habit => habit.UserId);
            var checkInsByUser = inputs.CheckIns.ToLookup(checkIn => checkIn.UserId);
            var challengesByUser = inputs.Challenges.ToLookup(challenge => challenge.UserId);
            var progressByUser = inputs.Progress.ToLookup(entry => entry.UserId);

            return userIds.Distinct().ToDictionary(
                userId => userId,
                userId => WeeklyScore.Calculate(
                    [.. habitsByUser[userId].Select(habit => new ScoredHabit(
                        habit.HabitId, habit.Kind, habit.Frequency, habit.StartDate,
                        habit.ArchivedAt is { } archivedAt ? _appClock.DateOf(archivedAt) : null))],
                    [.. checkInsByUser[userId].Select(checkIn => new ScoredCheckIn(checkIn.HabitId, checkIn.Date, checkIn.Status))],
                    weekStart,
                    upTo,
                    [.. challengesByUser[userId].Select(challenge => new ScoredChallenge(
                        challenge.HabitId, challenge.Type, challenge.Target, challenge.Effort, challenge.Status))],
                    [.. progressByUser[userId].Select(entry => new ScoredProgress(entry.HabitId, entry.Date, entry.Value))]));
        }
    }
}
