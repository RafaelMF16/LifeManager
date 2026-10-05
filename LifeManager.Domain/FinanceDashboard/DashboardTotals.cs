namespace LifeManager.Domain.FinanceDashboard
{
    /// <summary>How much came in, was spent and was invested over a period, always as positive amounts.</summary>
    public record DashboardTotals(decimal Income, decimal Expense, decimal Investment)
    {
        /// <summary>What is left in the account: same rule as a month's balance.</summary>
        public decimal Balance => Income - Expense - Investment;

        /// <summary>
        /// Relative change from <paramref name="previous"/> to <paramref name="current"/> (0.12 = +12%), against the
        /// size of the previous value, so a smaller negative balance reads as an improvement. Null when there is nothing
        /// to compare with (previous is zero).
        /// </summary>
        public static decimal? ChangeRatio(decimal current, decimal previous)
            => previous == 0 ? null : Math.Round((current - previous) / Math.Abs(previous), 4);

        /// <summary>The part <paramref name="amount"/> takes of <paramref name="total"/> (0–1); zero when the total is.</summary>
        public static decimal Share(decimal amount, decimal total)
            => total == 0 ? 0 : Math.Round(amount / total, 4);
    }
}
