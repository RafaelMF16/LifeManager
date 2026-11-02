using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Habits.DTOs
{
    /// <param name="Unit">The habit's daily target unit, for a volume challenge; null for frequency.</param>
    /// <param name="Progress">Days done (frequency) or amount logged (volume) in the week, recounted from what is stored.</param>
    /// <param name="DaysLeft">Days left to play, today included: 7 before the week starts, 0 once it ended.</param>
    public record ChallengeDto(
        int Id,
        int HabitId,
        string HabitName,
        ChallengeType Type,
        int Target,
        string? Unit,
        int Progress,
        int DaysLeft,
        DateOnly WeekStart,
        decimal? Baseline,
        ChallengeEffort Effort,
        int RewardCoins,
        int RewardXp,
        ChallengeStatus Status,
        DateTimeOffset? CompletedAt);

    /// <summary>A challenge the player can accept as is: creating it with these values gives this effort and reward.</summary>
    public record ChallengeSuggestionDto(
        int HabitId,
        string HabitName,
        ChallengeType Type,
        int Target,
        string? Unit,
        decimal Baseline,
        ChallengeEffort Effort,
        int RewardCoins,
        int RewardXp);

    /// <summary>A habit the form can pick for the week, with the limits of its target.</summary>
    /// <param name="MaxVolume">Null for a habit without a daily target (frequency only).</param>
    /// <param name="FrequencyProgress">Days already done in the week; a target must be above it.</param>
    /// <param name="VolumeProgress">Amount already logged in the week; a target must be above it.</param>
    public record ChallengeHabitOptionDto(
        int HabitId,
        string Name,
        string? Unit,
        int MaxFrequency,
        int? MaxVolume,
        int FrequencyProgress,
        int VolumeProgress);

    /// <summary><c>GET /api/Habits/Challenges?week=</c>: the week's challenges, what can still be created and the suggestions.</summary>
    /// <param name="IsOpen">The week still takes challenges: the current one until Wednesday, the next one always.</param>
    /// <param name="CanCreate">The week is open, has room and has a habit to pick.</param>
    public record ChallengeWeekDto(
        ChallengeWeek Week,
        DateOnly WeekStart,
        DateOnly WeekEnd,
        bool IsOpen,
        bool CanCreate,
        IReadOnlyList<ChallengeDto> Challenges,
        IReadOnlyList<ChallengeSuggestionDto> Suggestions,
        IReadOnlyList<ChallengeHabitOptionDto> Habits);

    public record CreateChallengeDto(int HabitId, ChallengeType Type, int Target, ChallengeWeek Week);

    /// <summary>What a challenge would be worth, before creating it.</summary>
    public record ChallengePreviewDto(ChallengeEffort Effort, int RewardCoins, int RewardXp, decimal? Baseline);

    public record ChallengesSeenDto(IReadOnlyList<int>? Ids);

    public record ChallengeHistoryQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    }
}
