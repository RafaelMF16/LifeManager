using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/Profile/Evolution</c>: one point per game day, oldest first, the last one today.</summary>
    public record PlayerEvolutionDto(EvolutionRange Range, IReadOnlyList<EvolutionPointDto> Points)
    {
        public static PlayerEvolutionDto From(EvolutionRange range, IReadOnlyList<PlayerEvolutionPoint> points)
            => new(range, [.. points.Select(point => new EvolutionPointDto(point.Date, point.TotalXp, point.Coins))]);
    }

    /// <param name="Date"><c>yyyy-MM-dd</c>.</param>
    /// <param name="TotalXp">The XP total at the end of that day.</param>
    /// <param name="Coins">The coin balance at the end of that day.</param>
    public record EvolutionPointDto(DateOnly Date, int TotalXp, int Coins);
}
