using LifeManager.Domain.Habits;

namespace LifeManager.Application.Habits.DTOs
{
    public record PlayerProfileResponseDto(
        int Level,
        int XpInLevel,
        int XpToNextLevel,
        int TotalXp,
        int Hp,
        int MaxHp,
        int Coins,
        int StreakFreezes,
        int MaxStreakFreezes,
        LastKnockoutDto? LastKnockout = null)
    {
        /// <param name="lastKnockout">Only filled by <c>GET Profile</c>, so the frontend can tell the player about a knockout the day close caused.</param>
        public static PlayerProfileResponseDto From(PlayerProfile profile, LastKnockoutDto? lastKnockout = null)
            => new(
                profile.Level,
                profile.XpInLevel,
                profile.XpToNextLevel,
                profile.TotalXp,
                profile.Hp,
                profile.MaxHp,
                profile.Coins,
                profile.StreakFreezes,
                GameRules.MaxStreakFreezes,
                lastKnockout);
    }

    /// <summary>The player's latest knockout: its ledger entry id (growing, so the frontend can tell a new one) and the coins it took.</summary>
    public record LastKnockoutDto(int Id, DateOnly OccurredOn, int CoinsLost)
    {
        public static LastKnockoutDto? From(GameLedgerEntry? entry)
            => entry is null ? null : new(entry.Id!.Value, entry.OccurredOn, -entry.CoinsDelta);
    }
}
