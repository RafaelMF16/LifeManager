using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>
    /// Mirrors HabitProgressRepository's transaction over the singletons: the day is read before the rules, nothing is
    /// written when they refuse, and a completed or undone check-in is saved like the check-in repository does.
    /// </summary>
    public class HabitProgressRepositoryMock : IHabitProgressRepository
    {
        private readonly HabitProgressEntrySingleton _entries = HabitProgressEntrySingleton.Instance;
        private readonly HabitCheckInSingleton _checkIns = HabitCheckInSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly HabitCheckInRepositoryMock _checkInRepository = new();

        public Task<IReadOnlyList<HabitProgressEntry>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            IReadOnlyList<HabitProgressEntry> entries = Ordered(_entries.Where(entry => entry.UserId == userId && entry.Date >= from && entry.Date <= to));

            return Task.FromResult(entries);
        }

        public Task<IReadOnlyDictionary<DateOnly, int>> GetDailyTotalsAsync(HabitId habitId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<DateOnly, int> totals = _entries
                .Where(entry => entry.HabitId == habitId && entry.Date >= from && entry.Date <= to)
                .GroupBy(entry => entry.Date)
                .ToDictionary(day => day.Key, day => day.Sum(entry => entry.Value));

            return Task.FromResult(totals);
        }

        public Task<Result<HabitProgressSaved>> AddAsync(
            Habit habit,
            HabitProgressEntry entry,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = _checkInRepository.LockProfile(habit.UserId);
            var (dayEntries, dayCheckIn) = ReadDay(habit, entry.Date);

            var profile = HabitCheckInRepositoryMock.Copy(_profiles[profileIndex]);
            var decision = decide(new HabitProgressContext(
                profile, entry, dayEntries, dayCheckIn, _checkInRepository.SuccessDates(habit.Id!), _checkInRepository.FailedDates(habit.Id!)));
            if (!decision.IsSuccess)
                return Task.FromResult<Result<HabitProgressSaved>>(decision.Error);

            entry.AssignId(_entries.Count == 0 ? 1 : _entries.Max(stored => stored.Id!.Value) + 1);
            _entries.Add(entry);
            Save(habit, entry, profileIndex, profile, dayCheckIn, decision.Value);

            return Task.FromResult<Result<HabitProgressSaved>>(new HabitProgressSaved(decision.Value, [.. dayEntries, entry]));
        }

        public Task<Result<HabitProgressSaved>> RemoveAsync(
            Habit habit,
            HabitProgressEntryId entryId,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = _checkInRepository.LockProfile(habit.UserId);

            var entry = _entries.SingleOrDefault(stored => stored.Id == entryId && stored.HabitId == habit.Id);
            if (entry is null)
                return Task.FromResult<Result<HabitProgressSaved>>(HabitErrors.ProgressEntryNotFound);

            var (dayEntries, dayCheckIn) = ReadDay(habit, entry.Date);

            var profile = HabitCheckInRepositoryMock.Copy(_profiles[profileIndex]);
            var decision = decide(new HabitProgressContext(
                profile, entry, dayEntries, dayCheckIn, _checkInRepository.SuccessDates(habit.Id!), _checkInRepository.FailedDates(habit.Id!)));
            if (!decision.IsSuccess)
                return Task.FromResult<Result<HabitProgressSaved>>(decision.Error);

            _entries.Remove(entry);
            Save(habit, entry, profileIndex, profile, dayCheckIn, decision.Value);

            return Task.FromResult<Result<HabitProgressSaved>>(
                new HabitProgressSaved(decision.Value, [.. dayEntries.Where(dayEntry => dayEntry.Id != entry.Id)]));
        }

        private (IReadOnlyList<HabitProgressEntry> Entries, HabitCheckIn? CheckIn) ReadDay(Habit habit, DateOnly date)
            => (Ordered(_entries.Where(entry => entry.HabitId == habit.Id && entry.Date == date)),
                _checkIns.SingleOrDefault(checkIn => checkIn.HabitId == habit.Id && checkIn.Date == date));

        private void Save(Habit habit, HabitProgressEntry entry, int profileIndex, PlayerProfile profile, HabitCheckIn? dayCheckIn, HabitProgressEffects effects)
        {
            if (effects.CheckIn is null)
                return;

            if (effects.Decision == HabitProgressDecision.Complete)
            {
                var checkIn = HabitCheckIn.Done(habit, entry.Date, entry.CreatedAt, effects.CheckIn.Applied, effects.CheckIn.FreezeAwarded);
                checkIn.AssignId(_checkIns.Count == 0 ? 1 : _checkIns.Max(stored => stored.Id!.Value) + 1);
                _checkIns.Add(checkIn);
            }
            else if (dayCheckIn is not null)
            {
                _checkIns.Remove(dayCheckIn);
            }

            _checkInRepository.Save(habit, profileIndex, profile, effects.CheckIn);
        }

        private static List<HabitProgressEntry> Ordered(IEnumerable<HabitProgressEntry> entries)
            => [.. entries.OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id!.Value)];
    }
}
