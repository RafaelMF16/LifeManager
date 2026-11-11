using LifeManager.Domain.SavingsGoals.Enums;
using LifeManager.Domain.Shared.ValueObjects;

namespace LifeManager.Domain.SavingsGoals
{
    /// <summary>
    /// Where a goal stands, derived on every read from what is stored (pure). <see cref="Saved"/> is the amount saved
    /// before the goal plus its linked investments; the deadline splits what is left into months.
    /// </summary>
    public sealed record SavingsGoalProgress(
        decimal Saved,
        decimal Ratio,
        decimal Remaining,
        int? MonthsLeft,
        decimal? MonthlyNeeded,
        SavingsGoalPace Pace)
    {
        /// <summary>How many months, the current one included, the pace looks back at.</summary>
        public const int PaceWindowMonths = 3;

        public bool IsReached => Pace == SavingsGoalPace.Done;

        public static bool Reaches(decimal targetAmount, decimal initialAmount, decimal linkedSum)
            => initialAmount + linkedSum >= targetAmount;

        /// <param name="linkedSum">Every linked investment's amount, summed.</param>
        /// <param name="recentSaved">
        /// The linked investments dated in the last <see cref="PaceWindowMonths"/> months (the current one included);
        /// their average per month is compared with <see cref="MonthlyNeeded"/>.
        /// </param>
        public static SavingsGoalProgress Calculate(
            decimal targetAmount,
            decimal initialAmount,
            decimal linkedSum,
            YearMonth? targetMonth,
            YearMonth currentMonth,
            decimal recentSaved)
        {
            var saved = initialAmount + linkedSum;
            var ratio = decimal.Round(saved / targetAmount, 4, MidpointRounding.AwayFromZero);
            var remaining = Math.Max(0m, targetAmount - saved);
            var reached = saved >= targetAmount;

            int? monthsLeft = targetMonth is null ? null : targetMonth.Ordinal - currentMonth.Ordinal + 1;
            decimal? monthlyNeeded = monthsLeft switch
            {
                null => null,
                _ when reached => 0m,
                // The deadline is this month or already passed: everything left is due now.
                <= 1 => remaining,
                _ => CeilingToCent(remaining / monthsLeft.Value)
            };

            var pace = reached
                ? SavingsGoalPace.Done
                : monthlyNeeded is null
                    ? SavingsGoalPace.NoDeadline
                    : recentSaved / PaceWindowMonths >= monthlyNeeded.Value
                        ? SavingsGoalPace.OnTrack
                        : SavingsGoalPace.Behind;

            return new SavingsGoalProgress(saved, ratio, remaining, monthsLeft, monthlyNeeded, pace);
        }

        private static decimal CeilingToCent(decimal value)
            => decimal.Ceiling(value * 100m) / 100m;
    }
}
