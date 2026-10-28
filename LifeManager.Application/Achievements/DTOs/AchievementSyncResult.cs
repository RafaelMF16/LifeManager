using LifeManager.Domain.Achievements;

namespace LifeManager.Application.Achievements.DTOs
{
    /// <summary>
    /// Where the player stands after a sync, so the callers don't read it again: the progress (its level after the
    /// bonuses), every unlock and the ones this sync added.
    /// </summary>
    public record AchievementSyncResult(
        AchievementProgress Progress,
        IReadOnlyList<AchievementUnlock> Unlocks,
        IReadOnlyList<AchievementUnlock> NewlyUnlocked)
    {
        /// <summary>The unlocks the player hasn't been shown yet, in catalog order.</summary>
        public IReadOnlyList<NewAchievementDto> Unseen()
            => [.. Unlocks.Where(unlock => !unlock.IsSeen).OrderBy(unlock => unlock.Code).Select(NewAchievementDto.From)];
    }
}
