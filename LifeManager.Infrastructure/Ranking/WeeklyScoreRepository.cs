using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Ranking
{
    public class WeeklyScoreRepository(LifeManagerDbContext dbContext) : IWeeklyScoreRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<RankingScoreInputs> GetInputsAsync(IReadOnlyCollection<UserId> userIds, DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken)
        {
            var ids = userIds.Distinct().ToList();
            // A habit archived before the week can't be due in it; a day of slack covers the business time zone.
            var archivedSince = new DateTimeOffset(weekStart.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            var habits = await _dbContext.Habits
                .AsNoTracking()
                .Where(habit => ids.Contains(habit.UserId)
                    && habit.StartDate <= weekEnd
                    && (habit.ArchivedAt == null || habit.ArchivedAt >= archivedSince))
                .Select(habit => new { habit.UserId, habit.Id, habit.Kind, habit.FrequencyType, habit.WeekDays, habit.TimesPerWeek, habit.StartDate, habit.ArchivedAt })
                .ToListAsync(cancellationToken);

            var checkIns = await _dbContext.HabitCheckIns
                .AsNoTracking()
                .Where(checkIn => ids.Contains(checkIn.UserId) && checkIn.Date >= weekStart && checkIn.Date <= weekEnd)
                .Select(checkIn => new { checkIn.UserId, checkIn.HabitId, checkIn.Date, checkIn.Status })
                .ToListAsync(cancellationToken);

            var challenges = await _dbContext.WeeklyChallenges
                .AsNoTracking()
                .Where(challenge => ids.Contains(challenge.UserId) && challenge.WeekStart == weekStart)
                .Select(challenge => new RankingChallengeRow(
                    challenge.UserId, challenge.HabitId.Value, challenge.Type, challenge.Target, challenge.Effort, challenge.Status))
                .ToListAsync(cancellationToken);

            var volumeHabitIds = challenges
                .Where(challenge => challenge.Type == ChallengeType.Volume)
                .Select(challenge => new HabitId(challenge.HabitId))
                .Distinct()
                .ToList();
            var progress = volumeHabitIds.Count == 0
                ? []
                : await _dbContext.HabitProgressEntries
                    .AsNoTracking()
                    .Where(entry => volumeHabitIds.Contains(entry.HabitId) && entry.Date >= weekStart && entry.Date <= weekEnd)
                    .Select(entry => new RankingProgressRow(entry.UserId, entry.HabitId.Value, entry.Date, entry.Value))
                    .ToListAsync(cancellationToken);

            return new RankingScoreInputs(
                [.. habits.Select(habit => new RankingHabitRow(
                    habit.UserId, habit.Id!.Value, habit.Kind,
                    HabitFrequency.FromPersistence(habit.FrequencyType, habit.WeekDays, habit.TimesPerWeek),
                    habit.StartDate, habit.ArchivedAt))],
                [.. checkIns.Select(checkIn => new RankingCheckInRow(checkIn.UserId, checkIn.HabitId.Value, checkIn.Date, checkIn.Status))],
                challenges,
                progress);
        }
    }
}
