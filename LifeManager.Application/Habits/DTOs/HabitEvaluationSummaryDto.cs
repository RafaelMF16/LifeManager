namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>What one day-close run did for one user, for the job's log.</summary>
    /// <param name="DaysEvaluated">Habit-days whose cursor this run moved.</param>
    public record HabitEvaluationSummaryDto(int DaysEvaluated, int Knockouts);
}
