using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class HabitEvaluationRepository(LifeManagerDbContext dbContext) : IHabitEvaluationRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<UserId>> GetUserIdsToEvaluateAsync(DateOnly lastClosedDay, int limit, CancellationToken cancellationToken)
        {
            return await _dbContext.Habits
                .AsNoTracking()
                .Where(habit => habit.ArchivedAt == null && habit.EvaluatedUntil < lastClosedDay)
                .Select(habit => habit.UserId)
                .Distinct()
                .OrderBy(userId => userId)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Habit>> GetHabitsToEvaluateAsync(UserId userId, DateOnly lastClosedDay, CancellationToken cancellationToken)
        {
            return await _dbContext.Habits
                .AsNoTracking()
                .Where(habit => habit.UserId == userId && habit.ArchivedAt == null && habit.EvaluatedUntil < lastClosedDay)
                .OrderBy(habit => habit.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<HabitEvaluationEffects?> EvaluateDayAsync(
            Habit habit,
            DateOnly date,
            Func<HabitEvaluationContext, HabitEvaluationEffects> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Same lock order as check-ins (profile, then habit row), so the two never deadlock.
            var profile = await PlayerWallet.LockAsync(_dbContext, habit.UserId, cancellationToken);

            // Compare-and-swap on the cursor: only the run that moves it onto this day judges the day.
            var previousDay = date.AddDays(-1);
            var moved = await _dbContext.Habits
                .Where(storedHabit => storedHabit.Id == habit.Id && storedHabit.EvaluatedUntil == previousDay && storedHabit.ArchivedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(storedHabit => storedHabit.EvaluatedUntil, date), cancellationToken);
            if (moved == 0)
                return null;

            var weekStart = StreakCalculator.WeekStart(date);
            var weekEnd = weekStart.AddDays(6);
            var weekCheckIns = await _dbContext.HabitCheckIns
                .AsNoTracking()
                .Where(checkIn => checkIn.HabitId == habit.Id && checkIn.Date >= weekStart && checkIn.Date <= weekEnd)
                .ToListAsync(cancellationToken);
            var successDates = await HabitCheckInRepository.GetSuccessDatesAsync(_dbContext, habit.Id!, cancellationToken);

            var effects = decide(new HabitEvaluationContext(profile, weekCheckIns, successDates));

            _dbContext.HabitCheckIns.AddRange(effects.NewCheckIns);
            await PlayerWallet.SaveAsync(_dbContext, profile, effects.Entries, cancellationToken);

            habit.MarkEvaluated(date);
            if (effects.CurrentStreak is not null)
            {
                habit.SetStreak(effects.CurrentStreak.Value);
                await _dbContext.Habits
                    .Where(storedHabit => storedHabit.Id == habit.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(storedHabit => storedHabit.CurrentStreak, habit.CurrentStreak)
                        .SetProperty(storedHabit => storedHabit.LongestStreak, habit.LongestStreak), cancellationToken);
            }

            await databaseTransaction.CommitAsync(cancellationToken);

            return effects;
        }
    }
}
