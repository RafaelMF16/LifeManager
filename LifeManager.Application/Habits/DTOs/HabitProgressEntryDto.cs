using LifeManager.Domain.Habits;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>One amount logged towards a habit's daily target.</summary>
    public record HabitProgressEntryDto(int Id, int Value, DateTimeOffset CreatedAt)
    {
        public static HabitProgressEntryDto From(HabitProgressEntry entry)
            => new(entry.Id!.Value, entry.Value, entry.CreatedAt);
    }
}
