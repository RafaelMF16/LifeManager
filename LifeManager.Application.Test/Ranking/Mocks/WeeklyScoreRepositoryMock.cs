using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Ranking.Mocks
{
    /// <summary>Mirrors WeeklyScoreRepository over the habit singletons, with the same filters.</summary>
    public class WeeklyScoreRepositoryMock : IWeeklyScoreRepository
    {
        public Task<RankingScoreInputs> GetInputsAsync(IReadOnlyCollection<UserId> userIds, DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken)
        {
            var archivedSince = new DateTimeOffset(weekStart.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            var habits = HabitSingleton.Instance
                .Where(habit => userIds.Contains(habit.UserId)
                    && habit.StartDate <= weekEnd
                    && (habit.ArchivedAt is null || habit.ArchivedAt >= archivedSince))
                .Select(habit => new RankingHabitRow(habit.UserId, habit.Id!.Value, habit.Kind, habit.Frequency, habit.StartDate, habit.ArchivedAt))
                .ToList();

            var checkIns = HabitCheckInSingleton.Instance
                .Where(checkIn => userIds.Contains(checkIn.UserId) && checkIn.Date >= weekStart && checkIn.Date <= weekEnd)
                .Select(checkIn => new RankingCheckInRow(checkIn.UserId, checkIn.HabitId.Value, checkIn.Date, checkIn.Status))
                .ToList();

            return Task.FromResult(new RankingScoreInputs(habits, checkIns));
        }
    }
}
