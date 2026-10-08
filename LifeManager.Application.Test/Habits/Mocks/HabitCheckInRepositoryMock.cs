using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>Mirrors HabitCheckInRepository's transaction over the singletons: profile, check-ins, ledger and streak.</summary>
    public class HabitCheckInRepositoryMock : IHabitCheckInRepository
    {
        private readonly HabitCheckInSingleton _checkIns = HabitCheckInSingleton.Instance;
        private readonly HabitSingleton _habits = HabitSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<IReadOnlyList<HabitCheckIn>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            IReadOnlyList<HabitCheckIn> checkIns =
            [
                .. _checkIns.Where(checkIn => checkIn.UserId == userId && checkIn.Date >= from && checkIn.Date <= to)
            ];

            return Task.FromResult(checkIns);
        }

        public Task<HabitCheckInEffects?> CheckInAsync(
            Habit habit,
            DateOnly date,
            DateTimeOffset createdAt,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(habit.UserId);

            if (_checkIns.Any(checkIn => checkIn.HabitId == habit.Id && checkIn.Date == date))
                return Task.FromResult<HabitCheckInEffects?>(null);

            var successDates = SuccessDates(habit.Id!);
            successDates.Add(date);

            var profile = Copy(_profiles[profileIndex]);
            var effects = decide(new HabitCheckInContext(profile, successDates, Removed: null));

            var checkIn = HabitCheckIn.Done(habit, date, createdAt, effects.Applied, effects.FreezeAwarded);
            checkIn.AssignId(_checkIns.Count == 0 ? 1 : _checkIns.Max(stored => stored.Id!.Value) + 1);
            _checkIns.Add(checkIn);

            Save(habit, profileIndex, profile, effects);

            return Task.FromResult<HabitCheckInEffects?>(effects);
        }

        public Task<HabitCheckInEffects?> UndoCheckInAsync(
            Habit habit,
            DateOnly date,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(habit.UserId);

            var checkIn = _checkIns.SingleOrDefault(stored => stored.HabitId == habit.Id && stored.Date == date && stored.Status == HabitCheckInStatus.Done);
            if (checkIn is null)
                return Task.FromResult<HabitCheckInEffects?>(null);

            _checkIns.Remove(checkIn);

            var profile = Copy(_profiles[profileIndex]);
            var effects = decide(new HabitCheckInContext(profile, SuccessDates(habit.Id!), checkIn));

            Save(habit, profileIndex, profile, effects);

            return Task.FromResult<HabitCheckInEffects?>(effects);
        }

        /// <summary>Like PlayerWallet.LockAsync: creates the default profile if missing.</summary>
        private int LockProfile(UserId userId)
        {
            var index = _profiles.FindIndex(profile => profile.UserId == userId);
            if (index >= 0)
                return index;

            var newProfile = PlayerProfile.CreateDefault(userId);
            newProfile.AssignId(_profiles.Count + 1);
            _profiles.Add(newProfile);

            return _profiles.Count - 1;
        }

        private HashSet<DateOnly> SuccessDates(HabitId habitId)
            => [.. _checkIns.Where(checkIn => checkIn.HabitId == habitId && checkIn.IsSuccess).Select(checkIn => checkIn.Date)];

        private void Save(Habit habit, int profileIndex, PlayerProfile profile, HabitCheckInEffects effects)
        {
            foreach (var entry in effects.Entries)
            {
                entry.AssignId(_ledger.Count + 1);
                _ledger.Add(entry);
            }

            _profiles[profileIndex] = Copy(profile);

            habit.SetStreak(effects.CurrentStreak);
            var habitIndex = _habits.FindIndex(stored => stored.Id == habit.Id);
            if (habitIndex >= 0)
                _habits[habitIndex] = HabitRepositoryMock.ToDetachedCopy(habit);
        }

        private static PlayerProfile Copy(PlayerProfile profile)
            => PlayerProfile.FromPersistence(
                profile.Id!.Value,
                profile.UserId.Value,
                profile.TotalXp,
                profile.Hp,
                profile.MaxHp,
                profile.Coins,
                profile.StreakFreezes);
    }
}
