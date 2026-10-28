namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>POST /api/Habits/{id}/Progress</c>.</summary>
    /// <param name="Date">"yyyy-MM-dd": today or yesterday.</param>
    /// <param name="Value">The amount logged, in the habit's unit (1-100000).</param>
    public record HabitProgressDto(DateOnly Date, int Value);
}
