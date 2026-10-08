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
        int MaxStreakFreezes)
    {
        public static PlayerProfileResponseDto From(PlayerProfile profile)
            => new(
                profile.Level,
                profile.XpInLevel,
                profile.XpToNextLevel,
                profile.TotalXp,
                profile.Hp,
                profile.MaxHp,
                profile.Coins,
                profile.StreakFreezes,
                GameRules.MaxStreakFreezes);
    }
}
