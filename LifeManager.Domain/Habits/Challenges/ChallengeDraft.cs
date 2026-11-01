using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Habits.Challenges
{
    /// <summary>
    /// What creating (or previewing) a challenge needs: the habit, the request, and the week's figures read by the
    /// caller (progress so far, the days done and the baseline).
    /// </summary>
    public record ChallengeDraft(
        Habit Habit,
        ChallengeType Type,
        int Target,
        DateOnly WeekStart,
        DateOnly Today,
        int Progress,
        decimal? Baseline,
        IReadOnlyCollection<DateOnly> DoneDates);
}
