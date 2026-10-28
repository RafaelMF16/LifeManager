using LifeManager.Application.Achievements.DTOs;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

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
        PlayerAvatar Avatar,
        AchievementCode? Title,
        LastKnockoutDto? LastKnockout,
        IReadOnlyList<NewAchievementDto> NewAchievements)
    {
        /// <param name="lastKnockout">Only filled by <c>GET Profile</c>, so the frontend can tell the player about a knockout the day close caused.</param>
        /// <param name="newAchievements">Only filled by <c>GET Profile</c>: the unlocks the player hasn't been shown; empty elsewhere.</param>
        public static PlayerProfileResponseDto From(PlayerProfile profile, LastKnockoutDto? lastKnockout = null, IReadOnlyList<NewAchievementDto>? newAchievements = null)
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
                profile.Avatar,
                profile.Title,
                lastKnockout,
                newAchievements ?? []);
    }

    /// <summary>The player's latest knockout: its ledger entry id (growing, so the frontend can tell a new one) and the coins it took.</summary>
    public record LastKnockoutDto(int Id, DateOnly OccurredOn, int CoinsLost)
    {
        public static LastKnockoutDto? From(GameLedgerEntry? entry)
            => entry is null ? null : new(entry.Id!.Value, entry.OccurredOn, -entry.CoinsDelta);
    }
}
