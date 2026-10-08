namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>POST /api/Habits/{id}/CheckIns</c>.</summary>
    /// <param name="Date">"yyyy-MM-dd": today or yesterday.</param>
    public record HabitCheckInDto(DateOnly Date);
}
