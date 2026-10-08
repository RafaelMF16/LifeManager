namespace LifeManager.Domain.Rewards
{
    /// <summary>How fast the player earns coins with habits, to price rewards in days ("≈ 3 days of habits").</summary>
    public static class RewardPace
    {
        /// <summary>The recent days the average looks at, today included.</summary>
        public const int WindowDays = 14;

        public static DateOnly WindowStart(DateOnly today)
            => today.AddDays(-(WindowDays - 1));

        /// <summary>
        /// Coins earned per day over the window. A player who started playing inside it is averaged from their first
        /// earning day, so a new player's pace isn't diluted by days they weren't playing yet. 0 with no earnings.
        /// </summary>
        /// <param name="coinsEarned">Net coins earned with habits in the window (undone check-ins already subtracted).</param>
        /// <param name="firstEarningDay">The first day in the window with a habit earning; null when there is none.</param>
        public static decimal AverageDailyCoins(int coinsEarned, DateOnly? firstEarningDay, DateOnly today)
        {
            if (firstEarningDay is null || coinsEarned <= 0)
                return 0;

            var from = firstEarningDay.Value < WindowStart(today) ? WindowStart(today) : firstEarningDay.Value;
            var days = Math.Max(today.DayNumber - from.DayNumber + 1, 1);

            return Math.Round((decimal)coinsEarned / days, 1, MidpointRounding.AwayFromZero);
        }
    }
}
