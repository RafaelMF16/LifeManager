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
        int MaxStreakFreezes);
}
