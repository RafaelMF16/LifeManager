using LifeManager.Domain.Ranking;

namespace LifeManager.Application.Ranking.DTOs
{
    public record WeeklyScoreDayDto(DateOnly Date, int Kept, bool Perfect, int Points)
    {
        public static WeeklyScoreDayDto From(WeeklyScoreDay day)
            => new(day.Date, day.Kept, day.Perfect, day.Points);
    }

    public record WeeklyScoreDto(DateOnly WeekStart, DateOnly WeekEnd, int Points, int KeptDays, int PerfectDays, IReadOnlyList<WeeklyScoreDayDto> Days)
    {
        public static WeeklyScoreDto From(WeeklyScoreResult result)
            => new(result.WeekStart, GameWeek.End(result.WeekStart), result.Points, result.KeptDays, result.PerfectDays,
                [.. result.Days.Select(WeeklyScoreDayDto.From)]);
    }
}
