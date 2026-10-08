namespace LifeManager.Application.Rewards.DTOs
{
    /// <summary>
    /// <c>GET /api/Rewards/EarningPace</c>: coins earned with habits per day lately, to price rewards in days. 0 when the
    /// player earned nothing in the window.
    /// </summary>
    public record EarningPaceDto(decimal AverageDailyCoins, int WindowDays);
}
