using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits
{
    /// <summary>The player's totals at the end of a game day.</summary>
    public record PlayerEvolutionPoint(DateOnly Date, int TotalXp, int Coins);

    /// <summary>Running totals: the sum of the ledger up to some day.</summary>
    public record PlayerTotals(int TotalXp, int Coins);

    /// <summary>The sum of one game day's ledger entries.</summary>
    public record PlayerDayDelta(DateOnly Date, int Xp, int Coins);

    /// <summary>The ledger as <see cref="PlayerEvolution.Fill"/> needs it: the totals before a period and its days.</summary>
    public record PlayerLedgerHistory(PlayerTotals Before, IReadOnlyList<PlayerDayDelta> Days);

    /// <summary>
    /// The profile's evolution chart: XP and coins day by day, rebuilt from the ledger. The ledger always adds up to the
    /// profile, so the last point is the profile as it is now.
    /// </summary>
    public static class PlayerEvolution
    {
        public static int Days(EvolutionRange range) => range switch
        {
            EvolutionRange.Month => 30,
            EvolutionRange.Year => 365,
            _ => 90
        };

        /// <summary>The first day of <paramref name="range"/>, so that it ends on <paramref name="today"/>.</summary>
        public static DateOnly Start(EvolutionRange range, DateOnly today) => today.AddDays(1 - Days(range));

        /// <summary>
        /// One point per day from <paramref name="start"/> to <paramref name="end"/>, each the totals at the end of
        /// that day: <paramref name="before"/> (everything before the period) plus the deltas up to it. Days without
        /// entries repeat the day before; deltas outside the period are ignored.
        /// </summary>
        public static IReadOnlyList<PlayerEvolutionPoint> Fill(DateOnly start, DateOnly end, PlayerTotals before, IReadOnlyList<PlayerDayDelta> days)
        {
            var deltas = days
                .Where(day => day.Date >= start && day.Date <= end)
                .GroupBy(day => day.Date)
                .ToDictionary(group => group.Key, group => (Xp: group.Sum(day => day.Xp), Coins: group.Sum(day => day.Coins)));

            var points = new List<PlayerEvolutionPoint>();
            var (totalXp, coins) = (before.TotalXp, before.Coins);
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (deltas.TryGetValue(date, out var delta))
                {
                    totalXp += delta.Xp;
                    coins += delta.Coins;
                }

                points.Add(new PlayerEvolutionPoint(date, totalXp, coins));
            }

            return points;
        }
    }
}
