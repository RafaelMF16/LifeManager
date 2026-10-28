using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    public class HabitProgressRepository(LifeManagerDbContext dbContext) : IHabitProgressRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<HabitProgressEntry>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            return await _dbContext.HabitProgressEntries
                .AsNoTracking()
                .Where(entry => entry.UserId == userId && entry.Date >= from && entry.Date <= to)
                .OrderBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<DateOnly, int>> GetDailyTotalsAsync(HabitId habitId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            return await _dbContext.HabitProgressEntries
                .Where(entry => entry.HabitId == habitId && entry.Date >= from && entry.Date <= to)
                .GroupBy(entry => entry.Date)
                .Select(day => new { Date = day.Key, Total = day.Sum(entry => entry.Value) })
                .ToDictionaryAsync(day => day.Date, day => day.Total, cancellationToken);
        }

        public async Task<Result<HabitProgressSaved>> AddAsync(
            Habit habit,
            HabitProgressEntry entry,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The same lock as check-ins: the day's total and check-in below can't race another change of the user.
            var profile = await PlayerWallet.LockAsync(_dbContext, habit.UserId, cancellationToken);
            var day = await ReadDayAsync(habit, entry.Date, cancellationToken);

            var decision = decide(new HabitProgressContext(profile, entry, day.Entries, day.CheckIn, day.SuccessDates, day.FailedDates));
            if (!decision.IsSuccess)
                return decision.Error;

            _dbContext.HabitProgressEntries.Add(entry);
            await SaveAsync(habit, entry, profile, day.CheckIn, decision.Value, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return new HabitProgressSaved(decision.Value, [.. day.Entries, entry]);
        }

        public async Task<Result<HabitProgressSaved>> RemoveAsync(
            Habit habit,
            HabitProgressEntryId entryId,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var profile = await PlayerWallet.LockAsync(_dbContext, habit.UserId, cancellationToken);

            var entry = await _dbContext.HabitProgressEntries
                .SingleOrDefaultAsync(storedEntry => storedEntry.Id == entryId && storedEntry.HabitId == habit.Id, cancellationToken);
            if (entry is null)
                return HabitErrors.ProgressEntryNotFound;

            var day = await ReadDayAsync(habit, entry.Date, cancellationToken);

            var decision = decide(new HabitProgressContext(profile, entry, day.Entries, day.CheckIn, day.SuccessDates, day.FailedDates));
            if (!decision.IsSuccess)
                return decision.Error;

            _dbContext.HabitProgressEntries.Remove(entry);
            await SaveAsync(habit, entry, profile, day.CheckIn, decision.Value, cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return new HabitProgressSaved(decision.Value, [.. day.Entries.Where(dayEntry => dayEntry.Id != entry.Id)]);
        }

        private record Day(
            IReadOnlyList<HabitProgressEntry> Entries,
            HabitCheckIn? CheckIn,
            HashSet<DateOnly> SuccessDates,
            HashSet<DateOnly> FailedDates);

        /// <summary>The day's entries (read-only, oldest first), its check-in (tracked, so an undo can delete it) and the habit's history.</summary>
        private async Task<Day> ReadDayAsync(Habit habit, DateOnly date, CancellationToken cancellationToken)
        {
            var entries = await _dbContext.HabitProgressEntries
                .AsNoTracking()
                .Where(entry => entry.HabitId == habit.Id && entry.Date == date)
                .OrderBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            var checkIn = await _dbContext.HabitCheckIns
                .SingleOrDefaultAsync(storedCheckIn => storedCheckIn.HabitId == habit.Id && storedCheckIn.Date == date, cancellationToken);

            var (successDates, failedDates) = await HabitCheckInRepository.GetHistoryAsync(_dbContext, habit.Id!, cancellationToken);

            return new Day(entries, checkIn, successDates, failedDates);
        }

        /// <summary>
        /// Writes the pending entry change with what the decision did to the check-in: a new done check-in, the stored one
        /// removed, or nothing, plus the ledger, profile and streak when the check-in changed.
        /// </summary>
        private async Task SaveAsync(
            Habit habit,
            HabitProgressEntry entry,
            PlayerProfile profile,
            HabitCheckIn? dayCheckIn,
            HabitProgressEffects effects,
            CancellationToken cancellationToken)
        {
            if (effects.CheckIn is null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            if (effects.Decision == HabitProgressDecision.Complete)
                _dbContext.HabitCheckIns.Add(HabitCheckIn.Done(habit, entry.Date, entry.CreatedAt, effects.CheckIn.Applied, effects.CheckIn.FreezeAwarded));
            else if (dayCheckIn is not null)
                _dbContext.HabitCheckIns.Remove(dayCheckIn);

            await HabitCheckInRepository.SaveAsync(_dbContext, habit, profile, effects.CheckIn, cancellationToken);
        }
    }
}
