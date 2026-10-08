using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>Mirrors HabitEvaluationRepository over the singletons, including the compare-and-swap on the cursor.</summary>
    public class HabitEvaluationRepositoryMock : IHabitEvaluationRepository
    {
        private readonly HabitSingleton _habits = HabitSingleton.Instance;
        private readonly HabitCheckInSingleton _checkIns = HabitCheckInSingleton.Instance;
        private readonly PlayerProfileSingleton _profiles = PlayerProfileSingleton.Instance;
        private readonly GameLedgerEntrySingleton _ledger = GameLedgerEntrySingleton.Instance;

        public Task<IReadOnlyList<UserId>> GetUserIdsToEvaluateAsync(DateOnly lastClosedDay, int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<UserId> userIds =
            [
                .. _habits
                    .Where(habit => !habit.IsArchived && habit.EvaluatedUntil < lastClosedDay)
                    .Select(habit => habit.UserId)
                    .Distinct()
                    .OrderBy(userId => userId.Value)
                    .Take(limit)
            ];

            return Task.FromResult(userIds);
        }

        public Task<IReadOnlyList<Habit>> GetHabitsToEvaluateAsync(UserId userId, DateOnly lastClosedDay, CancellationToken cancellationToken)
        {
            IReadOnlyList<Habit> habits =
            [
                .. _habits
                    .Where(habit => habit.UserId == userId && !habit.IsArchived && habit.EvaluatedUntil < lastClosedDay)
                    .OrderBy(habit => habit.Id!.Value)
                    .Select(HabitRepositoryMock.ToDetachedCopy)
            ];

            return Task.FromResult(habits);
        }

        public Task<HabitEvaluationEffects?> EvaluateDayAsync(
            Habit habit,
            DateOnly date,
            Func<HabitEvaluationContext, HabitEvaluationEffects> decide,
            CancellationToken cancellationToken)
        {
            var profileIndex = LockProfile(habit.UserId);

            var habitIndex = _habits.FindIndex(stored => stored.Id == habit.Id);
            var stored = habitIndex >= 0 ? _habits[habitIndex] : null;
            if (stored is null || stored.IsArchived || stored.EvaluatedUntil != date.AddDays(-1))
                return Task.FromResult<HabitEvaluationEffects?>(null);

            var weekStart = StreakCalculator.WeekStart(date);
            var weekEnd = weekStart.AddDays(6);
            IReadOnlyList<HabitCheckIn> weekCheckIns =
                [.. _checkIns.Where(checkIn => checkIn.HabitId == habit.Id && checkIn.Date >= weekStart && checkIn.Date <= weekEnd)];
            var successDates = _checkIns.Where(checkIn => checkIn.HabitId == habit.Id && checkIn.IsSuccess).Select(checkIn => checkIn.Date).ToHashSet();
            var failedDates = _checkIns
                .Where(checkIn => checkIn.HabitId == habit.Id && checkIn.Status == HabitCheckInStatus.Relapse)
                .Select(checkIn => checkIn.Date)
                .ToHashSet();

            var profile = Copy(_profiles[profileIndex]);
            var effects = decide(new HabitEvaluationContext(profile, weekCheckIns, successDates, failedDates));

            foreach (var checkIn in effects.NewCheckIns)
            {
                if (_checkIns.Any(existing => existing.HabitId == checkIn.HabitId && existing.Date == checkIn.Date))
                    throw new InvalidOperationException($"Unique (HabitId, Date) violated on {checkIn.Date}");

                checkIn.AssignId(_checkIns.Count == 0 ? 1 : _checkIns.Max(existing => existing.Id!.Value) + 1);
                _checkIns.Add(checkIn);
            }

            foreach (var entry in effects.Entries)
            {
                entry.AssignId(_ledger.Count + 1);
                _ledger.Add(entry);
            }

            _profiles[profileIndex] = Copy(profile);

            habit.MarkEvaluated(date);
            if (effects.CurrentStreak is not null)
                habit.SetStreak(effects.CurrentStreak.Value);
            _habits[habitIndex] = HabitRepositoryMock.ToDetachedCopy(habit);

            return Task.FromResult<HabitEvaluationEffects?>(effects);
        }

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
