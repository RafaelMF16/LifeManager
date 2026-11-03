using LifeManager.Domain.Habits;

namespace LifeManager.Application.FinanceDashboard.DTOs
{
    /// <summary>
    /// The clean days of the habits to avoid with a cost per relapse, in money: each clean day is one relapse not paid
    /// for. The habits that saved the most come first; an empty list when no habit has a cost.
    /// </summary>
    public record DashboardHabitSavingsDto(decimal Total, IReadOnlyList<DashboardHabitSavingsItemDto> Habits)
    {
        public static DashboardHabitSavingsDto From(HabitSavingsSummary summary)
            => new(summary.Total, [.. summary.Habits.Select(item => new DashboardHabitSavingsItemDto(item.HabitId, item.Name, item.CleanDays, item.Amount))]);
    }

    public record DashboardHabitSavingsItemDto(int HabitId, string Name, int CleanDays, decimal Amount);
}
