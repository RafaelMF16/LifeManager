using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Ranking
{
    /// <summary>A habit as the weekly score reads it; <see cref="ArchivedAt"/> is turned into a business day by the caller.</summary>
    public record RankingHabitRow(UserId UserId, int HabitId, HabitKind Kind, HabitFrequency Frequency, DateOnly StartDate, DateTimeOffset? ArchivedAt);

    public record RankingCheckInRow(UserId UserId, int HabitId, DateOnly Date, HabitCheckInStatus Status);

    /// <summary>Everything the weekly score of some players needs, read in one go.</summary>
    public record RankingScoreInputs(IReadOnlyList<RankingHabitRow> Habits, IReadOnlyList<RankingCheckInRow> CheckIns);
}
