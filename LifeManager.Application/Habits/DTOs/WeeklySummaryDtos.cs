using LifeManager.Application.Ranking.DTOs;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary><c>GET /api/Habits/Challenges/Summary</c>: how last week went, shown once on the first visit of the week.</summary>
    /// <param name="WeekStart">The Monday of the summarized week.</param>
    /// <param name="Week">That week's figures.</param>
    /// <param name="PreviousWeek">The same figures for the week before it, to compare.</param>
    /// <param name="Challenges">The week's challenges (given up ones left out), already settled.</param>
    /// <param name="Ranking">The player's place in each of their ranking groups that week, best first; empty without groups.</param>
    public record WeeklySummaryDto(
        DateOnly WeekStart,
        DateOnly WeekEnd,
        WeeklySummaryFiguresDto Week,
        WeeklySummaryFiguresDto PreviousWeek,
        IReadOnlyList<ChallengeDto> Challenges,
        IReadOnlyList<RankingWeekPlaceDto> Ranking);

    /// <param name="CheckIns">Habits checked in (done) during the week.</param>
    /// <param name="CleanDays">Days without relapse of the habits to avoid.</param>
    /// <param name="Coins">Coins earned with habits (undos taken back) plus the rewards of the challenges completed.</param>
    /// <param name="ChallengesCompleted">The week's challenges completed.</param>
    public record WeeklySummaryFiguresDto(int CheckIns, int CleanDays, int Coins, int ChallengesCompleted);

    /// <summary><c>POST /api/Habits/Challenges/Summary/Seen</c>.</summary>
    public record WeeklySummarySeenDto(DateOnly WeekStart);
}
