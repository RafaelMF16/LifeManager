using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class HabitCheckInRepository(LifeManagerDbContext dbContext) : IHabitCheckInRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<HabitCheckIn>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            return await _dbContext.HabitCheckIns
                .AsNoTracking()
                .Where(checkIn => checkIn.UserId == userId && checkIn.Date >= from && checkIn.Date <= to)
                .ToListAsync(cancellationToken);
        }

        public async Task<HabitCheckInEffects?> RecordAsync(
            Habit habit,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Every game write of the user takes this lock first, so the check below and the history read can't race
            // another write (the unique (HabitId, Date) index is the last guard).
            var profile = await PlayerWallet.LockAsync(_dbContext, habit.UserId, cancellationToken);

            if (await _dbContext.HabitCheckIns.AnyAsync(checkIn => checkIn.HabitId == habit.Id && checkIn.Date == date, cancellationToken))
                return null;

            var (successDates, failedDates) = await GetHistoryAsync(_dbContext, habit.Id!, cancellationToken);
            (HabitCheckIn.SuccessStatuses.Contains(status) ? successDates : failedDates).Add(date);

            var effects = decide(new HabitCheckInContext(profile, successDates, failedDates, Removed: null));

            _dbContext.HabitCheckIns.Add(HabitCheckIn.Judged(habit, date, status, createdAt, effects.Applied, effects.FreezeAwarded));
            await SaveAsync(habit, profile, effects, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return effects;
        }

        public async Task<HabitCheckInEffects?> RemoveAsync(
            Habit habit,
            DateOnly date,
            HabitCheckInStatus status,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, habit.UserId, cancellationToken);

            var checkIn = await _dbContext.HabitCheckIns
                .SingleOrDefaultAsync(storedCheckIn => storedCheckIn.HabitId == habit.Id
                    && storedCheckIn.Date == date
                    && storedCheckIn.Status == status, cancellationToken);
            if (checkIn is null)
                return null;

            _dbContext.HabitCheckIns.Remove(checkIn);

            var (successDates, failedDates) = await GetHistoryAsync(_dbContext, habit.Id!, cancellationToken);
            successDates.Remove(date);
            failedDates.Remove(date);

            var effects = decide(new HabitCheckInContext(profile, successDates, failedDates, checkIn));

            await SaveAsync(habit, profile, effects, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return effects;
        }

        /// <summary>
        /// The habit's days that count for the streak (<see cref="HabitCheckIn.SuccessStatuses"/>) and its relapses,
        /// which break it.
        /// </summary>
        internal static async Task<(HashSet<DateOnly> SuccessDates, HashSet<DateOnly> FailedDates)> GetHistoryAsync(
            LifeManagerDbContext dbContext,
            HabitId habitId,
            CancellationToken cancellationToken)
        {
            var successStatuses = HabitCheckIn.SuccessStatuses.ToArray();
            var days = await dbContext.HabitCheckIns
                .Where(checkIn => checkIn.HabitId == habitId
                    && (successStatuses.Contains(checkIn.Status) || checkIn.Status == HabitCheckInStatus.Relapse))
                .Select(checkIn => new { checkIn.Date, checkIn.Status })
                .ToListAsync(cancellationToken);

            return (
                [.. days.Where(day => day.Status != HabitCheckInStatus.Relapse).Select(day => day.Date)],
                [.. days.Where(day => day.Status == HabitCheckInStatus.Relapse).Select(day => day.Date)]);
        }

        /// <summary>Saves the pending check-in change with the ledger and profile, then the habit's streak.</summary>
        private async Task SaveAsync(Habit habit, PlayerProfile profile, HabitCheckInEffects effects, CancellationToken cancellationToken)
        {
            await PlayerWallet.SaveAsync(_dbContext, profile, effects.Entries, cancellationToken);

            habit.SetStreak(effects.CurrentStreak);
            await _dbContext.Habits
                .Where(storedHabit => storedHabit.Id == habit.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedHabit => storedHabit.CurrentStreak, habit.CurrentStreak)
                    .SetProperty(storedHabit => storedHabit.LongestStreak, habit.LongestStreak), cancellationToken);
        }
    }
}
