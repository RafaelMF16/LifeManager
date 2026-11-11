namespace LifeManager.Application.Home.DTOs
{
    /// <summary>The Home's summary of the day, in one read (<c>GET api/Home/Overview</c>).</summary>
    /// <param name="Today">The business time zone's today.</param>
    /// <param name="Player">Null before the player's first change to the habits game.</param>
    public record HomeOverviewDto(DateOnly Today, HomeHabitsDto Habits, HomePlayerDto? Player, HomeFinanceDto Finance);

    /// <summary>The same counts as <c>GET api/Habits/Today</c>.</summary>
    /// <param name="DueToday">Habits to build that can be checked in today.</param>
    /// <param name="YesterdayPending">Habits to build that can still be checked in for yesterday.</param>
    /// <param name="Avoiding">Habits to avoid in force today.</param>
    /// <param name="BestStreak">The active habit with the longest current streak; null when none has one.</param>
    public record HomeHabitsDto(int DueToday, int DoneToday, int YesterdayPending, int Avoiding, int RelapsedToday, HomeBestStreakDto? BestStreak);

    /// <param name="Days">The streak in days: a weekly habit's weeks count 7 days each.</param>
    public record HomeBestStreakDto(int HabitId, string Name, int Days);

    public record HomePlayerDto(int Level, int Hp, int MaxHp, int Coins);

    /// <summary>The current month (business time zone).</summary>
    /// <param name="Month">"yyyy-MM".</param>
    /// <param name="MonthlySummaryId">Null when the user has no such month yet (totals are then zero).</param>
    /// <param name="Budget">Null without a budget plan for the month.</param>
    public record HomeFinanceDto(
        string Month,
        int? MonthlySummaryId,
        decimal Income,
        decimal Expense,
        decimal Investment,
        decimal Balance,
        HomeBudgetDto? Budget);

    /// <param name="SpendingLimit">The plan's total minus its investment slice (the free part included).</param>
    /// <param name="Spent">The month's expenses.</param>
    /// <param name="Tightest">The expense slice with the highest spent-to-amount ratio; null without expense slices.</param>
    public record HomeBudgetDto(decimal SpendingLimit, decimal Spent, HomeTightestSliceDto? Tightest);

    /// <param name="Ratio">Spent over the slice's amount (1 = exactly at it), 4 places.</param>
    public record HomeTightestSliceDto(int CategoryId, string Name, decimal Ratio);
}
