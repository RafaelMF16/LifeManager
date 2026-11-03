namespace LifeManager.Domain.Achievements
{
    /// <summary>
    /// The player's whole history in the numbers the achievements count, archived habits included. Read fresh each
    /// time, so achievements count the past too.
    /// </summary>
    /// <param name="CheckIns">Check-ins that exist now (an undone one no longer counts).</param>
    /// <param name="LongestStreakDays">The best streak record of any habit; a weekly habit's weeks count 7 days each.</param>
    /// <param name="Level">The profile's current level.</param>
    /// <param name="Redemptions">Reward redemptions not undone.</param>
    /// <param name="CleanDays">Clean days of habits to avoid.</param>
    /// <param name="KeptDaysAfterFirstKnockout">Days done or clean after the day of the first knockout; 0 without one.</param>
    /// <param name="MostKeptDaysInOneHabit">The most days done or clean of a single habit.</param>
    /// <param name="RankingWins">Distinct weeks the player finished first in a group (<see cref="Ranking.RankingWeekResult.CountsForAchievements"/>).</param>
    /// <param name="RankingPodiums">Distinct weeks the player finished on a group's podium, by the same rule.</param>
    /// <param name="BudgetMonths">Closed months within the spending limit of an eligible plan (<see cref="FinanceMonthProgress"/>).</param>
    /// <param name="InvestmentMonths">Closed months that reached the investment slice of an eligible plan.</param>
    /// <param name="SavingsMonths">Closed months that saved at least part of the income (<see cref="Habits.GameRules.FinanceSavingsRateGoal"/>).</param>
    public record AchievementProgress(
        int CheckIns,
        int LongestStreakDays,
        int Level,
        int Redemptions,
        int CleanDays,
        int KeptDaysAfterFirstKnockout,
        int MostKeptDaysInOneHabit,
        int RankingWins = 0,
        int RankingPodiums = 0,
        int BudgetMonths = 0,
        int InvestmentMonths = 0,
        int SavingsMonths = 0)
    {
        /// <summary>This progress with the Finance counts, which come from the money side rather than the game's own read.</summary>
        public AchievementProgress WithFinance(FinanceMonthCounts finance)
            => this with { BudgetMonths = finance.BudgetMonths, InvestmentMonths = finance.InvestmentMonths, SavingsMonths = finance.SavingsMonths };
    }
}
