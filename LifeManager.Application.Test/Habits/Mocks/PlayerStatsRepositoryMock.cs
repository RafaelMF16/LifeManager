using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Habits.Mocks
{
    /// <summary>Mirrors PlayerStatsRepository over the singletons: the same counts as its SELECT.</summary>
    public class PlayerStatsRepositoryMock : IPlayerStatsRepository
    {
        public Task<PlayerStats> GetStatsAsync(UserId userId, CancellationToken cancellationToken)
        {
            var checkIns = HabitCheckInSingleton.Instance.Where(checkIn => checkIn.UserId == userId).ToList();
            var kept = checkIns.Where(checkIn => checkIn.Status is HabitCheckInStatus.Done or HabitCheckInStatus.Clean).ToList();
            var habits = HabitSingleton.Instance.Where(habit => habit.UserId == userId).ToList();
            var ledger = GameLedgerEntrySingleton.Instance.Where(entry => entry.UserId == userId).ToList();

            var coinsEarned = ledger
                .Where(entry => GameLedgerEntry.HabitEarningKinds.Contains(entry.Kind)
                    || entry.Kind == GameLedgerEntryKind.AchievementUnlocked
                    || (entry.Kind == GameLedgerEntryKind.Undo && entry.HabitId is not null))
                .Sum(entry => entry.CoinsDelta);
            var coinsSpent = ledger
                .Where(entry => entry.Kind is GameLedgerEntryKind.RewardRedeemed or GameLedgerEntryKind.ItemPurchased
                    || (entry.Kind == GameLedgerEntryKind.Undo && (entry.RewardId is not null || entry.ItemKind is not null)))
                .Sum(entry => -entry.CoinsDelta);

            return Task.FromResult(new PlayerStats(
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done),
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Clean),
                habits.Select(habit => habit.FrequencyType == HabitFrequencyType.TimesPerWeek
                    ? habit.LongestStreak * HabitRewards.DaysPerStreakWeek
                    : habit.LongestStreak).DefaultIfEmpty(0).Max(),
                kept.GroupBy(checkIn => checkIn.HabitId).Select(days => days.Count()).DefaultIfEmpty(0).Max(),
                kept.Select(checkIn => checkIn.Date).Distinct().Count(),
                habits.Count,
                RewardRedemptionSingleton.Instance.Count(redemption => redemption.UserId == userId && redemption.UndoneAt is null),
                Math.Max(coinsEarned, 0),
                Math.Max(coinsSpent, 0),
                ledger.Count(entry => entry.Kind == GameLedgerEntryKind.Knockout),
                AchievementUnlockSingleton.Instance.Count(unlock => unlock.UserId == userId),
                ledger.Count == 0 ? null : ledger.Min(entry => entry.OccurredOn)));
        }
    }
}
