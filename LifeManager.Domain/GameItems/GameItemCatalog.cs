using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.GameItems
{
    /// <summary>What a purchase changed: the applied outcome and the HP the potion actually healed (0 for a freeze).</summary>
    public record GameItemPurchaseOutcome(GameOutcome Outcome, int HpRestored);

    /// <summary>
    /// The shop's fixed game items and their rules (numbers in <see cref="GameRules"/>). Pure: the caller passes the
    /// profile, locked, and how many non-undone purchases of the item fall in its period. The same checks decide both
    /// what the shop shows (<see cref="Availability"/>) and whether a purchase goes through.
    /// </summary>
    public static class GameItemCatalog
    {
        public static readonly IReadOnlyList<GameItemKind> All = [GameItemKind.HpPotion, GameItemKind.StreakFreeze];

        /// <summary>Parses a catalog name (any case); numbers and unknown names are refused.</summary>
        public static bool TryParse(string? value, out GameItemKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]))
                return false;

            return Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);
        }

        public static int Price(GameItemKind kind)
            => kind switch
            {
                GameItemKind.HpPotion => GameRules.HpPotionPrice,
                GameItemKind.StreakFreeze => GameRules.StreakFreezePrice,
                _ => throw Unknown(kind)
            };

        public static int Limit(GameItemKind kind)
            => kind switch
            {
                GameItemKind.HpPotion => GameRules.MaxHpPotionsPerDay,
                GameItemKind.StreakFreeze => GameRules.MaxStreakFreezePurchasesPerWeek,
                _ => throw Unknown(kind)
            };

        public static GameItemLimitPeriod LimitPeriod(GameItemKind kind)
            => kind switch
            {
                GameItemKind.HpPotion => GameItemLimitPeriod.Day,
                GameItemKind.StreakFreeze => GameItemLimitPeriod.Week,
                _ => throw Unknown(kind)
            };

        /// <summary>HP the item heals at most; null for items that don't heal.</summary>
        public static int? HpRestore(GameItemKind kind)
            => kind == GameItemKind.HpPotion ? GameRules.HpPotionRestore : null;

        /// <summary>The first day of the item's limit period containing <paramref name="today"/>.</summary>
        public static DateOnly PeriodStart(GameItemKind kind, DateOnly today)
            => LimitPeriod(kind) == GameItemLimitPeriod.Week ? StreakCalculator.WeekStart(today) : today;

        /// <summary>Checks in order: the period's limit, then whether the item would do anything, then the coins.</summary>
        public static GameItemAvailability Availability(GameItemKind kind, PlayerProfile profile, int purchasedInPeriod)
        {
            if (purchasedInPeriod >= Limit(kind))
                return GameItemAvailability.LimitReached;

            if (kind == GameItemKind.HpPotion && profile.Hp >= profile.MaxHp)
                return GameItemAvailability.HpFull;

            if (kind == GameItemKind.StreakFreeze && profile.StreakFreezes >= GameRules.MaxStreakFreezes)
                return GameItemAvailability.StreakFreezesFull;

            if (profile.Coins < Price(kind))
                return GameItemAvailability.InsufficientCoins;

            return GameItemAvailability.Available;
        }

        public static Result EnsureCanPurchase(GameItemKind kind, PlayerProfile profile, int purchasedInPeriod)
            => Availability(kind, profile, purchasedInPeriod) switch
            {
                GameItemAvailability.Available => Result.Success(),
                GameItemAvailability.LimitReached => LimitPeriod(kind) == GameItemLimitPeriod.Week
                    ? GameItemErrors.WeeklyLimitReached
                    : GameItemErrors.DailyLimitReached,
                GameItemAvailability.HpFull => GameItemErrors.HpFull,
                GameItemAvailability.StreakFreezesFull => GameItemErrors.StreakFreezesFull,
                _ => GameItemErrors.InsufficientCoins
            };

        /// <summary>Charges the price and applies the item's effect to <paramref name="profile"/>, or changes nothing.</summary>
        public static Result<GameItemPurchaseOutcome> Purchase(GameItemKind kind, PlayerProfile profile, int purchasedInPeriod)
        {
            var canPurchase = EnsureCanPurchase(kind, profile, purchasedInPeriod);
            if (!canPurchase.IsSuccess)
                return canPurchase.Error;

            var hpRestored = kind == GameItemKind.HpPotion ? Math.Min(GameRules.HpPotionRestore, profile.MaxHp - profile.Hp) : 0;

            if (kind == GameItemKind.StreakFreeze)
            {
                var added = profile.AddStreakFreeze();
                if (!added.IsSuccess)
                    return added.Error;
            }

            var outcome = profile.Apply(new GameDelta(-Price(kind), 0, hpRestored));
            return new GameItemPurchaseOutcome(outcome, hpRestored);
        }

        /// <summary>
        /// Refunds <paramref name="costPaid"/> and takes the effect back: a potion's heal down to 1 HP at most (an undo
        /// never knocks out), or a held freeze (<see cref="GameItemErrors.ItemAlreadyUsed"/> when there is none left).
        /// </summary>
        public static Result<GameOutcome> Undo(GameItemKind kind, int costPaid, int hpRestored, PlayerProfile profile)
        {
            if (kind == GameItemKind.StreakFreeze)
            {
                var removed = profile.RemoveStreakFreeze();
                if (!removed.IsSuccess)
                    return removed.Error;
            }

            var hpTakenBack = Math.Min(hpRestored, Math.Max(profile.Hp - 1, 0));
            return profile.Apply(new GameDelta(costPaid, 0, -hpTakenBack));
        }

        private static ArgumentOutOfRangeException Unknown(GameItemKind kind)
            => new(nameof(kind), kind, "Unknown game item");
    }
}
