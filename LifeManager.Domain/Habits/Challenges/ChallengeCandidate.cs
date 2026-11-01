namespace LifeManager.Domain.Habits.Challenges
{
    /// <summary>
    /// A habit that may get a suggested challenge, with the figures the caller read: the baselines (null without a closed
    /// week), the days it can be done in the week, the most days still reachable and this week's progress so far.
    /// </summary>
    public record ChallengeCandidate(
        Habit Habit,
        decimal? FrequencyBaseline,
        decimal? VolumeBaseline,
        int PossibleDays,
        int MaxFrequency,
        int FrequencyProgress,
        int VolumeProgress);
}
