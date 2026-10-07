using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// The player's character in the habits game: one per user. Only changes through <see cref="Apply"/> and the
    /// streak freeze methods, always on a row locked by the caller, so the totals never race.
    /// </summary>
    public class PlayerProfile
    {
        public PlayerProfileId? Id { get; private set; }
        public UserId UserId { get; }

        /// <summary>Persisted copy of <see cref="GameRules.LevelForTotalXp"/>; always derived from <see cref="TotalXp"/>.</summary>
        public int Level { get; private set; }
        public int TotalXp { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Coins { get; private set; }
        public int StreakFreezes { get; private set; }

        /// <summary>XP earned since the current level started.</summary>
        public int XpInLevel => TotalXp - GameRules.TotalXpForLevel(Level);

        /// <summary>XP the current level needs in total to reach the next one.</summary>
        public int XpToNextLevel => GameRules.XpToNextLevel(Level);

        private PlayerProfile(UserId userId, int totalXp, int hp, int maxHp, int coins, int streakFreezes)
        {
            UserId = userId;
            TotalXp = totalXp;
            Level = GameRules.LevelForTotalXp(totalXp);
            Hp = hp;
            MaxHp = maxHp;
            Coins = coins;
            StreakFreezes = streakFreezes;
        }

        /// <summary>A new player: level 1, full HP, no coins.</summary>
        public static PlayerProfile CreateDefault(UserId userId)
            => new(userId, totalXp: 0, hp: GameRules.MaxHp, maxHp: GameRules.MaxHp, coins: 0, streakFreezes: 0);

        /// <summary>Rehydrates a stored profile without going through the game, so tests can seed any state.</summary>
        internal static PlayerProfile FromPersistence(int id, int idUser, int totalXp, int hp, int maxHp, int coins, int streakFreezes)
        {
            var profile = new PlayerProfile(new UserId(idUser), totalXp, hp, maxHp, coins, streakFreezes);
            profile.AssignId(id);

            return profile;
        }

        /// <summary>
        /// Applies <paramref name="delta"/>, then its consequences:
        /// <list type="number">
        /// <item>XP never goes below 0, and the level follows it;</item>
        /// <item>coins never go below 0;</item>
        /// <item>HP stays between 0 and <see cref="MaxHp"/>;</item>
        /// <item>leveling up fills the HP;</item>
        /// <item>HP at 0 is a knockout: the player loses <see cref="GameRules.KnockoutCoinLossRate"/> of the coins and
        /// gets full HP back, keeping level and XP.</item>
        /// </list>
        /// </summary>
        public GameOutcome Apply(GameDelta delta)
        {
            var previousLevel = Level;

            var totalXp = Math.Max(TotalXp + delta.Xp, 0);
            var coins = Math.Max(Coins + delta.Coins, 0);
            var hp = Math.Clamp(Hp + delta.Hp, 0, MaxHp);
            var applied = new GameDelta(coins - Coins, totalXp - TotalXp, hp - Hp);

            TotalXp = totalXp;
            Level = GameRules.LevelForTotalXp(totalXp);
            Coins = coins;
            Hp = hp;

            var levelsGained = Math.Max(Level - previousLevel, 0);
            var levelUpHpRestored = 0;
            if (levelsGained > 0)
            {
                levelUpHpRestored = MaxHp - Hp;
                Hp = MaxHp;
            }

            if (Hp > 0)
                return new GameOutcome(applied, levelsGained, levelUpHpRestored, KnockedOut: false, KnockoutCoinsLost: 0, KnockoutHpRestored: 0);

            var coinsLost = GameRules.KnockoutCoinLoss(Coins);
            Coins -= coinsLost;
            Hp = MaxHp;

            return new GameOutcome(applied, levelsGained, levelUpHpRestored, KnockedOut: true, KnockoutCoinsLost: coinsLost, KnockoutHpRestored: MaxHp);
        }

        public Result AddStreakFreeze()
        {
            if (StreakFreezes >= GameRules.MaxStreakFreezes)
                return PlayerProfileErrors.StreakFreezesFull;

            StreakFreezes++;
            return Result.Success();
        }

        public Result UseStreakFreeze()
        {
            if (StreakFreezes <= 0)
                return PlayerProfileErrors.NoStreakFreeze;

            StreakFreezes--;
            return Result.Success();
        }

        public void AssignId(int id)
        {
            Id = new PlayerProfileId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not PlayerProfile other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
