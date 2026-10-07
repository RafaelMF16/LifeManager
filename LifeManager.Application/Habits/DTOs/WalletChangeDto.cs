namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>The profile after a change, plus what the frontend celebrates or warns about.</summary>
    public record WalletChangeDto(
        PlayerProfileResponseDto Profile,
        int CoinsDelta,
        int XpDelta,
        int HpDelta,
        int LevelsGained,
        bool KnockedOut,
        int KnockoutCoinsLost);
}
