using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>A habit's daily target ("2000 ml", quick-add "+250").</summary>
    public record HabitTargetDto(int Value, string Unit, int? Step)
    {
        /// <returns>Null for a habit without a target.</returns>
        public static HabitTargetDto? From(HabitTarget? target)
            => target is null ? null : new(target.Value, target.Unit, target.Step);
    }
}
